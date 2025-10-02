import pandas as pd
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d import Axes3D

# 讀取 CSV
df = pd.read_csv("Session1.csv")

# 建立 3D 圖
fig = plt.figure()
ax = fig.add_subplot(111, projection='3d')

# 頭部
head = df[df["Type"] == "Head"]
ax.plot(head["X"], head["Y"], head["Z"], color="blue", label="Head")

# 左手
left = df[df["Type"] == "LeftHand"]
ax.plot(left["X"], left["Y"], left["Z"], color="green", label="Left Hand")

# 右手
right = df[df["Type"] == "RightHand"]
ax.plot(right["X"], right["Y"], right["Z"], color="red", label="Right Hand")

# 美化圖表
ax.set_xlabel("X")
ax.set_ylabel("Y")
ax.set_zlabel("Z")
ax.legend()
plt.title("VR Trajectory (Head & Hands)")

plt.show()
