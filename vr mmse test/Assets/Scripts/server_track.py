from flask import Flask, request
import os
import pandas as pd
import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation

app = Flask(__name__)
SAVE_FOLDER = "results"
os.makedirs(SAVE_FOLDER, exist_ok=True)

@app.route("/upload_csv", methods=["POST"])
def upload_csv():
    file_name = request.headers.get("File-Name", "trajectory.csv")
    file_path = os.path.join(SAVE_FOLDER, file_name)

    # 儲存 CSV
    csv_data = request.data.decode("utf-8")
    with open(file_path, "w", encoding="utf-8") as f:
        f.write(csv_data)

    # 讀取 CSV
    df = pd.read_csv(file_path)

    if "RightHand" not in df["Type"].values:
        return {"message": "No right hand data in CSV"}, 400

    # 取第一筆右手作為原點
    origin_row = df[df["Type"] == "RightHand"].iloc[0]
    origin = origin_row[["X","Y","Z"]].values

    # 玩家正面視角 XY 平面 (+X 左右, +Y 上下)
    df["Xp"] = df["X"] - origin[0]
    df["Yp"] = df["Y"] - origin[1]

    # 座標和 trigger
    data = df[["Xp","Yp"]].values
    trigger = df["TriggerPressed"].values
    max_len = len(data)

    # 繪圖
    fig, ax = plt.subplots()
    ax.set_xlabel("X (左右)")
    ax.set_ylabel("Y (上下)")
    ax.set_title("Right Hand Trajectory")
    line, = ax.plot([], [], color="red", label="RightHand")
    trigger_dot, = ax.plot([], [], "yo", markersize=8, label="Trigger")
    ax.legend()
    ax.set_xlim(df["Xp"].min()-0.1, df["Xp"].max()+0.1)
    ax.set_ylim(df["Yp"].min()-0.1, df["Yp"].max()+0.1)

    def update(frame):
        line.set_data(data[:frame,0], data[:frame,1])
        # trigger 顯示
        tx, ty = [], []
        if trigger[frame]==1:
            tx.append(data[frame,0])
            ty.append(data[frame,1])
        trigger_dot.set_data(tx, ty)
        return [line, trigger_dot]

    ani = FuncAnimation(fig, update, frames=max_len, interval=50, blit=True)
    video_path = file_path.replace(".csv",".mp4")
    ani.save(video_path, fps=20, extra_args=["-vcodec","libx264"])
    plt.close(fig)

    return {"message":"CSV received and animation saved","file":video_path}

if __name__=="__main__":
    app.run(host="127.0.0.1", port=5001, debug=False, use_reloader=False, threaded=True)
