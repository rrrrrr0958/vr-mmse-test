from flask import Flask, request, jsonify
# Server 1 导入
from PIL import Image
import time
import io, math, numpy as np, cv2
# Server 2 导入
import os
import pandas as pd
import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation
from datetime import datetime
from threading import Thread

# --- Flask 实例 ---
app = Flask(__name__)

# --- Server 2 全局变量 ---
SAVE_FOLDER = "results"
os.makedirs(SAVE_FOLDER, exist_ok=True)
# 也可以把一些 Server 1 的默认参数移到这里

# ===================================================
# Server 1: 图像评分相关函数 (复制粘贴)
# ===================================================
def _f(name, default=None, cast=str):
    v = request.form.get(name, None)
    if v is None or v == "": return default
    try:
        return cast(v)
    except Exception:
        return default

@app.route("/score", methods=["POST"])
def score_drawing():
    # ---- 讀圖 ----
    if 'user' not in request.files:
        return jsonify({"error": "missing file 'user'"}), 400
    user_b = request.files['user'].read()
    user_rgb = np.array(Image.open(io.BytesIO(user_b)).convert("RGB"))

    target_files = request.files.getlist("targets")
    if not target_files:
        return jsonify({"error": "no 'targets' uploaded"}), 400

    # ---- 參數（你的預設：mode=binary, tau=8, side=128, scan 0.85~1.25×11）----
    cfg = {
        "mode":       _f("mode", "binary", str).lower(),
        "tau":        _f("tau", 8.0, float),
        "side":       _f("side", 128, int),
        "scan_from":  _f("scan_from", 0.85, float),
        "scan_to":    _f("scan_to", 1.25, float),
        "scan_n":     _f("scan_n", 11, int),

        # 菱形檢測（可保留預設）
        "area_min_ratio": _f("area_min_ratio", 0.05, float),
        "hu_tau":         _f("hu_tau", 0.35, float),
        "quad_bonus":     _f("quad_bonus", 0.15, float),
        "close":          _f("close", 7, int),
        "dilate":         _f("dilate", 7, int),
        "margin":         _f("margin", 4, int),

        # 融合開關/比重（預設用懲罰，不啟動融合）
        "mode_blend":  _f("mode_blend", "false", str) == "true",
        "w_diamond":   _f("w_diamond", 0.35, float),

        # ★ 你指定的規則參數
        "dia_min":        _f("dia_min", 30.0, float),
        "dia_low_factor": _f("dia_low_factor", 0.6, float),
    }

    results = []
    best_idx = 0
    best_score = -1.0

    for idx, tf in enumerate(target_files):
        t_rgb = np.array(Image.open(io.BytesIO(tf.read())).convert("RGB"))
        res   = score_one(user_rgb, t_rgb, cfg)
        results.append({
            "index": idx,
            "name": tf.filename,
            "score": res["score"],
            "details": res["details"]
        })
        if res["score"] > best_score:
            best_score = res["score"]; best_idx = idx

    return jsonify({
        "best_index": best_idx,
        "best_score": float(best_score),
        "results": results
    })

@app.route("/health")
def health():
    return jsonify({"ok": True, "t": time.time()})



# =========================
# I/O
# =========================
def read_rgb_from_bytes(b):
    return np.array(Image.open(io.BytesIO(b)).convert("RGB"))

# =========================
# 基礎
# =========================
def to_edges(img_rgb, mode="edges"):
    """回傳二值邊緣影像（白底、邊為>0）。"""
    g = cv2.cvtColor(img_rgb, cv2.COLOR_RGB2GRAY)
    if mode == "binary":
        inv = 255 - g
        _, b = cv2.threshold(inv, 0, 255, cv2.THRESH_BINARY + cv2.THRESH_OTSU)
        k = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (3, 3))
        return cv2.morphologyEx(b, cv2.MORPH_CLOSE, k, 1)
    v = np.median(g); lo = int(max(0, 0.66 * v)); hi = int(min(255, 1.33 * v))
    e = cv2.Canny(g, lo, hi)
    return e

