# shapesig.py
import io, math, numpy as np, cv2
from PIL import Image

# ---------- I/O ----------
def read_rgb_from_bytes(b):
    return np.array(Image.open(io.BytesIO(b)).convert("RGB"))

# ---------- 基礎 ----------
def to_edges(img_rgb, mode="edges"):
    g = cv2.cvtColor(img_rgb, cv2.COLOR_RGB2GRAY)
    if mode == "binary":
        inv = 255 - g
        _, b = cv2.threshold(inv, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
        k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3))
        return cv2.morphologyEx(b, cv2.MORPH_CLOSE, k, 1)
    v = np.median(g); lo = int(max(0, 0.66 * v)); hi = int(min(255, 1.33 * v))
    return cv2.Canny(g, lo, hi)

def norm_by_bbox_to_canvas(img_rgb, side=512, mode="edges", ref_long=360, margin=6):
    edges = to_edges(img_rgb, mode=mode)
    ys, xs = np.nonzero(edges)
    canvas = np.ones((side, side, 3), np.uint8) * 255
    if len(xs) == 0: return canvas
    x1, x2 = xs.min(), xs.max(); y1, y2 = ys.min(), ys.max()
    y1 = max(0, y1 - margin); y2 = min(edges.shape[0] - 1, y2 + margin)
    x1 = max(0, x1 - margin); x2 = min(edges.shape[1] - 1, x2 + margin)
    crop = img_rgb[y1:y2 + 1, x1:x2 + 1]
    h, w = crop.shape[:2]; r = min(ref_long / max(1, max(h, w)), side / max(1, max(h, w)))
    nh, nw = max(1, int(round(h * r))), max(1, int(round(w * r)))
    small = cv2.resize(crop, (nw, nh), interpolation=cv2.INTER_AREA)
    y0 = (side - nh) // 2; x0 = (side - nw) // 2
    canvas[y0:y0 + nh, x0:x0 + nw] = small
    return canvas

def place_on_canvas(img_rgb, side, scale):
    h, w = img_rgb.shape[:2]
    nh, nw = max(1, int(round(h * scale))), max(1, int(round(w * scale)))
    resized = cv2.resize(img_rgb, (nw, nh), interpolation=cv2.INTER_AREA)
    canvas = np.ones((side, side, 3), np.uint8) * 255
    y0 = (side - nh) // 2; x0 = (side - nw) // 2
    y1 = min(side, y0 + nh); x1 = min(side, x0 + nw)
    canvas[max(0, y0):y1, max(0, x0):x1] = resized[:(y1 - max(0, y0)), :(x1 - max(0, x0))]
    return canvas

# ---------- Chamfer ----------
def chamfer_mean(A_edges, B_edges, tau_px=8):
    src = (B_edges == 0).astype(np.uint8)  # 邊=0
    dt = cv2.distanceTransform(src, cv2.DIST_L2, 5)
    ys, xs = np.nonzero(A_edges)
    if len(xs) == 0: return float(tau_px)
    d = dt[ys, xs]; d = np.minimum(d, tau_px)
    return float(np.mean(d))

def bi_chamfer_score(imgA_rgb, imgB_rgb, tau_px=8, mode="edges"):
    A = to_edges(imgA_rgb, mode=mode); B = to_edges(imgB_rgb, mode=mode)
    d_ab = chamfer_mean(A, B, tau_px); d_ba = chamfer_mean(B, A, tau_px)
    avg = 0.5 * (d_ab + d_ba)
    score = max(0.0, min(100.0, (1.0 - avg / float(tau_px)) * 100.0))
    return score, {"d_ab": d_ab, "d_ba": d_ba, "avg_d": avg}

def best_scale_bichamfer(imgA_rgb, imgB_rgb, tau_px=8, mode="edges",
                         side=512, scales=None):
    if scales is None: scales = np.linspace(0.85, 1.20, 8)
    A0 = norm_by_bbox_to_canvas(imgA_rgb, side=side, mode=mode)
    B0 = norm_by_bbox_to_canvas(imgB_rgb, side=side, mode=mode)
    best = {"score": -1.0, "avg_d": 1e9, "scale": 1.0,
            "d_ab": None, "d_ba": None, "A_best": None, "B": B0}
    for s in scales:
        A_can = place_on_canvas(A0, side, s)
        sc, det = bi_chamfer_score(A_can, B0, tau_px=tau_px, mode=mode)
        if det["avg_d"] < best["avg_d"]:
            best.update({"score": sc, "avg_d": det["avg_d"], "scale": float(s),
                         "d_ab": det["d_ab"], "d_ba": det["d_ba"], "A_best": A_can})
    return best

# ---------- 產生「填滿的筆畫」並切半、取交集 ----------
def filled_strokes(img_rgb, close_ks=7, dilate_ks=7):
    g = cv2.cvtColor(img_rgb, cv2.COLOR_RGB2GRAY)
    inv = 255 - g
    _, b = cv2.threshold(inv, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
    kC = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (close_ks, close_ks))
    kD = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (dilate_ks, dilate_ks))
    b = cv2.morphologyEx(b, cv2.MORPH_CLOSE, kC, 1)
    b = cv2.dilate(b, kD, 1)
    # 只留最大連通塊
    n, lab = cv2.connectedComponents(b)
    if n > 1:
        areas = [(lab == i).sum() for i in range(1, n)]
        i = 1 + int(np.argmax(areas))
        b = np.where(lab == i, 255, 0).astype(np.uint8)
    return b

