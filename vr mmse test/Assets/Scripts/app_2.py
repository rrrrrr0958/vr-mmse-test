from flask import Flask, request, jsonify
from pypinyin import lazy_pinyin
from difflib import SequenceMatcher
import os
from datetime import datetime
import openai  # 確保你已經 pip install openai

app = Flask(__name__)

UPLOAD_FOLDER = "uploads"
os.makedirs(UPLOAD_FOLDER, exist_ok=True)

TARGET_PASS_THRESHOLD = 0.7  # 70%

openai.api_key = os.getenv("OPENAI_API_KEY")  # 你的 API key

def calc_similarity(target, spoken):
    """將文字轉成拼音後比較相似度"""
    target_pinyin = lazy_pinyin(target)
    spoken_pinyin = lazy_pinyin(spoken)
    ratio = SequenceMatcher(None, target_pinyin, spoken_pinyin).ratio()
    return ratio

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

    # Whisper 語音辨識
    try:
        with open(filepath, "rb") as audio_file:
            transcript = openai.Audio.transcriptions.create(
                model="whisper-1",  # 或者你自己本地模型
                file=audio_file
            )
        spoken_text = transcript.text.strip()
    except Exception as e:
        return jsonify({"error": str(e)}), 500

    # 計算相似度
    similarity = calc_similarity(target, spoken_text)
    passed = similarity >= TARGET_PASS_THRESHOLD

    result = {
        "filename": filename,
        "spoken_text": spoken_text,
        "accuracy": round(similarity * 100, 2),
        "passed": passed
    }

    # TODO: 存資料庫
    print("儲存資料到 DB:", result)

    return jsonify(result)

if __name__ == "__main__":
    app.run(host="0.0.0.0", port=5000, debug=True)
