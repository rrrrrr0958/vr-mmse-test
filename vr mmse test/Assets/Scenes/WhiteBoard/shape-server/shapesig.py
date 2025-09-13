import cv2
import numpy as np
import math
from typing import List, Dict, Tuple

# ---------- Utilities ----------
def _binarize_arr(img: np.ndarray) -> np.ndarray:
    # 對白底黑線友善：模糊 + Otsu 二值 + 反相 + 去小雜訊
    if img.ndim == 3:
        gray = cv2.cvtColor(img, cv2.COLOR_BGR2GRAY)
    else:
        gray = img.copy()
    blur = cv2.GaussianBlur(gray, (5, 5), 0)
    _, th = cv2.threshold(blur, 0, 255, cv2.THRESH_BINARY_INV + cv2.THRESH_OTSU)
    th = cv2.morphologyEx(th, cv2.MORPH_OPEN, np.ones((3,3), np.uint8), iterations=1)
    return th

def _bbox_aspect_ratio(poly: np.ndarray) -> float:
    x, y, w, h = cv2.boundingRect(poly)
    return (w / h) if h else 0.0

def _angle_to_cardinal(angle_rad: float) -> str:
    deg = (math.degrees(angle_rad) + 360.0) % 360.0
    if 315 <= deg or deg < 45:   return "right"
    if 45 <= deg < 135:          return "down"   # 影像座標系 y 向下
    if 135 <= deg < 225:         return "left"
    return "up"

def _contour_centroid(cnt: np.ndarray) -> Tuple[float,float]:
    M = cv2.moments(cnt)
    if M["m00"] == 0: return (0.0, 0.0)
    return (M["m10"]/M["m00"], M["m01"]/M["m00"])

def _triangle_orientation(approx: np.ndarray, centroid: Tuple[float,float]) -> str:
    # 以「質心 -> 最遠頂點」決定三角形朝向（四向）
    pts = approx.reshape(-1, 2).astype(float)
    c = np.array(centroid, dtype=float)
    dists = np.linalg.norm(pts - c, axis=1)
    apex = pts[int(np.argmax(dists))]
    vec = apex - c
    angle = math.atan2(vec[1], vec[0])
    return _angle_to_cardinal(angle)

def _rect_is_square(approx: np.ndarray, tol_ratio: float = 0.18) -> bool:
    rect = cv2.minAreaRect(approx.astype(np.float32))
    (cx, cy), (w, h), rot = rect
    if w == 0 or h == 0: return False
    ratio = max(w, h) / min(w, h)
    return abs(ratio - 1.0) <= tol_ratio

# ---------- Extraction ----------
def extract_shapes_from_array(img: np.ndarray, min_area_ratio: float = 0.002) -> Dict:
    """
    抽取形狀、交疊，以及每個形狀質心（用於幾何關係比較）
    回傳：
      - size: (H, W)
      - shapes: [ {type, orientation, area, area_ratio, aspect, poly, centroid:(nx,ny), n_verts} ... ]
      - overlaps: n×n 交疊矩陣（交疊面積 / 較小形狀面積）
    """
    H, W = img.shape[:2]
    th = _binarize_arr(img)
    contours, _ = cv2.findContours(th, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)

    shapes = []
    total_area = float(H * W)

    for cnt in contours:
        area = cv2.contourArea(cnt)
        if area / total_area < min_area_ratio:
            continue
        epsilon = 0.01 * cv2.arcLength(cnt, True)
        approx = cv2.approxPolyDP(cnt, epsilon, True)
        n = len(approx)
        c = _contour_centroid(cnt)

        if n == 3:
            shape_type = "triangle"
            orientation = _triangle_orientation(approx, c)
        elif n == 4:
            shape_type = "square" if _rect_is_square(approx) else "rectangle"
            orientation = None
        else:
            shape_type = f"poly{n}"
            orientation = None

        shapes.append({
            "type": shape_type,
            "orientation": orientation,
            "area": float(area),
            "area_ratio": float(area) / total_area,
            "aspect": _bbox_aspect_ratio(approx),
            "poly": approx.reshape(-1, 2).astype(int),
            "centroid": (c[0] / W, c[1] / H),   # 正規化質心
            "n_verts": int(n)
        })

    # 交疊矩陣
    n = len(shapes)
    overlaps = np.zeros((n, n), dtype=np.float32)
    masks = []
    for s in shapes:
        mask = np.zeros((H, W), dtype=np.uint8)
        cv2.fillPoly(mask, [s["poly"].astype(np.int32)], 255)
        masks.append(mask)
    for i in range(n):
        for j in range(i+1, n):
            inter = cv2.bitwise_and(masks[i], masks[j])
            inter_area = float(np.count_nonzero(inter))
            denom = max(1.0, min(shapes[i]["area"], shapes[j]["area"]))
            overlaps[i, j] = overlaps[j, i] = inter_area / denom

    return {"size": (H, W), "shapes": shapes, "overlaps": overlaps}