def waist_split_and_intersection(mask, margin=4):
    H, W = mask.shape
    rows = (mask > 0).sum(axis=1).astype(np.float32)
    rows = cv2.GaussianBlur(rows.reshape(-1, 1), (1, 9), 0).ravel()
    ylo, yhi = int(0.3 * H), int(0.7 * H)
    if yhi <= ylo or np.all(rows[ylo:yhi] == 0): return None, None, None, 0
    y_star = ylo + int(np.argmin(rows[ylo:yhi]))
    top = mask.copy(); top[min(H, y_star + margin):, :] = 0
    bot = mask.copy(); bot[:max(0, y_star - margin), :] = 0
    inter = ((top > 0) & (bot > 0)).astype(np.uint8) * 255
    a1 = np.count_nonzero(top); a2 = np.count_nonzero(bot)
    return top, bot, inter, max(1, min(a1, a2))

def biggest_contour(bin_mask):
    cnts, _ = cv2.findContours(bin_mask, cv2.RETR_EXTERNAL, cv2.CHAIN_APPROX_SIMPLE)
    if not cnts: return None
    return max(cnts, key=cv2.contourArea)

# ---------- 菱形分 ----------
def diamond_score(user_rgb, target_rgb, side=512,
                  close_ks=7, dilate_ks=7, margin=4,
                  area_min_ratio=0.05, hu_tau=0.35, quad_bonus=0.15):
    U = filled_strokes(user_rgb, close_ks, dilate_ks)
    T = filled_strokes(target_rgb, close_ks, dilate_ks)

    utop, ubot, uint, ubase = waist_split_and_intersection(U, margin)
    ttop, tbot, tint, tbase = waist_split_and_intersection(T, margin)
    if uint is None or tint is None:
        return 0.0, {"area_ratio": 0.0, "hu": 9.9, "quad": 0}

    uarea = np.count_nonzero(uint)
    # 相對手繪 min(上,下) 的占比，避免「幾乎沒交疊」也過關
    r_area = float(uarea / max(1, ubase))
    if r_area < area_min_ratio:
        return 0.0, {"area_ratio": r_area, "hu": 9.9, "quad": 0}

    u_cnt = biggest_contour(uint); t_cnt = biggest_contour(tint)
    if u_cnt is None or t_cnt is None:
        return 0.0, {"area_ratio": r_area, "hu": 9.9, "quad": 0}

    hu = cv2.matchShapes(u_cnt, t_cnt, cv2.CONTOURS_MATCH_I1, 0)
    peri = cv2.arcLength(u_cnt, True)
    approx = cv2.approxPolyDP(u_cnt, 0.02 * peri, True) if peri > 0 else u_cnt
    is_quadish = (4 <= len(approx) <= 6) and cv2.isContourConvex(approx)
    quad = 1 if is_quadish else 0

    s_hu = max(0.0, min(100.0, (1.0 - hu / hu_tau) * 100.0))
    s = s_hu * (1.0 + quad_bonus * quad)
    s = max(0.0, min(100.0, s))
    return s, {"area_ratio": r_area, "hu": float(hu), "quad": int(quad)}

# ---------- 封裝一個總分 ----------
def score_one(user_rgb, target_rgb, cfg):
    # 先把 target / user 都正規化到同尺寸
    tgt = norm_by_bbox_to_canvas(target_rgb, side=cfg["side"], mode=cfg["mode"])
    img = norm_by_bbox_to_canvas(user_rgb,    side=cfg["side"], mode=cfg["mode"])
    scales = np.linspace(cfg["scan_from"], cfg["scan_to"], cfg["scan_n"])

    best = best_scale_bichamfer(img, tgt, tau_px=cfg["tau"], mode=cfg["mode"],
                                side=cfg["side"], scales=scales)
    chamfer = best["score"]

    ds, dmeta = diamond_score(img, tgt, side=cfg["side"],
                              close_ks=cfg["close"], dilate_ks=cfg["dilate"], margin=cfg["margin"],
                              area_min_ratio=cfg["area_min_ratio"], hu_tau=cfg["hu_tau"],
                              quad_bonus=cfg["quad_bonus"])

    if cfg.get("mode_blend", False):
        final = (1.0 - cfg["w_diamond"]) * chamfer + cfg["w_diamond"] * ds
    else:
        # ★ 你指定的規則：如果菱形分 < dia_min，就 final = 0.6 × Chamfer
        dia_min = cfg.get("dia_min", 30.0)
        low_factor = cfg.get("dia_low_factor", 0.6)
        final = chamfer * (low_factor if ds < dia_min else 1.0)

    final = float(max(0.0, min(100.0, final)))
    return {
        "score": final,
        "details": {
            "chamfer": float(chamfer),
            "diamond": float(ds),
            "area_ratio": float(dmeta["area_ratio"]),
            "hu": float(dmeta["hu"]),
            "quad": int(dmeta["quad"]),
            "avg_d": float(best["avg_d"]),
            "d_ab": float(best["d_ab"]),
            "d_ba": float(best["d_ba"]),
            "best_scale": float(best["scale"])
        }
    }
