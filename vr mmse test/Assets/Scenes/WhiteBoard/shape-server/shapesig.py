# shapesig.py
import numpy as np
import cv2
from typing import List, Dict, Any, Tuple

# -------------------- 基礎工具 --------------------
def _to_gray_any(img: np.ndarray) -> np.ndarray:
    if img.ndim == 2: return img.copy()
    if img.ndim == 3:
        c = img.shape[2]
        if c == 4:
            try:    return cv2.cvtColor(img, cv2.COLOR_RGBA2GRAY)
            except: return cv2.cvtColor(img, cv2.COLOR_BGRA2GRAY)
        elif c == 3:
            try:    return cv2.cvtColor(img, cv2.COLOR_RGB2GRAY)
            except: return cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    return cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)

def _binarize_black_strokes(gray: np.ndarray) -> np.ndarray:
    g = cv2.GaussianBlur(gray, (5, 5), 0)
    _, th = cv2.threshold(g, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
    H, W = g.shape[:2]
    if cv2.countNonZero(th) < 0.001 * H * W:
        _, th = cv2.threshold(g, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    th = cv2.morphologyEx(th, cv2.MORPH_OPEN, np.ones((3, 3), np.uint8), 1)
    return th

# -------------------- 幾何規則（數量/面積/類型/交疊） --------------------
def extract_shapes_from_array(image_array: np.ndarray) -> List[Dict[str, Any]]:
    gray = _to_gray_any(image_array)
    binary = _binarize_black_strokes(gray)
    contours, _ = cv2.findContours(binary, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    shapes = []
    for cnt in contours:
        if cv2.contourArea(cnt) < 100: continue
        m = cv2.moments(cnt)
        cx, cy = (int(m['m10']/m['m00']), int(m['m01']/m['m00'])) if m['m00'] != 0 else (0, 0)
        if len(cnt) >= 5: orientation = cv2.fitEllipse(cnt)[2]
        else:             orientation = 0.0
        shapes.append({
            'contour': cnt,
            'center': (cx, cy),
            'area': float(cv2.contourArea(cnt)),
            'perimeter': float(cv2.arcLength(cnt, True)),
            'orientation': float(orientation),
            'approximation': cv2.approxPolyDP(cnt, 0.02*cv2.arcLength(cnt, True), True),
            'bounding_rect': cv2.boundingRect(cnt)
        })
    return shapes

def _overlap_pairs_and_area(shapes: List[Dict[str,Any]]) -> Tuple[int, int]:
    n = len(shapes)
    if n < 2: return 0, 0
    # 將每個輪廓填滿，標號 1..n
    b = 0
    # 動態取畫布，避免 1024 固定過小/過大
    xs, ys = [], []
    for s in shapes:
        x,y,w,h = s['bounding_rect']
        xs += [x, x+w]; ys += [y, y+h]
    W = max(8, max(xs) + 8); H = max(8, max(ys) + 8)
    canvas = np.zeros((H, W), np.uint8)
    for i, s in enumerate(shapes):
        cv2.drawContours(canvas, [s['contour']], 0, i+1, -1)
    count = 0; area_sum = 0
    for i in range(n):
        for j in range(i+1, n):
            a = np.uint8(canvas == (i+1))
            b = np.uint8(canvas == (j+1))
            o = cv2.bitwise_and(a, b)
            inter = int(o.sum())
            if inter > 0:
                count += 1
                area_sum += inter
    return count, area_sum

def score_signatures(user_shapes, target_shapes) -> Dict[str, Any]:
    # 形狀數量 (20)
    uc, tc = len(user_shapes), len(target_shapes)
    if uc == 0 or tc == 0:
        return {'score': 0, 'details': {
            'shape_count_score': 0, 'area_score': 0, 'shape_type_score': 0, 'overlap_score': 0,
            'user_shapes': uc, 'target_shapes': tc, 'multiplier': 0.0,
            'count_ratio': 0.0, 'type_match_ratio': 0.0, 'overlap_norm': 0.0,
            'target_overlap_pairs': 0
        }}
    count_ratio = min(uc, tc) / max(uc, tc)
    count_score = 20.0 * count_ratio

    # 面積比 (20)
    ua = sum(s['area'] for s in user_shapes)
    ta = sum(s['area'] for s in target_shapes)
    area_score = 20.0 * (min(ua, ta) / max(ua, ta)) if max(ua, ta) > 0 else 0.0

    # 類型 (30)
    def type_of(s):
        v = len(s['approximation'])
        if v <= 4: return v  # 3=三角 4=四邊
        circ = 4*np.pi*s['area']/(s['perimeter']*s['perimeter'] + 1e-6)
        return 0 if circ > 0.8 else v  # 0=圓
    u_types = [type_of(s) for s in user_shapes]
    t_types = [type_of(s) for s in target_shapes]
    matches = 0; tmp = u_types.copy()
    for tt in t_types:
        if tt in tmp:
            matches += 1
            tmp.remove(tt)
    type_match_ratio = matches/len(t_types) if len(t_types) else 0.0
    type_score = 30.0 * type_match_ratio

    # 交疊 (30)
    u_pairs, u_area = _overlap_pairs_and_area(user_shapes)
    t_pairs, t_area = _overlap_pairs_and_area(target_shapes)
    if t_pairs == 0 and u_pairs == 0:
        overlap_norm = 1.0
        ov_score = 30.0
    elif (t_pairs == 0) ^ (u_pairs == 0):
        overlap_norm = 0.0
        ov_score = 0.0
    else:
        count_ratio2 = min(u_pairs, t_pairs) / max(u_pairs, t_pairs)
        area_ratio2  = min(u_area,  t_area ) / max(u_area,  t_area ) if max(u_area,t_area)>0 else 0.0
        overlap_norm = 0.5*count_ratio2 + 0.5*area_ratio2
        ov_score = 30.0 * overlap_norm

    raw = count_score + area_score + type_score + ov_score

    # 缺件懲罰（更嚴）：最小倍率從 0.5 -> 0.35
    mult = 1.0 if uc >= tc else max(0.35, uc/tc)
    base_score = min(100.0, raw * mult)

    return {
        'score': int(round(base_score)),
        'details': {
            'shape_count_score': count_score, 'area_score': area_score,
            'shape_type_score': type_score, 'overlap_score': ov_score,
            'user_shapes': uc, 'target_shapes': tc,
            'multiplier': mult,
            # Gate 需要的指標
            'count_ratio': count_ratio,
            'type_match_ratio': float(type_match_ratio),
            'overlap_norm': float(overlap_norm),
            'target_overlap_pairs': int(t_pairs)
        }
    }

# -------------------- GF-HOG / BoVW（輕量實作） --------------------
def _prep_gray(img: np.ndarray, side: int = 200) -> Tuple[np.ndarray, np.ndarray]:
    g = _to_gray_any(img)
    h, w = g.shape
    r = side / max(h, w)
    g = cv2.resize(g, (int(w*r), int(h*r)), interpolation=cv2.INTER_AREA)
    H, W = g.shape
    pad = np.zeros((side, side), np.uint8)
    y0 = (side - H)//2; x0 = (side - W)//2
    pad[y0:y0+H, x0:x0+W] = g
    edges = cv2.Canny(pad, 50, 150)
    return pad, edges

def _gradient_field(gray: np.ndarray, edges: np.ndarray) -> Tuple[np.ndarray, np.ndarray]:
    gx = cv2.Sobel(gray, cv2.CV_32F, 1, 0, ksize=3)
    gy = cv2.Sobel(gray, cv2.CV_32F, 0, 1, ksize=3)
    mag = np.sqrt(gx*gx + gy*gy) + 1e-6
    vx = (gx/mag) * (edges>0).astype(np.float32)
    vy = (gy/mag) * (edges>0).astype(np.float32)
    for _ in range(3):
        vx = cv2.GaussianBlur(vx, (0,0), 2.0)
        vy = cv2.GaussianBlur(vy, (0,0), 2.0)
    ang = np.arctan2(vy, vx); m = np.sqrt(vx*vx + vy*vy)
    return ang, m

def _local_hog_from_field(ang: np.ndarray, mag: np.ndarray,
                          step: int = 8, wins=(16,28,40), nc: int = 6, q: int = 9) -> np.ndarray:
    H, W = ang.shape
    descs = []
    for win in wins:
        cell = max(1, win // nc)
        block = cell * nc
        half = block // 2
        if block < 2: continue
        for y in range(half, H - half, step):
            for x in range(half, W - half, step):
                a = ang[y-half:y+half, x-half:x+half]
                w = mag[y-half:y+half, x-half:x+half]
                ah = []
                for cy in range(nc):
                    for cx in range(nc):
                        ys = cy*cell; xs = cx*cell
                        patch_a = a[ys:ys+cell, xs:xs+cell]
                        patch_w = w[ys:ys+cell, xs:xs+cell]
                        hist = np.zeros(q, np.float32)
                        ang01 = (np.mod(patch_a, np.pi)/np.pi)*q
                        bins = np.clip(ang01.astype(np.int32), 0, q-1)
                        for b in range(q):
                            hist[b] = float(patch_w[bins==b].sum())
                        hist = hist / (np.linalg.norm(hist)+1e-6)
                        ah.append(hist)
                descs.append(np.concatenate(ah))  # 固定長度 q*nc*nc
    if len(descs)==0: return np.empty((0, q*nc*nc), dtype=np.float32)
    return np.vstack(descs).astype(np.float32)

def _bovw_hist(dA: np.ndarray, dB: np.ndarray, k: int = 128) -> Tuple[np.ndarray, np.ndarray]:
    allD = np.vstack([dA, dB]).astype(np.float32)
    crit = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 50, 1e-3)
    _ret, labels, _centers = cv2.kmeans(allD, k, None, crit, 3, cv2.KMEANS_PP_CENTERS)
    la = labels[:len(dA)].ravel(); lb = labels[len(dA):].ravel()
    hA = np.bincount(la, minlength=k).astype(np.float32); hB = np.bincount(lb, minlength=k).astype(np.float32)
    hA /= (hA.sum()+1e-6); hB /= (hB.sum()+1e-6)
    return hA, hB

def _chi2(a: np.ndarray, b: np.ndarray) -> float:
    return float(0.5*np.sum(((a-b)**2)/(a+b+1e-8)))

def _gfhog_logistic_score(chi2: float, m: float = 0.8, k: float = 4.0) -> float:
    """把 χ² 距離轉成 0..100 分：score=100/(1+exp(k*(chi2-m)))。"""
    s = 1.0 / (1.0 + np.exp(k*(chi2 - m)))
    return float(max(0.0, min(1.0, s)) * 100.0)

def gfhog_score(user_img: np.ndarray, target_img: np.ndarray, k: int = 128,
                m_logistic: float = 0.8, k_logistic: float = 4.0) -> Tuple[float, Dict[str,float]]:
    gU, eU = _prep_gray(user_img); gT, eT = _prep_gray(target_img)
    angU, magU = _gradient_field(gU, eU); angT, magT = _gradient_field(gT, eT)
    dU = _local_hog_from_field(angU, magU); dT = _local_hog_from_field(angT, magT)
    if len(dU)==0 or len(dT)==0: return 0.0, {'pairs': 0.0, 'chi2': 1e9}
    hU, hT = _bovw_hist(dU, dT, k=k)
    chi2 = _chi2(hU, hT)
    score = _gfhog_logistic_score(chi2, m=m_logistic, k=k_logistic)
    return float(score), {'pairs': float(len(dU)+len(dT)), 'chi2': float(chi2)}

# 供 Gate 使用的快速邊緣 IoU
def _edge_iou(user_img: np.ndarray, target_img: np.ndarray) -> float:
    _, eU = _prep_gray(user_img); _, eT = _prep_gray(target_img)
    k = np.ones((3,3), np.uint8)
    eU = cv2.dilate((eU>0).astype(np.uint8), k, 1)
    eT = cv2.dilate((eT>0).astype(np.uint8), k, 1)
    inter = float(np.sum((eU>0) & (eT>0)))
    union = float(np.sum((eU>0) | (eT>0)))
    if union == 0: return 0.0
    return inter/union

# -------------------- 混合 + Gate --------------------
def hybrid_score(user_img: np.ndarray, target_img: np.ndarray,
                 user_shapes, target_shapes) -> Dict[str, Any]:
    geo = score_signatures(user_shapes, target_shapes)   # 0..100
    # GF-HOG（Logistic 版）
    gfhog, extra = gfhog_score(user_img, target_img, k=128, m_logistic=0.8, k_logistic=4.0)

    # 加權合成
    final = 0.6 * gfhog + 0.4 * geo['score']

    # -------- 硬性 Gate（封頂） --------
    det = geo['details']
    # 1) 數量比太差
    if det['count_ratio'] < 0.60:
        final = min(final, 55.0)
    # 2) 類型匹配 < 50%
    if det['type_match_ratio'] < 0.50:
        final = min(final, 60.0)
    # 3) 目標本來就有交疊，但你沒畫到或重疊很差
    if det['target_overlap_pairs'] > 0 and det['overlap_norm'] < 0.25:
        final = min(final, 65.0)
    # 4) 邊緣 IoU 極低（很不像）
    iou = _edge_iou(user_img, target_img)
    if iou < 0.08:
        final = min(final, 45.0)

    final = int(round(max(0.0, min(100.0, final))))
    return {
        'score': final,
        'details': {
            'geo_score': geo['score'],
            'gfhog_score': gfhog,
            'gfhog_pairs': extra['pairs'],
            'gfhog_chi2': extra['chi2'],
            'edge_iou': float(iou),
            'gates': {
                'count_ratio': det['count_ratio'],
                'type_match_ratio': det['type_match_ratio'],
                'overlap_norm': det['overlap_norm'],
                'target_overlap_pairs': det['target_overlap_pairs']
            }
        }
    }