def norm_by_bbox_to_canvas(img_rgb, side=512, mode="edges", ref_long=360, margin=6):
    """把目標區域裁切縮放到方形畫布中央，避免空白干擾。"""
    edges = to_edges(img_rgb, mode=mode)
    ys, xs = np.nonzero(edges)
    canvas = np.ones((side, side, 3), np.uint8) * 255
    if len(xs) == 0: 
        return canvas
    x1, x2 = xs.min(), xs.max(); y1, y2 = ys.min(), ys.max()
    y1 = max(0, y1 - margin); y2 = min(edges.shape[0] - 1, y2 + margin)
    x1 = max(0, x1 - margin); x2 = min(edges.shape[1] - 1, x2 + margin)
    crop = img_rgb[y1:y2 + 1, x1:x2 + 1]
    h, w = crop.shape[:2]
    r = min(ref_long / max(1, max(h, w)), side / max(1, max(h, w)))
    nh, nw = max(1, int(round(h * r))), max(1, int(round(w * r)))
    small = cv2.resize(crop, (nw, nh), interpolation=cv2.INTER_AREA)
    y0 = (side - nh) // 2; x0 = (side - nw) // 2
    canvas[y0:y0 + nh, x0:x0 + nw] = small
    return canvas

def place_on_canvas(img_rgb, side, scale):
    """把圖以 scale 縮放後置中到 side×side 畫布。"""
    h, w = img_rgb.shape[:2]
    nh, nw = max(1, int(round(h * scale))), max(1, int(round(w * scale)))
    resized = cv2.resize(img_rgb, (nw, nh), interpolation=cv2.INTER_AREA)
    canvas = np.ones((side, side, 3), np.uint8) * 255
    y0 = (side - nh) // 2; x0 = (side - nw) // 2
    y1 = min(side, y0 + nh); x1 = min(side, x0 + nw)
    canvas[max(0, y0):y1, max(0, x0):x1] = resized[:(y1 - max(0, y0)), :(x1 - max(0, x0))]
    return canvas

# =========================
# Chamfer
# =========================
def chamfer_mean(A_edges, B_edges, tau_px=8):
    src = (B_edges == 0).astype(np.uint8)  # 邊=0
    dt = cv2.distanceTransform(src, cv2.DIST_L2, 5)
    ys, xs = np.nonzero(A_edges)
    if len(xs) == 0: 
        return float(tau_px)
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
    if scales is None: 
        scales = np.linspace(0.85, 1.20, 8)
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

# =========================
# 取腰部位置（由 target 決定）
# =========================
def filled_strokes(img_rgb, close_ks=7, dilate_ks=7):
    """把線稿轉成實心粗筆畫（用於偵測腰部位置）。"""
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

