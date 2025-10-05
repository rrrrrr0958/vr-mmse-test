from flask import Flask, request
import os
import pandas as pd
import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation
from datetime import datetime

app = Flask(__name__)
SAVE_FOLDER = "results"
os.makedirs(SAVE_FOLDER, exist_ok=True)

@app.route("/upload_csv", methods=["POST"])
def upload_csv():
    # 建立唯一檔名
    timestamp = datetime.now().strftime("%Y%m%d_%H%M%S")
    file_name = f"session_{timestamp}.csv"
    file_path = os.path.join(SAVE_FOLDER, file_name)

    # 儲存 CSV
    csv_data = request.data.decode("utf-8")
    with open(file_path, "w", encoding="utf-8") as f:
        f.write(csv_data)

    # 讀取 CSV
    df = pd.read_csv(file_path)

    # 檢查是否有至少一隻手的資料
    if not any(df["Type"].isin(["RightHand", "LeftHand"])):
        return {"message": "No valid hand data found"}, 400

    # 去掉前 60 筆
    if len(df) > 60:
        df = df.iloc[60:].reset_index(drop=True)

    # 對每一隻手分別做位移轉換
    dfs = {}
    for hand in ["RightHand", "LeftHand"]:
        hand_df = df[df["Type"] == hand]
        if len(hand_df) == 0:
            continue
        origin = hand_df.iloc[0][["X", "Y", "Z"]].values
        hand_df["Xp"] = hand_df["X"] - origin[0]
        hand_df["Yp"] = hand_df["Y"] - origin[1]
        dfs[hand] = hand_df

    # 準備畫圖
    fig, ax = plt.subplots()
    ax.set_xlabel("X (<- left . right ->)")
    ax.set_ylabel("Y (up, down)")
    ax.set_title("Hand Trajectory (Trimmed)")
    ax.set_xlim(-0.5, 0.5)
    ax.set_ylim(0, 1.0)

    # 根據有的手建立線物件
    lines = {}
    triggers = {}
    colors = {"RightHand": "red", "LeftHand": "blue"}

    for hand, hand_df in dfs.items():
        line, = ax.plot([], [], color=colors[hand], label=hand)
        trigger_dot, = ax.plot([], [], "yo", markersize=10)
        lines[hand] = (line, trigger_dot)

    ax.legend()

    # 動畫更新
    max_len = max(len(df_) for df_ in dfs.values())

    def update(frame):
        for hand, hand_df in dfs.items():
            if frame < len(hand_df):
                data = hand_df[["Xp", "Yp"]].values
                trigger = hand_df["TriggerPressed"].values
                line, dot = lines[hand]
                line.set_data(data[:frame, 0], data[:frame, 1])
                if trigger[frame] == 1:
                    dot.set_data([data[frame, 0]], [data[frame, 1]])
                else:
                    dot.set_data([], [])
        return [item for pair in lines.values() for item in pair]

    ani = FuncAnimation(fig, update, frames=max_len, interval=50, blit=True)
    video_path = file_path.replace(".csv", "_trimmed.mp4")
    ani.save(video_path, fps=20, extra_args=["-vcodec", "libx264"])
    plt.close(fig)

    return {"message": "CSV received and trimmed animation saved", "file": video_path}


@app.route("/shutdown", methods=["POST"])
def shutdown():
    func = request.environ.get("werkzeug.server.shutdown")
    if func is None:
        raise RuntimeError("Not running with the Werkzeug Server")
    func()
    return {"message": "Server shutting down..."}


if __name__ == "__main__":
    app.run(host="127.0.0.1", port=5001, debug=False, use_reloader=False, threaded=True)
