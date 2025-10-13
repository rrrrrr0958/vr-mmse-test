from flask import Flask, jsonify
from flask_cors import CORS
import pandas as pd
import matplotlib
matplotlib.use("Agg")  # 無頭環境下也能畫圖
import matplotlib.pyplot as plt
from matplotlib.animation import FuncAnimation
from datetime import datetime
import os, time
import threading  # 引入 threading 模組

# === 全域設定 ===
BASE_DIR = os.path.dirname(os.path.abspath(__file__))
SAVE_FOLDER = os.path.join(BASE_DIR, "csv_results")
# 只需要創建一次資料夾
os.makedirs(SAVE_FOLDER, exist_ok=True)
print("🔍 Looking for CSVs in:", SAVE_FOLDER)

TRIM_FIRST = 120  # 去掉前120筆

app = Flask(__name__)
CORS(app)


# === 工具函式 (保持不變) ===
def get_latest_csv(folder=SAVE_FOLDER):
    files = [f for f in os.listdir(folder) if f.endswith(".csv")]
    if not files:
        return None
    files.sort(key=lambda f: os.path.getmtime(os.path.join(folder, f)), reverse=True)
    return files[0]


def wait_file_stable(file_path, checks=5, interval=0.3):
    prev_size = -1
    for _ in range(checks):
        size = os.path.getsize(file_path)
        if size == prev_size:
            return True
        prev_size = size
        time.sleep(interval)
    return True


# === 動畫核心 (保持不變) ===
def generate_animation(file_path, file_name):
    print(f"🎬 Generating animation for {file_name}")
    df = pd.read_csv(file_path)

    if not any(df["Type"].isin(["RightHand", "LeftHand"])):
        print("⚠️ No valid hand data")
        return {"status": "error", "message": "No valid hand data"}

    if len(df) > TRIM_FIRST:
        df = df.iloc[TRIM_FIRST:].reset_index(drop=True)

    dfs = {}
    for hand in ["RightHand", "LeftHand"]:
        h = df[df["Type"] == hand].copy()
        if len(h) == 0:
            continue
        origin = h.iloc[0][["X", "Y", "Z"]].values
        h["Xp"] = h["X"] - origin[0]
        h["Yp"] = h["Y"] - origin[1]
        dfs[hand] = h

    fig, ax = plt.subplots()
    ax.set_xlabel("X (<- left . right ->)")
    ax.set_ylabel("Y (up, down)")
    ax.set_title(f"Hand Trajectory - {file_name}")

    lower = file_name.lower()
    if "pick" in lower or "draw" in lower:
        ax.set_xlim(-0.3, 0.3)
        ax.set_ylim(-0.3, 0.3)

    lines = {}
    colors = {"RightHand": "red", "LeftHand": "blue"}
    for hand, h in dfs.items():
        line, = ax.plot([], [], color=colors[hand], label=hand)
        trigger_dot, = ax.plot([], [], "yo", markersize=10)
        lines[hand] = (line, trigger_dot)
    ax.legend()

    max_len = max(len(h) for h in dfs.values())

    def update(frame):
        for hand, h in dfs.items():
            if frame < len(h):
                data = h[["Xp", "Yp"]].values
                trig = h["TriggerPressed"].values
                line, dot = lines[hand]
                line.set_data(data[:frame, 0], data[:frame, 1])
                if trig[frame] == 1:
                    dot.set_data([data[frame, 0]], [data[frame, 1]])
                else:
                    dot.set_data([], [])
        return [item for pair in lines.values() for item in pair]

    ani = FuncAnimation(fig, update, frames=max_len, interval=50, blit=True)
    mp4_path = file_path.replace(".csv", "_trimmed.mp4")

    ani.save(mp4_path, fps=20, extra_args=["-vcodec", "libx264"])
    plt.close(fig)
    print(f"✅ Saved animation: {mp4_path}")
    return {"status": "ok", "video": mp4_path}


# === 移除原本的 /generate_latest 路由 (或保持不動，讓它仍可手動觸發) ===
# 為了滿足你的需求，我們將核心邏輯移到啟動區塊

# === 核心自動執行函式 ===
def run_initial_generation():
    print("⏳ Attempting to run initial animation generation...")
    latest = get_latest_csv()
    if not latest:
        print("❌ No CSV found, skipping initial generation.")
        return

    latest_path = os.path.join(SAVE_FOLDER, latest)
    print(f"📄 Found latest file: {latest}")
    wait_file_stable(latest_path)

    # 執行動畫生成
    res = generate_animation(latest_path, latest)
    if res.get("status") == "ok":
        print(f"🎉 Initial animation successful! Video at: {res.get('video')}")
    else:
        print(f"😢 Initial animation failed: {res.get('message')}")


@app.route("/health", methods=["GET"])
def health():
    return jsonify({"status": "alive", "folder": SAVE_FOLDER})


if __name__ == "__main__":
    run_initial_generation()
    print("🌐 Flask animation server running at http://127.0.0.1:5051")
    app.run(host="127.0.0.1", port=5051, debug=False)
