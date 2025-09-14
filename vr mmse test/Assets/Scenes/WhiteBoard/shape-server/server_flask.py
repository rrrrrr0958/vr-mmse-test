# server_flask.py
from flask import Flask, request, jsonify
import numpy as np
from PIL import Image
import io

from shapesig import extract_shapes_from_array, hybrid_score

app = Flask(__name__)

@app.route('/score', methods=['POST'])
def score_drawing():
    if 'user' not in request.files:
        return jsonify({'error': "missing file field 'user'"}), 400
    if 'targets' not in request.files:
        return jsonify({'error': "no 'targets' uploaded"}), 400

    # 讀使用者圖（保留 RGBA → 交給 shapesig 去處理）
    user_img = Image.open(io.BytesIO(request.files['user'].read()))
    user_np = np.array(user_img)

    # 先抽一次使用者幾何，避免每個 target 重算
    user_shapes = extract_shapes_from_array(user_np)

    results = []
    for idx, tf in enumerate(request.files.getlist('targets')):
        tgt_img = Image.open(io.BytesIO(tf.read()))
        tgt_np = np.array(tgt_img)

        target_shapes = extract_shapes_from_array(tgt_np)
        res = hybrid_score(user_np, tgt_np, user_shapes, target_shapes)

        results.append({
            'target_index': idx,
            'target_name': tf.filename,
            'score': res['score'],
            'details': res['details'],
        })

    overall = int(round(sum(r['score'] for r in results) / len(results))) if results else 0
    return jsonify({'overall_score': overall, 'results': results})

if __name__ == '__main__':
    # 需要套件：pip install pillow opencv-python numpy
    app.run(host='127.0.0.1', port=5000, debug=True)