def estimate_waist_band_from_target(target_rgb, band_frac=0.20,
                                    close_ks=7, dilate_ks=7):
    """
    由 target 圖估計「腰部水平位置」：
    - 在 30%~70% 高度間找最窄列(y_star)
    - 回傳 (y0, y1) 作為腰部視窗
    """
    T = filled_strokes(target_rgb, close_ks=close_ks, dilate_ks=dilate_ks)
    H, W = T.shape
    rows = (T > 0).sum(axis=1).astype(np.float32)
    rows = cv2.GaussianBlur(rows.reshape(-1, 1), (1, 9), 0).ravel()
    ylo, yhi = int(0.30 * H), int(0.70 * H)
    if yhi <= ylo or np.all(rows[ylo:yhi] == 0):
        # fallback：中段居中
        y_star = H // 2
    else:
        y_star = ylo + int(np.argmin(rows[ylo:yhi]))
    band_h = max(8, int(round(band_frac * H)))
    y0 = max(0, y_star - band_h // 2)
    y1 = min(H, y_star + (band_h - band_h // 2))
    return y0, y1, int(y_star)

# =========================
# 洞（hole）檢測：是否有「相交」形成封閉環
# =========================
def _poly_angles_deg(pts):
    def ang(a,b,c):
        ba = a - b; bc = c - b
        cosang = float(np.dot(ba, bc)) / (np.linalg.norm(ba)*np.linalg.norm(bc) + 1e-6)
        return float(np.degrees(np.arccos(np.clip(cosang, -1.0, 1.0))))
    n = len(pts)
    return [ang(pts[(i-1)%n], pts[i], pts[(i+1)%n]) for i in range(n)]

def diamond_exists_score(user_rgb, target_rgb, side=512, mode="edges",
                         # --- 預處理 ---
                         dilate_ks=3, close_ks=3,
                         # --- 腰帶設定 ---
                         band_frac=0.20,                 # 腰部視窗高度比例（相對 H）
                         # --- 洞的面積篩選（相對整張影像）---
                         hole_area_min_frac=0.0015,
                         hole_area_max_frac=0.06,
                         # --- 形狀與位置約束 ---
                         flat_min=0.16,                  # 最小外接矩形短/長比；太扁則捨去
                         angle_tol=25.0,                 # 對角互補的容忍度
                         center_weight=0.40,             # 洞中心靠近腰線的權重
                         shape_weight=0.60,              # 幾何形狀（角度/扁平）的權重
                         prefer_quad_bonus=0.10):        # 近似四邊形加分
    """
    回傳:
      s_exist: 0~100 相交存在性的分數（存在且像樣越高）
      meta: 診斷資訊（是否找到洞、最佳洞資訊等）
    方法：
      1) 以 target 推定腰部視窗
      2) 對 user 做輕微膨脹/封孔，確保線帶閉合
      3) 用 RETR_CCOMP/TREE 找到「洞」(child contours)
      4) 在腰部視窗內尋洞，綜合中心距離、扁平度、角度互補給分
    """
    # 0) 正規化
    tgt = norm_by_bbox_to_canvas(target_rgb, side=side, mode=mode)
    usr = norm_by_bbox_to_canvas(user_rgb,    side=side, mode=mode)
    H, W = side, side

    # 1) 估腰帶
    y0, y1, y_star = estimate_waist_band_from_target(tgt, band_frac=band_frac)

    # 2) 邊緣→線帶
    E = to_edges(usr, mode=mode)                     # 0/255
    kD = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (dilate_ks, dilate_ks))
    kC = cv2.getStructuringElement(cv2.MORPH_ELLIPSE, (close_ks, close_ks))
    B = cv2.dilate(E, kD, 1)
    B = cv2.morphologyEx(B, cv2.MORPH_CLOSE, kC, 1)
    # 二值化（確保 findContours 可用）
    _, B = cv2.threshold(B, 0, 255, cv2.THRESH_BINARY)

    # 3) 找輪廓 + 階層
    #   RETR_CCOMP：同一層級的外輪廓，其子輪廓為洞
    cnts, hier = cv2.findContours(B, cv2.RETR_CCOMP, cv2.CHAIN_APPROX_SIMPLE)
    if hier is None or len(cnts) == 0:
        return 0.0, {
            "found": False, "reason": "no_contours",
            "waist_band": [int(y0), int(y1)], "best": None
        }

    total_area = float(H * W)
    cx_mid = W // 2

    best = {
        "score": 0.0,
        "found": False,
        "waist_band": [int(y0), int(y1)],
        "hole_bbox": None,
        "hole_area_frac": 0.0,
        "flat": 0.0,
        "angles": None,
        "quad": 0,
        "center_dist_px": None
    }

    # 4) 掃描所有「洞」
    for i, h in enumerate(hier[0]):  # h: [next, prev, first_child, parent]
        parent = i
        child = h[2]
        while child != -1:
            cnt = cnts[child]
            area = cv2.contourArea(cnt)
            area_frac = area / total_area

            # --- 面積權重（軟式）：在 [min,max] 內給 1；外側逐步衰減到 ~0.3 ---
            amin = hole_area_min_frac; amax = hole_area_max_frac
            if area_frac <= 0:
                area_w = 0.0
            elif area_frac < amin:
                area_w = max(0.3, float(area_frac / max(1e-6, amin)))  # 小也給低分，不丟掉
            elif area_frac > amax:
                area_w = max(0.3, float(amax / area_frac))             # 大也給低分，不丟掉
            else:
                area_w = 1.0

            x, y, w, h_rect = cv2.boundingRect(cnt)
            cy = y + h_rect // 2

            # --- 腰帶權重（軟式）：沒重疊也給 0.2 的殘留分 ---
            overlap = max(0, min(y + h_rect, y1) - max(y, y0))
            band_overlap = overlap / max(1, (y1 - y0))  # 0~1
            band_w = 0.2 + 0.8 * float(np.clip(band_overlap * 1.2, 0.0, 1.0))  # 放寬

            # --- 近似多邊形與凸性（軟式）---
            peri = cv2.arcLength(cnt, True)
            approx = cv2.approxPolyDP(cnt, 0.03 * peri, True) if peri > 0 else cnt  # 放鬆 0.02→0.03
            verts = len(approx)
            verts_w = 1.0 if 4 <= verts <= 6 else (0.7 if 3 <= verts <= 8 else 0.4)
            convex_w = 1.0 if cv2.isContourConvex(approx) else 0.85

            pts = approx[:, 0, :].astype(np.float32)
            # 角度互補（軟式）
            angs = _poly_angles_deg(pts) if len(pts) >= 4 else []
            if len(angs) >= 4:
                e0 = abs((angs[0] + angs[2]) - 180.0)
                e1 = abs((angs[1] + angs[3]) - 180.0)
                # 容忍度放寬：angle_tol 內=1；到 2*angle_tol 緩降到 0
                tol = angle_tol
                ang0 = max(0.0, 1.0 - e0 / max(1e-6, 2*tol))
                ang1 = max(0.0, 1.0 - e1 / max(1e-6, 2*tol))
                ang_score = 0.5 * (ang0 + ang1)
            else:
                ang_score = 0.5  # 頂點數太少也給半分

            # 扁平度（軟式）
            rect = cv2.minAreaRect(cnt)
            w_minrect, h_minrect = rect[1] if rect[1] != (0.0,0.0) else (1.0,1.0)
            flat = min(w_minrect, h_minrect) / max(w_minrect, h_minrect)
            flat_score = float(np.clip((flat - flat_min) / max(1e-6, (1.0 - flat_min)), 0.0, 1.0))

            # 位置（靠近畫面中心與腰線）
            M = cv2.moments(cnt)
            if abs(M["m00"]) < 1e-6:
                cx, cy = x + w//2, y + h_rect//2
            else:
                cx = int(M["m10"]/M["m00"]); cy = int(M["m01"]/M["m00"])
            dist_y = abs(cy - y_star); dist_x = abs(cx - cx_mid)
            y_span = max(1, (y1 - y0) // 2)
            y_score = max(0.0, 1.0 - dist_y / (y_span * 1.5))
            x_score = max(0.0, 1.0 - dist_x / (W * 0.25))
            center_score = 0.5 * (y_score + x_score)

            # 幾何形狀綜合（軟式）+ 四邊形加分
            geom = 0.5 * ang_score + 0.5 * flat_score
            if 4 <= verts <= 6:
                geom *= (1.0 + prefer_quad_bonus)

            # 最終存在分（全部是連續權重，沒有硬丟棄）
            s = 100.0 * (
                band_w *                               # 腰帶權重（放寬）
                area_w *                               # 面積權重（放寬）
                convex_w * verts_w *                   # 形狀權重（放寬）
                (center_weight * center_score + shape_weight * geom)
            )

            if s > best["score"]:
                best.update({
                    "score": float(s),
                    "found": True,
                    "waist_band": [int(y0), int(y1)],
                    "hole_bbox": [int(x), int(y), int(w), int(h_rect)],
                    "hole_area_frac": float(area_frac),
                    "flat": float(flat),
                    "angles": [float(a) for a in (angs[:4] if len(angs)>=4 else [])],
                    "quad": 1 if 4 <= verts <= 6 else 0,
                    "center_dist_px": [int(dist_x), int(dist_y)]
                })

            child = hier[0][child][0]


    return float(best["score"]), best

# =========================
# 總分封裝
# =========================
def score_one(user_rgb, target_rgb, cfg):
    """
    統一流程：
      1) 規格化
      2) 掃scale取最好的雙向Chamfer（外形）
      3) 洞檢測（相交與否，腰帶視窗內）
      4) 融合規則
    cfg 重要參數：
      - side, mode, tau, scan_from, scan_to, scan_n
      - chamfer_weight  : 當有相交時，final = cw*Chamfer + (1-cw)*DiamondExist
      - no_diamond_factor: 當無相交時，final = Chamfer * no_diamond_factor（可設 0~1）
    """
    # 1) 正規化（為了與 Chamfer 的最佳縮放一致）
    tgt = norm_by_bbox_to_canvas(target_rgb, side=cfg["side"], mode=cfg["mode"])
    img = norm_by_bbox_to_canvas(user_rgb,    side=cfg["side"], mode=cfg["mode"])
    scales = np.linspace(cfg["scan_from"], cfg["scan_to"], cfg["scan_n"])

    # 2) 最佳 scale 的 Bi-Chamfer
    best = best_scale_bichamfer(img, tgt, tau_px=cfg["tau"], mode=cfg["mode"],
                                side=cfg["side"], scales=scales)
    chamfer = float(best["score"])

    # 3) 洞檢測（直接用未縮放前的 img/tgt 亦可；這裡用規格化版本）
    #    這裡不依賴 target 的實心交集，只用 target 估腰帶位置
    s_exist, dx_meta = diamond_exists_score(img, tgt, side=cfg["side"], mode=cfg["mode"],
                                            dilate_ks=cfg.get("hole_dilate", 3),
                                            close_ks=cfg.get("hole_close", 3),
                                            band_frac=cfg.get("waist_band_frac", 0.20),
                                            hole_area_min_frac=cfg.get("hole_area_min_frac", 0.0015),
                                            hole_area_max_frac=cfg.get("hole_area_max_frac", 0.06),
                                            flat_min=cfg.get("flat_min", 0.16),
                                            angle_tol=cfg.get("angle_tol", 25.0),
                                            center_weight=cfg.get("exist_center_w", 0.40),
                                            shape_weight=cfg.get("exist_shape_w", 0.60),
                                            prefer_quad_bonus=cfg.get("exist_quad_bonus", 0.10))

    has_diamond = s_exist >= cfg.get("exist_threshold", 15.0)  # 過低視為不存在（避免噪聲洞）
    chamfer_weight   = cfg.get("chamfer_weight", 0.6)          # 有相交時的加權
    no_diamond_factor= cfg.get("no_diamond_factor", 0.55)      # 無相交時懲罰

    if has_diamond:
        final = chamfer_weight * chamfer + (1.0 - chamfer_weight) * float(s_exist)
    else:
        final = chamfer * no_diamond_factor

    final = float(max(0.0, min(100.0, final)))
    return {
        "score": final,
        "details": {
            "chamfer": float(chamfer),
            "diamond_exist": float(s_exist),
            "has_diamond": bool(has_diamond),
            "avg_d": float(best["avg_d"]),
            "d_ab": float(best["d_ab"]),
            "d_ba": float(best["d_ba"]),
            "best_scale": float(best["scale"]),
            # debug: 洞檢測
            "waist_band": dx_meta.get("waist_band"),
            "hole_bbox": dx_meta.get("hole_bbox"),
            "hole_area_frac": dx_meta.get("hole_area_frac"),
            "flat": dx_meta.get("flat"),
            "angles": dx_meta.get("angles"),
            "center_dist_px": dx_meta.get("center_dist_px")
        }
    }


# ===================================================
# Server 2: CSV 上传/动画生成相关函数 (复制粘贴)
# ===================================================
# === 繪圖與動畫函式 ===
def generate_animation(file_path, file_name):
    try:
        print(f"🎬 Start generating animation for {file_name}")
        df = pd.read_csv(file_path)

        # 檢查是否有至少一隻手的資料
        if not any(df["Type"].isin(["RightHand", "LeftHand"])):
            print(f"⚠️ No valid hand data in {file_name}")
            return

        # 去掉前 120 筆
        if len(df) > 120:
            df = df.iloc[120:].reset_index(drop=True)

        # 對每一隻手分別做位移轉換
        dfs = {}
        for hand in ["RightHand", "LeftHand"]:
            hand_df = df[df["Type"] == hand].copy()
            if len(hand_df) == 0:
                continue
            origin = hand_df.iloc[0][["X", "Y", "Z"]].values
            hand_df["Xp"] = hand_df["X"] - origin[0]
            hand_df["Yp"] = hand_df["Y"] - origin[1]
            dfs[hand] = hand_df

        # === 準備畫圖 ===
        fig, ax = plt.subplots()
        ax.set_xlabel("X (<- left . right ->)")
        ax.set_ylabel("Y (up, down)")
        ax.set_title("Hand Trajectory (Trimmed)")
        lower_name = file_name.lower()
        if "pick" in lower_name or "draw" in lower_name:
            ax.set_xlim(-0.5, 0.5)
            ax.set_ylim(-0.2, 0.8)

        lines = {}
        colors = {"RightHand": "red", "LeftHand": "blue"}
        for hand, hand_df in dfs.items():
            line, = ax.plot([], [], color=colors[hand], label=hand)
            trigger_dot, = ax.plot([], [], "yo", markersize=10)
            lines[hand] = (line, trigger_dot)

        ax.legend()

        max_len = max(len(df_) for df_ in dfs.values())

        def update(frame):
            for hand, hand_df in dfs.items():
                if frame < len(hand_df):
                    data = hand_df[["Xp", "Yp"]].values
                    trigger = hand_df["TriggerPressed"].values
                    line, dot = lines[hand]
                    line.set_data(data[:frame, 0], data[:frame, 1])
                    if trigger[frame] == 1:
                        dot.set_data([data[frame, 0]], [data[frame, 1]])
                    else:
                        dot.set_data([], [])
            return [item for pair in lines.values() for item in pair]

        ani = FuncAnimation(fig, update, frames=max_len, interval=50, blit=True)
        video_path = file_path.replace(".csv", "_trimmed.mp4")

        ani.save(video_path, fps=20, extra_args=["-vcodec", "libx264"])
        plt.close(fig)
        print(f"✅ Animation saved: {video_path}")

    except Exception as e:
        print(f"❌ Error during animation generation: {e}")


@app.route("/upload_csv", methods=["POST"])
def upload_csv():
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    client_file_name = request.headers.get("File-Name", None)
    file_name = client_file_name or f"session_{timestamp}.csv"

    print("📦 Received header File-Name =", file_name)

    file_path = os.path.join(SAVE_FOLDER, file_name)
    csv_data = request.data.decode("utf-8")

    with open(file_path, "w", encoding="utf-8") as f:
        f.write(csv_data)

    # 開新執行緒去處理繪圖，不阻塞 Flask 主執行緒
    Thread(target=generate_animation, args=(file_path, file_name), daemon=True).start()

    return {"message": "CSV received, animation thread started", "file": file_name}, 200


@app.route("/shutdown", methods=["POST"])
def shutdown():
    func = request.environ.get("werkzeug.server.shutdown")
    if func is None:
        raise RuntimeError("Not running with the Werkzeug Server")
    func()
    return {"message": "Server shutting down..."}


if __name__ == "__main__":
    app.run(host="127.0.0.1", port=5002, debug=False, use_reloader=False, threaded=True)
