from flask import Flask, request, jsonify
from flask_cors import CORS
import speech_recognition as sr
import os
import pydub
import threading
import subprocess
import signal

app = Flask(__name__)
CORS(app)  # 允許來自 Unity 的跨域請求

# 加上全域 shutdown_flag 與 child_processes
shutdown_flag = False
child_processes = []

# 初始化語音辨識器
r = sr.Recognizer()

@app.route('/recognize_speech', methods=['POST'])
def recognize_speech():
    if 'file' not in request.files:
        return jsonify({"error": "No file part"}), 400

    file = request.files['file']

    if file.filename == '':
        return jsonify({"error": "No selected file"}), 400

    # 將接收到的音訊檔案暫時儲存
    filepath = "temp_audio.wav"
    file.save(filepath)

    try:
        # 轉換為 .wav 格式
        audio = pydub.AudioSegment.from_file(filepath)
        audio.export(filepath, format="wav")

        # 使用語音辨識庫進行辨識
        with sr.AudioFile(filepath) as source:
            audio_data = r.record(source)

            # 方案 1: Google Web Speech (需連網)
            text = r.recognize_google(audio_data, language='zh-TW')

            # 方案 2: Vosk (離線)
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


# === 新增優雅關閉路由 ===
@app.route("/shutdown", methods=['POST'])
def shutdown():
    global shutdown_flag
    shutdown_flag = True
    return {"message":"Shutdown flag set"}, 200

def run_flask():
    app.run(host="0.0.0.0", port=5000, debug=False, use_reloader=False, threaded=True)

if __name__=="__main__":
    server_thread = threading.Thread(target=run_flask)
    server_thread.start()

    try:
        while not shutdown_flag:
            threading.Event().wait(0.5)
    finally:
        print("Shutdown flag detected, terminating child processes...")
        for p in child_processes:
            try:
                p.terminate()
            except:
                pass
        print("Exiting server...")
        os._exit(0)
