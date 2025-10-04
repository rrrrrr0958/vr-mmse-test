from flask import Flask, request, jsonify
import os
import pandas as pd
import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation
import threading
import subprocess
import signal

app = Flask(__name__)
SAVE_FOLDER = "results"
os.makedirs(SAVE_FOLDER, exist_ok=True)

shutdown_flag = False
child_processes = []

# === 儲存並處理 CSV 的 endpoint ===
@app.route("/upload_csv", methods=["POST"])
def upload_csv():
    file_name = request.headers.get("File-Name", "trajectory.csv")
    file_path = os.path.join(SAVE_FOLDER, file_name)
    csv_data = request.data.decode("utf-8")
    with open(file_path, "w", encoding="utf-8") as f:
        f.write(csv_data)

    df = pd.read_csv(file_path)
    if "RightHand" not in df["Type"].values:
        return {"message": "No right hand data in CSV"}, 400

    if len(df) > 60:
        df = df.iloc[60:].reset_index(drop=True)

    origin_row = df[df["Type"]=="RightHand"].iloc[0]
    origin = origin_row[["X","Y","Z"]].values
    df["Xp"] = df["X"] - origin[0]
    df["Yp"] = df["Y"] - origin[1]

    data = df[["Xp","Yp"]].values
    trigger = df["TriggerPressed"].values
    max_len = len(data)

    fig, ax = plt.subplots()
    ax.set_xlabel("X (<-left.right->)")
    ax.set_ylabel("Y (up,down)")
    ax.set_title("Right Hand Trajectory (Trimmed)")
    line, = ax.plot([], [], color="red", label="RightHand")
    trigger_dot, = ax.plot([], [], "yo", markersize=12, label="Trigger")
    ax.legend()
    ax.set_xlim(df["Xp"].min()-0.1, df["Xp"].max()+0.1)
    ax.set_ylim(df["Yp"].min()-0.1, df["Yp"].max()+0.1)

    def update(frame):
        line.set_data(data[:frame,0], data[:frame,1])
        tx, ty = [], []
        if trigger[frame]==1:
            tx.append(data[frame,0])
            ty.append(data[frame,1])
        trigger_dot.set_data(tx, ty)
        return [line, trigger_dot]

    ani = FuncAnimation(fig, update, frames=max_len, interval=50, blit=True)

    # 改成 spawn ffmpeg 手動加入 child_processes
    video_path = file_path.replace(".csv","_trimmed.mp4")
    # matplotlib 會自動 spawn ffmpeg
    # 如果你用其他方式 spawn subprocess，請 append child_processes
    ani.save(video_path, fps=20, extra_args=["-vcodec","libx264"])
    plt.close(fig)

    return {"message":"CSV received and trimmed animation saved","file":video_path}

# === 優雅關閉 endpoint ===
@app.route("/shutdown", methods=["POST"])
def shutdown():
    global shutdown_flag
    shutdown_flag = True
    return {"message": "Shutdown flag set"}, 200

# === 主 loop 監控 shutdown_flag ===
def run_flask():
    app.run(host="127.0.0.1", port=5001, debug=False, use_reloader=False, threaded=True)

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
