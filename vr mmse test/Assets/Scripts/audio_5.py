from flask import Flask, request, jsonify
from flask_cors import CORS
import speech_recognition as sr
import os
import pydub

app = Flask(__name__)
CORS(app) # 允許來自 Unity 的跨域請求

# 初始化語音辨識器
r = sr.Recognizer()

@app.route('/recognize_speech', methods=['POST'])
def recognize_speech():
    # 檢查請求中是否包含音訊檔案
    if 'file' not in request.files:
        return jsonify({"error": "No file part"}), 400

    file = request.files['file']

    if file.filename == '':
        return jsonify({"error": "No selected file"}), 400

    # 將接收到的音訊檔案暫時儲存
    filepath = "temp_audio.wav"
    file.save(filepath)

    try:
        # 轉換為 .wav 格式 (因為一些辨識庫可能需要)
        audio = pydub.AudioSegment.from_file(filepath)
        audio.export(filepath, format="wav")

        # 使用語音辨識庫進行辨識
        with sr.AudioFile(filepath) as source:
            audio_data = r.record(source)  # 讀取整個音訊檔案

            # *** 選擇你的辨識引擎 ***
            # 你可以切換成你想要的引擎。這裡我們使用 Google Web Speech API，
            # 但你也可以用離線的 Vosk (需另外安裝 Vosk 庫並下載模型)。
            #
            # 方案 1: Google Web Speech (需連網)
            text = r.recognize_google(audio_data, language='zh-TW')

            # 方案 2: Vosk (離線，需另外設定)
            # text = r.recognize_vosk(audio_data, language='zh-TW')

        # 刪除暫存檔案
        os.remove(filepath)

        return jsonify({"transcription": text}), 200

    except sr.UnknownValueError:
        return jsonify({"error": "Speech recognition could not understand audio"}), 400
    except sr.RequestError as e:
        return jsonify({"error": f"Could not request results from service; {e}"}), 500
    except Exception as e:
        return jsonify({"error": f"An error occurred: {e}"}), 500

if __name__ == '__main__':
    app.run(host="0.0.0.0", port=5000, debug=False, use_reloader=False, threaded=True)