# ---------- Matching & cost ----------
def _shape_cost(a: Dict, b: Dict) -> float:
    # 類型/朝向/面積比例/外框比例 + 頂點數一致性
    same_poly_family = a["type"].startswith("poly") and b["type"].startswith("poly")
    type_mismatch = 0.0 if (a["type"] == b["type"] or same_poly_family) else 1.0

    orient_mismatch = 0.0
    if a["type"] == "triangle" and b["type"] == "triangle":
        orient_mismatch = 0.0 if a.get("orientation") == b.get("orientation") else 1.0

    area_diff = abs(a["area_ratio"] - b["area_ratio"]) / max(1e-6, b["area_ratio"])
    aspect_diff = abs(a["aspect"] - b["aspect"]) / max(1e-6, b["aspect"])
    verts_diff = abs(a["n_verts"] - b["n_verts"]) / max(1.0, b["n_verts"])

    # 加重權重：類型/朝向最重要
    w_type, w_orient, w_area, w_aspect, w_verts = 4.0, 3.0, 1.5, 1.0, 1.0
    return (w_type * type_mismatch +
            w_orient * orient_mismatch +
            w_area * area_diff +
            w_aspect * aspect_diff +
            w_verts * verts_diff)

def _greedy_match(A: List[Dict], B: List[Dict]):
    pairs, usedA, usedB = [], set(), set()
    costs = [(_shape_cost(a,b), i, j) for i,a in enumerate(A) for j,b in enumerate(B)]
    for cost, i, j in sorted(costs, key=lambda x: x[0]):
        if i not in usedA and j not in usedB:
            usedA.add(i); usedB.add(j); pairs.append((i,j))
    return pairs

# ---------- Geometric/overlap structure ----------
def _centroid_pair_cost(sig_u: Dict, sig_t: Dict, pairs: List[Tuple[int,int]]) -> float:
    """比較成對形狀間質心距離（正規化）結構"""
    A, B = sig_u["shapes"], sig_t["shapes"]
    if len(pairs) < 2: return 0.0
    cost = 0.0
    for idx1 in range(len(pairs)):
        for idx2 in range(idx1+1, len(pairs)):
            i1, j1 = pairs[idx1]
            i2, j2 = pairs[idx2]
            cu1 = np.array(A[i1]["centroid"]); cu2 = np.array(A[i2]["centroid"])
            ct1 = np.array(B[j1]["centroid"]); ct2 = np.array(B[j2]["centroid"])
            du = float(np.linalg.norm(cu1 - cu2))
            dt = float(np.linalg.norm(ct1 - ct2))
            cost += abs(du - dt)  # 距離結構差
    return cost

def _overlap_cost(sig_u: Dict, sig_t: Dict, pairs: List[Tuple[int,int]]) -> Tuple[float,int]:
    """連續值差 + 二元圖樣（是否重疊）的不一致筆數"""
    Ou, Ot = sig_u["overlaps"], sig_t["overlaps"]
    cont_cost = 0.0
    bin_mismatch = 0
    TH = 0.25  # 視為「有重疊」的門檻
    for x in range(len(pairs)):
        for y in range(x+1, len(pairs)):
            i1, j1 = pairs[x]; i2, j2 = pairs[y]
            u = float(Ou[i1, i2]); t = float(Ot[j1, j2])
            cont_cost += abs(u - t)
            if (u > TH) != (t > TH):
                bin_mismatch += 1
    return cont_cost, bin_mismatch

# ---------- Scoring ----------
def score_signatures(sig_user: Dict, sig_target: Dict) -> Dict:
    A, B = sig_user["shapes"], sig_target["shapes"]
    pairs = _greedy_match(A, B)

    # 1) 數量差：加重
    miss = abs(len(A) - len(B))
    miss_penalty = 6.0 * miss

    # 2) 單形狀成本
    per_shape_cost = sum(_shape_cost(A[i], B[j]) for i, j in pairs)

    # 3) 幾何關係（質心距離結構）
    centroid_cost = _centroid_pair_cost(sig_user, sig_target, pairs)

    # 4) 交疊（連續）+ 圖樣（二元）
    ov_cont, ov_bin_mis = _overlap_cost(sig_user, sig_target, pairs)

    # 5) 目標若有明顯重疊而使用者幾乎沒有 → 直接門檻懲罰
    target_has_overlap = False
    if len(B) >= 2:
        for x in range(len(B)):
            for y in range(x+1, len(B)):
                if sig_target["overlaps"][x, y] > 0.25:
                    target_has_overlap = True
    max_user_overlap = float(np.max(sig_user["overlaps"])) if len(A) >= 2 and sig_user["overlaps"].size else 0.0
    strong_overlap_gate_penalty = 10.0 if (target_has_overlap and max_user_overlap < 0.15) else 0.0

    # 權重（可微調）
    W_OV_CONT = 6.0
    W_OV_BIN  = 3.0
    W_CENT    = 6.0
    total_cost = (miss_penalty +
                  per_shape_cost +
                  W_CENT * centroid_cost +
                  W_OV_CONT * ov_cont +
                  W_OV_BIN * ov_bin_mis +
                  strong_overlap_gate_penalty)

    score = max(0.0, 100.0 - 15.0 * total_cost)
    return {
        "pairs": pairs,
        "miss_penalty": miss_penalty,
        "per_shape_cost": per_shape_cost,
        "centroid_cost": centroid_cost,
        "overlap_cont_cost": ov_cont,
        "overlap_pattern_mismatch": ov_bin_mis,
        "strong_overlap_gate_penalty": strong_overlap_gate_penalty,
        "total_cost": total_cost,
        "score": float(score)
    }

def signature_from_path(path: str) -> Dict:
    img = cv2.imread(path)
    if img is None:
        raise FileNotFoundError(path)
    return extract_shapes_from_array(img)
