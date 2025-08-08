from flask import Flask, request, jsonify
import whisper
import os

app = Flask(__name__)
model = whisper.load_model("base")

@app.route('/transcribe', methods=['POST'])
def transcribe():
    if 'file' not in request.files:
        return "No file part", 400

    file = request.files['file']
    filepath = os.path.join("recordings", file.filename)
    os.makedirs("recordings", exist_ok=True)
    file.save(filepath)

    result = model.transcribe(filepath, language='zh')
    return result["text"]

if __name__ == '__main__':
    app.run(host='0.0.0.0', port=5000)

