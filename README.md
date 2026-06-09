# VR MMSE Test

一套基於 **Unity 6 + Meta Quest VR** 的虛擬實境認知功能評估系統，以「迷你精神狀態檢查（MMSE）」為框架，將傳統紙筆測驗轉化為沉浸式互動遊戲，並透過 Firebase 進行用戶管理與成績雲端儲存。

---

## 功能概覽

### 測驗關卡流程

| 關卡 | 場景名稱 | 測驗內容 | 認知域 |
|------|----------|----------|--------|
| 1 | `SampleScene_7` | 圖形仿繪（AI 評分） | 視覺空間 / 執行功能 |
| 2 | `SampleScene_14` | 文字辨識測試 | 語言 / 注意力 |
| 3 | `SentenceGame_13` | 口語造句（語音辨識） | 語言 / 口語表達 |
| 4 | `SampleScene_3` | 認知任務 3 | 記憶 |
| 5 | `SampleScene_2` | 語音理解測試 | 語言理解 |
| 6 | `SampleScene_5` | 問答測驗 | 定向力 / 知識 |
| 7 | `SampleScene_11` | 計算 / 序列任務 | 注意力 / 計算 |
| 8 | `f1_8` | 空間導航（3D 環境） | 空間記憶 / 定向力 |
| 9 | `SampleScene_6` | 物件辨識選取 | 記憶 / 語言 |

### 主要特色

- **VR 沉浸式體驗**：採用 Meta Quest 手部追蹤，完全無手把操作
- **AI 圖形評分**：使用 OpenCV 雙向 Chamfer 距離 + 菱形結構偵測對仿繪圖形評分
- **語音辨識**：整合 Google Web Speech API 進行中文語音轉文字與 NLP 評估
- **雲端資料**：Firebase Firestore 儲存用戶個人資料與每場測驗成績
- **手部動作記錄**：以 CSV 格式記錄手部位置/旋轉軌跡供後續研究分析

---

## 系統架構

```
┌─────────────────────────────────┐
│         Unity 應用 (Android)    │
│  XR Bootstrap → Login Scene →   │
│  SceneFlowManager (場景調度)    │
│  └── 9 個遊戲關卡 → Final Score │
└──────────────┬──────────────────┘
               │ HTTP (localhost)
   ┌───────────┴───────────────┐
   │     Python 後端服務群     │
   │  draw.py     :5002  圖形評分  │
   │  audio_5.py  :5000  語音辨識  │
   │  app_13.py   :5003  造句評估  │
   └───────────┬───────────────┘
               │
   ┌───────────┴───────────────┐
   │       Firebase Cloud      │
   │  Authentication           │
   │  Firestore Database       │
   │  Cloud Storage            │
   └───────────────────────────┘
```

### 技術棧

| 層次 | 技術 |
|------|------|
| 遊戲引擎 | Unity 6000.0.47f1 (Unity 6) |
| VR SDK | Meta XR SDK 77/78、OpenXR 1.15.1 |
| 互動框架 | XR Interaction Toolkit 3.0.8、XR Hands 1.6.1 |
| 渲染 | Universal Render Pipeline (URP) 17.0.4 |
| 後端語言 | Python 3.x + Flask |
| 圖形分析 | OpenCV (cv2)、Pillow、NumPy |
| 語音辨識 | SpeechRecognition + Google Web Speech API |
| NLP | jieba 斷詞、pypinyin 拼音比對、python-Levenshtein |
| 雲端 | Firebase Auth、Firestore、Cloud Storage |
| JSON 序列化 | Newtonsoft.Json 3.2.1 |

---

## 專案結構

```
vr-mmse-test/
├── README.md
├── SETUP.md                          ← 完整安裝指引
└── vr mmse test/                     ← Unity 專案根目錄
    ├── Assets/
    │   ├── Scripts/                  ← 94 個 C# 腳本 + Python 後端
    │   │   ├── SceneFlowManager.cs   ← 場景流程總控
    │   │   ├── FirebaseManager_Firestore.cs
    │   │   ├── draw.py               ← 圖形評分伺服器 (port 5002)
    │   │   ├── audio_5.py            ← 語音辨識伺服器 (port 5000)
    │   │   ├── app_13.py             ← 造句評估伺服器 (port 5003)
    │   │   └── Game_*/csv_results/   ← 本地測驗結果 CSV
    │   ├── Scenes/                   ← 40+ Unity 場景
    │   ├── Data/                     ← 遊戲資料 JSON
    │   ├── Prefab/                   ← 可重用預製物件
    │   ├── Audio/                    ← 音效資源
    │   └── google-services.json      ← Firebase 設定 (已包含)
    ├── Packages/
    │   └── manifest.json             ← Unity 套件清單
    └── ProjectSettings/
        └── ProjectVersion.txt        ← Unity 版本: 6000.0.47f1
```

---

## Python 套件需求

Python 後端所需套件（詳見 [SETUP.md](SETUP.md)）：

```
flask
flask-cors
pillow
numpy
opencv-python
pandas
matplotlib
SpeechRecognition
pydub
pypinyin
python-Levenshtein
jieba
```

---

## Firebase 設定

本專案已包含 `google-services.json`，連接至以下 Firebase 專案：

- **Project ID**: `vr-mmse-test-710d8`
- **Realtime Database**: `asia-southeast1` 區域
- **Android Package**: `com.UnityTechnologies.com.unity.template.urpblank`

如需部署自己的 Firebase，請參考 [SETUP.md](SETUP.md) 中的 Firebase 設定章節。

---

## 開發團隊

- 開發語言：C#（Unity）、Python
- UI 語言：繁體中文
- 目標裝置：Meta Quest 2 / 3（Android API 32+）
- 測試資料期間：2025 年 9 月 ～ 10 月
