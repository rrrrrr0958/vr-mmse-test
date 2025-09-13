from flask import Flask, request, jsonify
import numpy as np
import cv2
from shapesig import extract_shapes_from_array, score_signatures

app = Flask(__name__)

def _decode_upload(file_storage):
    data = np.frombuffer(file_storage.read(), np.uint8)
    img  = cv2.imdecode(data, cv2.IMREAD_UNCHANGED)
    if img is None:
        raise ValueError(f"cannot decode image: {getattr(file_storage, 'filename', 'unknown')}")
    # 若是 BGRA 轉成 BGR
    if img.ndim == 3 and img.shape[2] == 4:
        img = cv2.cvtColor(img, cv2.COLOR_BGRA2BGR)
    return img

@app.route("/score", methods=["POST"])
def score():
    try:
        if 'user' not in request.files:
            return jsonify({"error":"missing file field 'user'"}), 400
        user_img = _decode_upload(request.files['user'])
        sig_user = extract_shapes_from_array(user_img)

        targets = request.files.getlist("targets")
        if not targets:
            return jsonify({"error":"no 'targets' uploaded"}), 400

        results = []
        best_idx, best_score = -1, -1.0

        for idx, tf in enumerate(targets):
            timg = _decode_upload(tf)
            sig_t = extract_shapes_from_array(timg)
            det   = score_signatures(sig_user, sig_t)
            score = float(det["score"])
            results.append({
                "index": idx,
                "name": tf.filename,
                "score": score,
                "details": det
            })
            if score > best_score:
                best_idx, best_score = idx, score

        return jsonify({"best_index": int(best_idx), "best_score": float(best_score), "results": results})
    except Exception as e:
        # 把錯誤回給客戶端，Unity Console 看得到
        return jsonify({"error": str(e)}), 500

if __name__ == "__main__":
    # 開發時可開 debug；正式請關閉
    app.run(host="127.0.0.1", port=5000, debug=True)
