# pip install flask flask-cors pypinyin SpeechRecognition pydub opencc-python-reimplemented
# 需要系統安裝 ffmpeg（pydub 轉檔用）

from flask import Flask, request, jsonify
from flask_cors import CORS
from pypinyin import lazy_pinyin
from difflib import SequenceMatcher
import os
from datetime import datetime
import json
import re
import shutil

import speech_recognition as sr
from pydub import AudioSegment
from opencc import OpenCC

# ---- 初始化 ----
app = Flask(__name__)
CORS(app)  # 允許來自 Unity 的跨域請求

UPLOAD_FOLDER = "uploads"
RESULT_FOLDER = "results"
os.makedirs(UPLOAD_FOLDER, exist_ok=True)
os.makedirs(RESULT_FOLDER, exist_ok=True)

# 相似度通過門檻
TARGET_PASS_THRESHOLD = 0.5  # 50%

# OpenCC 繁簡轉換（台灣用詞）
cc = OpenCC("s2twp")

# 初始化 SpeechRecognition
recognizer = sr.Recognizer()

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

def _ensure_wav(input_path: str) -> str:
    """
    確保輸入音檔為 wav；若不是，使用 pydub 轉成 wav 後回傳新路徑。
    SpeechRecognition 的 sr.AudioFile 對 wav/AIFF/FLAC 友好。
    """
    base, ext = os.path.splitext(input_path)
    ext = ext.lower()
    if ext == ".wav":
        return input_path

    wav_path = base + ".wav"
    try:
        audio = AudioSegment.from_file(input_path)
        audio.export(wav_path, format="wav")
        return wav_path
    except Exception as e:
        # 若轉檔失敗，最後仍嘗試直接用原檔（有些情況原檔也是 wav header 但副檔名不同）
        print(f"[WARN] 轉檔為 WAV 失敗，改用原檔：{e}")
        return input_path

# ---- API ----
@app.route("/transcribe", methods=["POST"])
def transcribe():
    file = request.files.get("file")
    target = request.form.get("target", "").strip()

    if not file:
        return jsonify({"error": "No file uploaded"}), 400

    # 儲存原始上傳音檔（保留存檔與稽核）
    filename = datetime.now().strftime("%Y%m%d_%H%M%S") + os.path.splitext(file.filename or ".wav")[-1]
    filepath = os.path.join(UPLOAD_FOLDER, filename)
    file.save(filepath)

    # 確保有可被 SpeechRecognition 處理的 WAV
    wav_path = _ensure_wav(filepath)

    # 使用 Google Web Speech API 進行辨識（需連網）
    try:
        with sr.AudioFile(wav_path) as source:
            audio_data = recognizer.record(source)  # 讀取整段音訊
            # 語言為繁體中文（台灣）
            spoken_text = recognizer.recognize_google(audio_data, language="zh-TW").strip()
            print(f"[DEBUG] 原始辨識: {spoken_text}")  # 🔍 偵錯輸出
    except sr.UnknownValueError:
        return jsonify({"error": "Speech recognition could not understand audio"}), 400
    except sr.RequestError as e:
        return jsonify({"error": f"Could not request results from service; {e}"}), 500
    except Exception as e:
        return jsonify({"error": f"Speech recognition failed: {e}"}), 500

    # 計算相似度
    similarity = calc_similarity(target, spoken_text)
    passed = similarity >= TARGET_PASS_THRESHOLD

    result = {
        "filename": os.path.basename(wav_path) if os.path.exists(wav_path) else os.path.basename(filepath),
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
    # 與原本相同：對外 0.0.0.0，port=5000
    app.run(host="0.0.0.0", port=5000, debug=True)

# cd Assets\Scripts
