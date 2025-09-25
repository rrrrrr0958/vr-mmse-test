from flask import Flask, request, jsonify
from flask_cors import CORS
import speech_recognition as sr
import os
import pydub
import tempfile 

app = Flask(__name__)
CORS(app) 

# 初始化語音辨識器
r = sr.Recognizer()

# 路由修改為與第一個參考範例相同的 /recognize_speech
@app.route('/recognize_speech', methods=['POST'])
def recognize_speech():
    print("⚡ /recognize_speech 路由被呼叫")
    if 'file' not in request.files:
        return jsonify({"error": "No file part"}), 400

    file = request.files['file']

    if file.filename == '':
        return jsonify({"error": "No selected file"}), 400

    # 使用臨時檔案來儲存上傳的音訊
    with tempfile.NamedTemporaryFile(delete=False, suffix=".wav") as tmp_file:
        filepath = tmp_file.name
        file.save(filepath)
    
    audio_source_path = filepath 

    try:
        # 確保格式為 WAV
        audio = pydub.AudioSegment.from_file(audio_source_path)
        audio.export(audio_source_path, format="wav")
        print(f"[DEBUG] 音訊已轉換並儲存為 WAV: {audio_source_path}")

        # 使用語音辨識庫進行辨識
        with sr.AudioFile(audio_source_path) as source:
            audio_data = r.record(source)  
            
            # 使用 Google Web Speech API (zh-TW)
            text = r.recognize_google(audio_data, language='zh-TW')
            print(f"[DEBUG] 辨識結果: {text}")

        return jsonify({"transcription": text}), 200

    except sr.UnknownValueError:
        print("[ERROR] Speech recognition could not understand audio")
        return jsonify({"error": "Speech recognition could not understand audio"}), 400
    except sr.RequestError as e:
        print(f"[ERROR] Could not request results from service; {e}")
        return jsonify({"error": f"Could not request results from service; {e}"}), 500
    except Exception as e:
        print(f"[ERROR] An error occurred: {e}")
        return jsonify({"error": f"Speech recognition failed: {e}"}), 500
    finally:
        # 無論成功或失敗，刪除暫存檔案
        if os.path.exists(audio_source_path):
            os.remove(audio_source_path)


if __name__ == '__main__':
    app.run(host="0.0.0.0", port=5000, debug=False, use_reloader=False, threaded=True)