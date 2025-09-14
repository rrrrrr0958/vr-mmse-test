# shapesig.py
import numpy as np
import cv2
from typing import List, Dict, Any, Tuple

# -------------------- 基礎工具 --------------------
def _to_gray_any(img: np.ndarray) -> np.ndarray:
    """把任意通道數的影像轉灰階；支援 RGB / BGR / RGBA / BGRA / Gray。"""
    if img.ndim == 2:
        return img.copy()
    if img.ndim == 3:
        c = img.shape[2]
        if c == 4:
            # 假設來自 PIL → 通常是 RGBA；若來自 OpenCV → BGRA 也能轉灰
            try:
                return cv2.cvtColor(img, cv2.COLOR_RGBA2GRAY)
            except:
                return cv2.cvtColor(img, cv2.COLOR_BGRA2GRAY)
        elif c == 3:
            # PIL→RGB / OpenCV→BGR；兩者轉灰僅係數不同，不影響二值化
            try:
                return cv2.cvtColor(img, cv2.COLOR_RGB2GRAY)
            except:
                return cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    # 其他情況：保守退回單通道
    return cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)

def _binarize_black_strokes(gray: np.ndarray) -> np.ndarray:
    """把白底(淺底)的深色筆劃變成白色(255)，背景黑(0)；輸出二值影像。"""
    g = cv2.GaussianBlur(gray, (5, 5), 0)
    # 先試 Otsu 反相（黑線→白）
    _, th = cv2.threshold(g, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
    H, W = g.shape[:2]
    if cv2.countNonZero(th) < 0.001 * H * W:
        # 幾乎全黑 → 換正常 Otsu
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
        if cv2.contourArea(cnt) < 100:
            continue
        m = cv2.moments(cnt)
        cx, cy = (int(m['m10']/m['m00']), int(m['m01']/m['m00'])) if m['m00'] != 0 else (0, 0)
        if len(cnt) >= 5:
            orientation = cv2.fitEllipse(cnt)[2]
        else:
            orientation = 0.0
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

def _overlap_similarity(user_shapes, target_shapes) -> float:
    if len(user_shapes) < 2 and len(target_shapes) < 2:
        return 30.0

    canvas = 1024
    u = np.zeros((canvas, canvas), np.uint8)
    t = np.zeros((canvas, canvas), np.uint8)
    for i, s in enumerate(user_shapes):
        cv2.drawContours(u, [s['contour']], 0, i + 1, -1)
    for i, s in enumerate(target_shapes):
        cv2.drawContours(t, [s['contour']], 0, i + 1, -1)

    def collect_overlaps(img, n):
        areas = []
        for i in range(n):
            for j in range(i + 1, n):
                a = np.uint8(img == (i + 1))
                b = np.uint8(img == (j + 1))
                o = cv2.bitwise_and(a, b)
                area = int(o.sum())
                if area > 0:
                    areas.append(area)
        return areas

    u_os = collect_overlaps(u, len(user_shapes))
    t_os = collect_overlaps(t, len(target_shapes))

    if len(u_os) == 0 and len(t_os) == 0:
        return 30.0
    if (len(u_os) == 0) ^ (len(t_os) == 0):
        return 0.0

    count_ratio = min(len(u_os), len(t_os)) / max(len(u_os), len(t_os))
    area_ratio = min(sum(u_os), sum(t_os)) / max(sum(u_os), sum(t_os))
    return (0.5 * count_ratio + 0.5 * area_ratio) * 30.0

def score_signatures(user_shapes, target_shapes) -> Dict[str, Any]:
    if not user_shapes or not target_shapes:
        return {'score': 0, 'details': {
            'shape_count_score': 0, 'area_score': 0, 'shape_type_score': 0, 'overlap_score': 0,
            'user_shapes': len(user_shapes) if user_shapes else 0,
            'target_shapes': len(target_shapes) if target_shapes else 0,
            'multiplier': 0.0
        }}

    # 形狀數量 (20)
    count_ratio = min(len(user_shapes), len(target_shapes)) / max(len(user_shapes), len(target_shapes))
    count_score = 20.0 * count_ratio

    # 面積比 (20)
    ua = sum(s['area'] for s in user_shapes)
    ta = sum(s['area'] for s in target_shapes)
    area_score = 20.0 * (min(ua, ta) / max(ua, ta)) if max(ua, ta) > 0 else 0.0

    # 形狀類型 (30)
    def type_of(s):
        v = len(s['approximation'])
        if v <= 4:  # 3=三角, 4=四邊
            return v
        circ = 4 * np.pi * s['area'] / (s['perimeter'] * s['perimeter'] + 1e-6)
        return 0 if circ > 0.8 else v  # 0=圓

    u_types = [type_of(s) for s in user_shapes]
    t_types = [type_of(s) for s in target_shapes]
    matches = 0
    tmp = u_types.copy()
    for tt in t_types:
        if tt in tmp:
            matches += 1
            tmp.remove(tt)
    type_score = 30.0 * (matches / len(t_types)) if len(t_types) else 0.0

    # 交疊 (30)
    ov_score = _overlap_similarity(user_shapes, target_shapes)

    raw = count_score + area_score + type_score + ov_score
    mult = 1.0 if len(user_shapes) >= len(target_shapes) else max(0.5, len(user_shapes)/len(target_shapes))
    base_score = min(100.0, raw * mult)

    return {
        'score': int(round(base_score)),
        'details': {
            'shape_count_score': count_score, 'area_score': area_score,
            'shape_type_score': type_score, 'overlap_score': ov_score,
            'user_shapes': len(user_shapes), 'target_shapes': len(target_shapes),
            'multiplier': mult
        }
    }

# -------------------- GF-HOG / BoVW（輕量實作） --------------------
def _prep_gray(img: np.ndarray, side: int = 200) -> Tuple[np.ndarray, np.ndarray]:
    g = _to_gray_any(img)
    # 做到固定畫布大小（邊保持比例，周圍補黑）
    h, w = g.shape
    r = side / max(h, w)
    g = cv2.resize(g, (int(w * r), int(h * r)), interpolation=cv2.INTER_AREA)
    H, W = g.shape
    pad = np.zeros((side, side), np.uint8)
    y0 = (side - H) // 2
    x0 = (side - W) // 2
    pad[y0:y0+H, x0:x0+W] = g
    edges = cv2.Canny(pad, 50, 150)
    return pad, edges

def _gradient_field(gray: np.ndarray, edges: np.ndarray) -> Tuple[np.ndarray, np.ndarray]:
    gx = cv2.Sobel(gray, cv2.CV_32F, 1, 0, ksize=3)
    gy = cv2.Sobel(gray, cv2.CV_32F, 0, 1, ksize=3)
    mag = np.sqrt(gx * gx + gy * gy) + 1e-6
    vx = (gx / mag) * (edges > 0).astype(np.float32)
    vy = (gy / mag) * (edges > 0).astype(np.float32)
    # 擴散平滑
    for _ in range(3):
        vx = cv2.GaussianBlur(vx, (0, 0), 2.0)
        vy = cv2.GaussianBlur(vy, (0, 0), 2.0)
    ang = np.arctan2(vy, vx)  # [-pi, pi]
    m = np.sqrt(vx * vx + vy * vy)
    return ang, m

def _local_hog_from_field(
    ang: np.ndarray, mag: np.ndarray,
    step: int = 8,
    wins=(16, 28, 40),   # 多尺度視窗
    nc: int = 6,         # 固定 cell 網格數（所有尺度一致 → 向量維度一致）
    q: int = 9           # 方向 bins
) -> np.ndarray:
    """
    回傳形狀：(N, q*nc*nc)，確保每筆描述子長度一致，方便 kmeans。
    """
    H, W = ang.shape
    descs = []
    for win in wins:
        cell = max(1, win // nc)
        block = cell * nc
        half = block // 2
        if block < 2: 
            continue

        for y in range(half, H - half, step):
            for x in range(half, W - half, step):
                a = ang[y-half:y+half, x-half:x+half]
                w = mag[y-half:y+half, x-half:x+half]

                ah = []
                for cy in range(nc):
                    for cx in range(nc):
                        ys = cy * cell
                        xs = cx * cell
                        patch_a = a[ys:ys+cell, xs:xs+cell]
                        patch_w = w[ys:ys+cell, xs:xs+cell]

                        hist = np.zeros(q, np.float32)
                        ang01 = (np.mod(patch_a, np.pi) / np.pi) * q  # 0..pi（無向）
                        bins = np.clip(ang01.astype(np.int32), 0, q-1)
                        for b in range(q):
                            hist[b] = float(patch_w[bins == b].sum())
                        hist = hist / (np.linalg.norm(hist) + 1e-6)
                        ah.append(hist)

                desc = np.concatenate(ah)  # 長度固定：q*nc*nc
                descs.append(desc)

    if len(descs) == 0:
        return np.empty((0, q * nc * nc), dtype=np.float32)
    return np.vstack(descs).astype(np.float32)

def _bovw_hist(dA: np.ndarray, dB: np.ndarray, k: int = 128) -> Tuple[np.ndarray, np.ndarray]:
    allD = np.vstack([dA, dB]).astype(np.float32)
    crit = (cv2.TERM_CRITERIA_EPS + cv2.TERM_CRITERIA_MAX_ITER, 50, 1e-3)
    _ret, labels, _centers = cv2.kmeans(allD, k, None, crit, 3, cv2.KMEANS_PP_CENTERS)
    la = labels[:len(dA)].ravel()
    lb = labels[len(dA):].ravel()
    hA = np.bincount(la, minlength=k).astype(np.float32)
    hB = np.bincount(lb, minlength=k).astype(np.float32)
    hA /= (hA.sum() + 1e-6)
    hB /= (hB.sum() + 1e-6)
    return hA, hB

def _chi2(a: np.ndarray, b: np.ndarray) -> float:
    return float(0.5 * np.sum(((a - b) ** 2) / (a + b + 1e-8)))

def gfhog_score(user_img: np.ndarray, target_img: np.ndarray, k: int = 128) -> Tuple[float, Dict[str, float]]:
    gU, eU = _prep_gray(user_img)
    gT, eT = _prep_gray(target_img)
    angU, magU = _gradient_field(gU, eU)
    angT, magT = _gradient_field(gT, eT)
    dU = _local_hog_from_field(angU, magU)   # (Nu, D)
    dT = _local_hog_from_field(angT, magT)   # (Nt, D)

    if len(dU) == 0 or len(dT) == 0:
        return 0.0, {'pairs': 0.0, 'chi2': 1e9}

    hU, hT = _bovw_hist(dU, dT, k=k)
    chi2 = _chi2(hU, hT)
    c = 0.8  # 平滑常數：越大越寬鬆
    score = 100.0 * (1.0 - (chi2 / (chi2 + c)))
    return float(score), {'pairs': float(len(dU) + len(dT)), 'chi2': float(chi2)}

# -------------------- 混合總分 --------------------
def hybrid_score(user_img: np.ndarray, target_img: np.ndarray,
                 user_shapes, target_shapes) -> Dict[str, Any]:
    geo = score_signatures(user_shapes, target_shapes)   # 幾何 0~100
    gfhog, extra = gfhog_score(user_img, target_img, k=128)

    final = int(round(min(100.0, 0.6 * gfhog + 0.4 * geo['score'])))
    return {
        'score': final,
        'details': {
            'geo_score': geo['score'],
            'gfhog_score': gfhog,
            'gfhog_pairs': extra['pairs'],
            'gfhog_chi2': extra['chi2']
        }
    }
