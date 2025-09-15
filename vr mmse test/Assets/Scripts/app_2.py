# pip install flask pypinyin faster-whisper opencc-python-reimplemented ffmpeg-python

from flask import Flask, request, jsonify
from pypinyin import lazy_pinyin
from difflib import SequenceMatcher
import os
from datetime import datetime
import json
from faster_whisper import WhisperModel
from opencc import OpenCC
import re

# ---- 初始化 ----
app = Flask(__name__)

UPLOAD_FOLDER = "uploads"
RESULT_FOLDER = "results"
os.makedirs(UPLOAD_FOLDER, exist_ok=True)
os.makedirs(RESULT_FOLDER, exist_ok=True)

# 相似度通過門檻
TARGET_PASS_THRESHOLD = 0.5  # 50%

# OpenCC 繁簡轉換（台灣用詞）
cc = OpenCC("s2twp")

# 初始化 Whisper 模型
# 可選 tiny, base, small, medium, large-v3
model_size = "small"
model = WhisperModel(model_size, device="cpu", compute_type="int8")

# ---- 工具函數 ----
def _normalize(s: str) -> str:
    if not s:
        return ""
    s = re.sub(r"\s+", "", s)                        # 去掉空白
    s = re.sub(r"[^\w\u4e00-\u9fff]", "", s)         # 去掉標點
    return s.lower()

def calc_similarity(target_text: str, spoken_text: str) -> float:
    """
    轉換繁體並正規化，計算文字相似度
    """
    target_conv = cc.convert(target_text or "")
    spoken_conv = cc.convert(spoken_text or "")

    a = _normalize(target_conv)
    b = _normalize(spoken_conv)

    if not a and not b:
        return 1.0
    if not a or not b:
        return 0.0

    return SequenceMatcher(None, a, b).ratio()

# ---- API ----
@app.route("/transcribe", methods=["POST"])
def transcribe():
    file = request.files.get("file")
    target = request.form.get("target", "").strip()

    if not file:
        return jsonify({"error": "No file uploaded"}), 400

    # 儲存音檔
    filename = datetime.now().strftime("%Y%m%d_%H%M%S") + ".wav"
    filepath = os.path.join(UPLOAD_FOLDER, filename)
    file.save(filepath)

    # Whisper 辨識
    try:
        segments, _ = model.transcribe(filepath, beam_size=5)
        spoken_text = "".join([seg.text for seg in segments]).strip()
        print(f"[DEBUG] 原始辨識: {spoken_text}")  # 🔍 偵錯輸出
    except Exception as e:
        return jsonify({"error": f"Speech recognition failed: {e}"}), 500

    # 計算相似度
    similarity = calc_similarity(target, spoken_text)
    passed = similarity >= TARGET_PASS_THRESHOLD

    result = {
        "filename": filename,
        "spoken_text": cc.convert(spoken_text),  # 確保輸出繁體
        "accuracy": round(similarity * 100, 2),
        "passed": passed
    }

    # 存成 JSON 檔案
    result_filename = datetime.now().strftime("%Y%m%d_%H%M%S") + ".json"
    result_path = os.path.join(RESULT_FOLDER, result_filename)
    with open(result_path, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)

    print(f"[DEBUG] 回傳結果: {result}")  # 🔍 偵錯輸出
    return jsonify(result), 200

# ---- 啟動 ----
if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=True)
#cd Assets\Scripts