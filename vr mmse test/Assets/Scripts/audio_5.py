from flask import Flask, request, jsonify
from flask_cors import CORS
import speech_recognition as sr
import os
import pydub
import threading

app = Flask(__name__)
CORS(app)

r = sr.Recognizer()

@app.route('/recognize_speech', methods=['POST'])
def recognize_speech():
    if 'file' not in request.files:
        return jsonify({"error": "No file part"}), 400

    file = request.files['file']
    if file.filename == '':
        return jsonify({"error": "No selected file"}), 400

    filepath = "temp_audio.wav"
    file.save(filepath)

    try:
        audio = pydub.AudioSegment.from_file(filepath)
        audio.export(filepath, format="wav")

        with sr.AudioFile(filepath) as source:
            audio_data = r.record(source)
            text = r.recognize_google(audio_data, language='zh-TW')

        os.remove(filepath)
        return jsonify({"transcription": text}), 200

    except sr.UnknownValueError:
        return jsonify({"error": "Speech recognition could not understand audio"}), 400
    except sr.RequestError as e:
        return jsonify({"error": f"Could not request results; {e}"}), 500
    except Exception as e:
        return jsonify({"error": f"An error occurred: {e}"}), 500

@app.route("/shutdown", methods=["POST"])
def shutdown():
    func = request.environ.get("werkzeug.server.shutdown")
    if func is None:
        raise RuntimeError("Not running with the Werkzeug Server")
    func()
    return {"message": "Server shutting down..."}

if __name__ == "__main__":
    app.run(host="127.0.0.1", port=5000, debug=False, use_reloader=False, threaded=True)
