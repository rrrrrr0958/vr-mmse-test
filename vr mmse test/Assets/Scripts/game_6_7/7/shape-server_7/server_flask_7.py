# server_flask.py
from flask import Flask, request, jsonify
import numpy as np
from PIL import Image
import io, time
import shapesig_7 as S

app = Flask(__name__)

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
        res   = S.score_one(user_rgb, t_rgb, cfg)
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

if __name__ == "__main__":
    # python3 server_flask.py
    app.run(host="127.0.0.1", port=5000, debug=True)
