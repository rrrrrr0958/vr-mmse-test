from flask import Flask, request, jsonify
from flask_cors import CORS
import os
import io
import speech_recognition as sr
from pypinyin import lazy_pinyin
import Levenshtein
import jieba
import re

# 初始化 Flask
app = Flask(__name__)
CORS(app)

r = sr.Recognizer()

# ===== 工具 =====

PRON = list("我我們你你們他她它大家")
VERBS = ["是","有","要","想","在","買","吃","看","去","來","說","做","拿","走","坐","喝","學","寫","讀","玩","睡","聽","等","付","愛","覺得"]
FUNCTIONALS = list("了在著把被對於以及和而但呢嗎吧呀啊喔唉哎就還也很")

def normalize_text(s: str) -> str:
    """去除空白、無意義字元"""
    s = s.strip()
    s = re.sub(r"[^\u4e00-\u9fa5a-zA-Z0-9，。！？,.!?]", "", s)
    return s

def pinyin_close(a: str, b: str, max_dist=1) -> bool:
    pa, pb = ''.join(lazy_pinyin(a)), ''.join(lazy_pinyin(b))
    return Levenshtein.distance(pa, pb) <= max_dist

def contains_subject_and_verb(text: str) -> bool:
    has_pron = any(ch in text for ch in PRON)
    has_verb = any(v in text for v in VERBS)
    # 容錯拼音
    if not has_pron:
        has_pron = any(pinyin_close(ch, p, 1) for ch in text for p in PRON)
    if not has_verb:
        has_verb = any(pinyin_close(v, ch, 1) for ch in text for v in VERBS)
    return has_pron and has_verb

def is_understandable(text: str) -> bool:
    """檢查是否可理解：至少3字，有詞彙"""
    if len(text) < 3:
        return False
    seg = list(jieba.cut(text))
    valid = sum(1 for w in seg if len(w.strip()) >= 1)
    return valid / max(1, len(seg)) >= 0.5

# **【新增】統一處理錯誤回傳的函式**
def create_error_response(error_message: str, status_code: int = 400):
    """建立一個符合 C# 客戶端期待的錯誤 JSON 回應，填補所有欄位。"""
    return jsonify({
        "error": error_message,
        "transcript": f"<ERROR> {error_message}", # 傳回錯誤訊息，方便日誌記錄
        "score": -1,       # 預設分數 -1
        "reasons": {       # 補上 reasons 物件，避免 C# JsonUtility 解析失敗
            "has_subject_verb": False,
            "understandable": False
        }
    }), status_code

# ===== API =====

@app.route("/score", methods=["POST"])
def score():
    if "file" not in request.files:
        # 使用新的錯誤回傳函式
        return create_error_response("No audio file", 400)

    file = request.files["file"]
    temp_path = "temp_audio.wav"
    file.save(temp_path)

    try:
        # 使用 speech_recognition + Google Web Speech API
        with sr.AudioFile(temp_path) as source:
            audio_data = r.record(source)
            text = r.recognize_google(audio_data, language="zh-TW")
    except sr.UnknownValueError:
        # 使用新的錯誤回傳函式
        return create_error_response("Could not understand audio", 400)
    except sr.RequestError as e:
        # 使用新的錯誤回傳函式
        return create_error_response(f"Speech API error: {e}", 500)
    except Exception as e:
        # 使用新的錯誤回傳函式
        return create_error_response(str(e), 500)
    finally:
        if os.path.exists(temp_path):
            os.remove(temp_path)

    text = normalize_text(text)
    if not text:
        # 使用新的錯誤回傳函式
        return create_error_response("Empty transcription", 400)

    # NLP 檢查 (成功路徑)
    A = contains_subject_and_verb(text)
    B = is_understandable(text)
    passed = 1 if (A and B) else 0
    reasons = {"has_subject_verb": A, "understandable": B}

    return jsonify({
        "transcript": text,
        "score": passed,
        "reasons": reasons,
        "error": None # 成功時 error 欄位為 None/空字串
    }), 200


@app.route("/health", methods=["GET"])
def health():
    return jsonify({"ok": True}), 200


if __name__ == "__main__":
    app.run(host="127.0.0.1", port=5003)