#pip install faster-whisper
#pip install ffmpeg-python
#pip install flask pypinyin faster-whisper opencc-python-reimplemented
from flask import Flask, request, jsonify
from pypinyin import lazy_pinyin
from difflib import SequenceMatcher
import os
from datetime import datetime
import json
from faster_whisper import WhisperModel
from opencc import OpenCC
import re

cc = OpenCC("s2twp")  # 統一轉繁體（台灣用詞）

def _normalize(s: str) -> str:
    if not s:
        return ""
    s = re.sub(r"\s+", "", s)                          # 去空白
    s = re.sub(r"[^\w\u4e00-\u9fff]", "", s)           # 去標點
    return s.lower()

def calc_similarity(target_text: str, spoken_text: str) -> float:
    """
    將 target 與辨識結果都做繁體轉換與正規化後，用 SequenceMatcher 計算相似度
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

app = Flask(__name__)

UPLOAD_FOLDER = "uploads"
RESULT_FOLDER = "results"
os.makedirs(UPLOAD_FOLDER, exist_ok=True)
os.makedirs(RESULT_FOLDER, exist_ok=True)

TARGET_PASS_THRESHOLD = 0.7  # 70%

# 1. 初始化本地 Whisper 模型（第一次會下載模型檔）
# 可選 "tiny", "base", "small", "medium", "large-v3"
model_size = "small"
model = WhisperModel(model_size, device="cpu", compute_type="int8")

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

    # 2. 本地 Whisper 語音辨識
    try:
        segments, _ = model.transcribe(filepath, beam_size=5)
        spoken_text = "".join([segment.text for segment in segments]).strip()
    except Exception as e:
        return jsonify({"error": f"Speech recognition failed: {e}"}), 500

    # 計算相似度
    similarity = calc_similarity(target, spoken_text)
    passed = similarity >= TARGET_PASS_THRESHOLD

    result = {
        "filename": filename,
        "spoken_text": spoken_text,
        "accuracy": round(similarity * 100, 2),
        "passed": passed
    }

    # 存成 JSON 檔案
    result_filename = datetime.now().strftime("%Y%m%d_%H%M%S") + ".json"
    result_path = os.path.join(RESULT_FOLDER, result_filename)
    with open(result_path, "w", encoding="utf-8") as f:
        json.dump(result, f, ensure_ascii=False, indent=2)

    return jsonify(result)

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=True)
