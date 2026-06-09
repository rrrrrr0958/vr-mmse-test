# 完整安裝與復現指引

> 本文件假設讀者**從零開始**：電腦上尚未安裝 Unity、Python 或任何開發工具。  
> 目標：在 Windows 10/11 環境下，讓此 VR MMSE 測驗系統可以在 Meta Quest 上完整運作。

---

## 目錄

1. [系統需求確認](#1-系統需求確認)
2. [安裝 Git](#2-安裝-git)
3. [取得程式碼](#3-取得程式碼)
4. [安裝 Python 3](#4-安裝-python-3)
5. [建立 Python 虛擬環境與安裝套件](#5-建立-python-虛擬環境與安裝套件)
6. [安裝 Unity Hub](#6-安裝-unity-hub)
7. [安裝正確版本的 Unity Editor](#7-安裝正確版本的-unity-editor)
8. [在 Unity Hub 開啟專案](#8-在-unity-hub-開啟專案)
9. [等待套件載入完成](#9-等待套件載入完成)
10. [設定 Android Build Support](#10-設定-android-build-support)
11. [設定 XR Plugin Management](#11-設定-xr-plugin-management)
12. [Firebase 設定確認](#12-firebase-設定確認)
13. [在 Editor 中測試執行](#13-在-editor-中測試執行)
14. [Build 成 APK 並安裝到 Meta Quest](#14-build-成-apk-並安裝到-meta-quest)
15. [常見問題與排除](#15-常見問題與排除)

---

## 1. 系統需求確認

在開始之前，請確認你的電腦符合以下規格：

| 項目 | 最低需求 |
|------|----------|
| 作業系統 | Windows 10（64-bit）或 Windows 11 |
| CPU | Intel Core i5-8600 / AMD Ryzen 5 2600 或以上 |
| RAM | 16 GB（建議 32 GB） |
| 顯示卡 | NVIDIA GTX 1060 / AMD RX 580 或以上（支援 DirectX 11） |
| 儲存空間 | 至少 30 GB 可用空間（Unity + 專案 + Python 環境） |
| 網路 | 需要網際網路連線（下載套件、Firebase、語音辨識） |
| USB | USB-A 或 USB-C（用於連接 Meta Quest） |

**硬體裝置**（執行測驗所需）：
- Meta Quest 2 或 Meta Quest 3

---

## 2. 安裝 Git

Git 用於下載/管理程式碼。

1. 前往 [https://git-scm.com/download/win](https://git-scm.com/download/win)
2. 下載「64-bit Git for Windows Setup」
3. 執行安裝程式，所有選項保持預設，一路按 Next 直到 Install
4. 安裝完成後，開啟「命令提示字元（cmd）」或「PowerShell」，輸入：
   ```
   git --version
   ```
   若出現版本號（如 `git version 2.47.0`）即代表安裝成功。

---

## 3. 取得程式碼

如果你拿到的是 **ZIP 壓縮檔**：
1. 將 ZIP 解壓縮到你想放置的目錄，例如 `C:\Projects\vr-mmse-test`
2. 確認解壓縮後有這個結構：
   ```
   C:\Projects\vr-mmse-test\
   ├── README.md
   ├── SETUP.md
   └── vr mmse test\        ← Unity 專案在這裡
   ```

如果你拿到的是 **Git 儲存庫網址**：
1. 開啟 PowerShell，輸入：
   ```powershell
   git clone <儲存庫網址> C:\Projects\vr-mmse-test
   ```

> **重要**：專案路徑中不建議有中文字，且不建議放在 OneDrive 或 Dropbox 同步資料夾內，這可能造成 Unity 匯入錯誤。

---

## 4. 安裝 Python 3

Python 用於執行三個後端伺服器（圖形評分、語音辨識、造句評估）。

1. 前往 [https://www.python.org/downloads/windows/](https://www.python.org/downloads/windows/)
2. 下載 **Python 3.11.x**（建議版本，相容性最佳）
   - 點擊「Python 3.11.x - Windows installer (64-bit)」
3. 執行安裝程式：
   - **務必勾選「Add python.exe to PATH」**（安裝畫面最下方的選項）
   - 點擊「Install Now」
4. 安裝完成後，開啟新的 PowerShell 視窗，輸入：
   ```
   python --version
   ```
   應顯示 `Python 3.11.x`

> 如果顯示 `Python 3.12.x` 或其他版本也可以使用，但 3.11 與所有套件相容性最好。

---

## 5. 建立 Python 虛擬環境與安裝套件

為了避免套件版本衝突，請在專案內建立獨立的 Python 虛擬環境。

### 5.1 建立虛擬環境

開啟 PowerShell，切換到專案中的 Scripts 資料夾：

```powershell
cd "C:\Projects\vr-mmse-test\vr mmse test\Assets\Scripts"
python -m venv .venv
```

這會在 Scripts 資料夾內建立一個 `.venv` 子目錄。

### 5.2 啟動虛擬環境

```powershell
.\.venv\Scripts\Activate.ps1
```

成功後，你的命令提示字元前方會出現 `(.venv)` 字樣。

> **PowerShell 執行原則問題**：如果出現「無法執行此腳本」錯誤，請先執行：
> ```powershell
> Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser
> ```
> 然後再重試上面的啟動指令。

### 5.3 安裝所有必要套件

在啟動虛擬環境的狀態下，執行：

```powershell
pip install flask flask-cors pillow numpy opencv-python pandas matplotlib SpeechRecognition pydub pypinyin python-Levenshtein jieba
```

等待所有套件下載安裝完成（約需 3～10 分鐘，視網路速度而定）。

### 5.4 安裝 ffmpeg（pydub 音訊處理需要）

pydub 套件需要 ffmpeg 才能轉換音訊格式：

1. 前往 [https://github.com/BtbN/FFmpeg-Builds/releases](https://github.com/BtbN/FFmpeg-Builds/releases)
2. 下載 `ffmpeg-master-latest-win64-gpl.zip`
3. 解壓縮，將其中的 `bin` 資料夾路徑（例如 `C:\ffmpeg\bin`）加入系統環境變數 PATH：
   - 按 Win + S，搜尋「編輯系統環境變數」
   - 點「環境變數」→ 在「系統變數」中找到 `Path` → 點「編輯」
   - 點「新增」，貼上 `C:\ffmpeg\bin`
   - 確定 → 確定 → 確定
4. 開啟新的 PowerShell 驗證：
   ```
   ffmpeg -version
   ```

### 5.5 確認套件安裝成功

在虛擬環境啟動的狀態下執行：

```powershell
python -c "import flask, cv2, PIL, numpy, speech_recognition, jieba; print('所有套件正常')"
```

若顯示「所有套件正常」即代表安裝完成。

---

## 6. 安裝 Unity Hub

Unity Hub 是管理多個 Unity 版本的統一入口。

1. 前往 [https://unity.com/download](https://unity.com/download)
2. 點擊「Download Unity Hub」，下載 `UnityHubSetup.exe`
3. 執行安裝，預設路徑即可
4. 安裝完成後開啟 Unity Hub
5. 首次開啟會要求你**登入或建立 Unity 帳號**（免費）：
   - 點「Sign in」→「Create account」
   - 填寫 Email 和密碼完成註冊
   - 登入後在授權畫面選擇「Personal」（免費個人授權）

---

## 7. 安裝正確版本的 Unity Editor

本專案使用 **Unity 6000.0.47f1（Unity 6）**，必須使用完全相同的版本。

### 方法一：從 Unity Hub 安裝（建議）

1. 在 Unity Hub 左側點「Installs」
2. 點右上角「Install Editor」
3. 切換到「Archive」或「Other versions」標籤
4. 搜尋 `6000.0.47f1`，若找不到請點「Download Archive」
5. 在瀏覽器中找到 Unity 6 → Unity 6000.0.47f1，點「Install」

### 方法二：直接下載

1. 前往 Unity 下載封存頁：[https://unity.com/releases/editor/archive](https://unity.com/releases/editor/archive)
2. 找到「Unity 6」→「6000.0.47f1」
3. 點「Hub」按鈕，Unity Hub 會自動下載並安裝

### 必選模組（安裝時需要勾選）

在安裝 Unity Editor 時，務必勾選以下模組：

| 模組名稱 | 說明 |
|----------|------|
| **Android Build Support** | 必選，用於打包 APK |
| ↳ Android SDK & NDK Tools | Android Build Support 的子選項，必選 |
| ↳ OpenJDK | Android Build Support 的子選項，必選 |

> 安裝完整大小約 15～20 GB，請確保有足夠空間和穩定網路。

---

## 8. 在 Unity Hub 開啟專案

1. 在 Unity Hub 左側點「Projects」
2. 點右上角「Open」→「Add project from disk」
3. 瀏覽到 `C:\Projects\vr-mmse-test\vr mmse test`（注意：選擇含有 `Assets` 資料夾的那層目錄）
4. 點「Add Project」
5. 確認專案列表中「Editor Version」顯示為 `6000.0.47f1`
6. 點專案名稱開啟，**首次開啟需要約 5～20 分鐘**進行資源匯入

> 如果 Unity Hub 提示版本不符，點「Change Version」選擇正確的版本。

---

## 9. 等待套件載入完成

Unity 首次開啟專案時會自動下載並編譯所有套件，包含：
- Meta XR SDK（最大，約 500 MB）
- XR Interaction Toolkit
- Universal Render Pipeline

**請耐心等待右下角的進度條消失**，整個過程可能需要 10～30 分鐘。

期間可能出現幾個彈出視窗：
- 「New Input System Package」→ 點「Yes」（重啟 Unity）
- XR Plugin Management 設定提示 → 先關閉，等匯入完成再處理
- 任何關於 Obsolete API 的警告 → 可忽略

---

## 10. 設定 Android Build Support

1. 在 Unity 選單列點「File」→「Build Settings」
2. 在「Platform」清單中點「Android」
3. 點右下角「Switch Platform」（需要等候 1～5 分鐘）
4. 切換完成後，點右上角「Player Settings」
5. 在「Player Settings」視窗中確認：
   - **Company Name**: DefaultCompany
   - **Product Name**: vr mmse test
   - **Package Name**: `com.UnityTechnologies.com.unity.template.urpblank`
6. 在左側點「Other Settings」，確認：
   - **Minimum API Level**: API 29 或以上
   - **Target API Level**: API 32

---

## 11. 設定 XR Plugin Management

這是讓 Unity 應用程式支援 Meta Quest VR 的關鍵步驟。

1. 選單列「Edit」→「Project Settings」→ 左側點「XR Plug-in Management」
2. 點「Install XR Plugin Management」（如果尚未安裝）
3. 在「Android」標籤頁，勾選：
   - **OpenXR**
4. 點左側「OpenXR」（在 XR Plug-in Management 下方出現）
5. 在「Interaction Profiles」中點「+」，新增：
   - **Meta Quest Touch Pro Controller Profile**
   - **Oculus Touch Controller Profile**
6. 在「Features」中啟用：
   - **Hand Tracking Subsystem**
   - **Meta Quest: Support OpenXR Runtime**

> 若出現黃色感嘆號，點旁邊的「Fix All」按鈕。

---

## 12. Firebase 設定確認

專案已包含預設的 Firebase 設定，直接使用即可。如果你想使用自己的 Firebase：

### 確認現有設定正常運作

1. 在 Project 視窗中找到 `Assets/google-services.json`
2. 確認檔案存在即可，不需要修改

### 如需建立自己的 Firebase（可選）

1. 前往 [https://console.firebase.google.com](https://console.firebase.google.com)
2. 建立新專案
3. 新增 Android 應用程式，Package name 填入 `com.UnityTechnologies.com.unity.template.urpblank`
4. 下載 `google-services.json`，取代 `Assets/google-services.json`
5. 在 Firebase Console 中啟用：
   - Authentication（Email/Password 登入方式）
   - Firestore Database（建立，選擇亞洲地區）
   - Cloud Storage
6. 在 Firestore 規則中設定允許已登入用戶讀寫：
   ```
   rules_version = '2';
   service cloud.firestore {
     match /databases/{database}/documents {
       match /{document=**} {
         allow read, write: if request.auth != null;
       }
     }
   }
   ```

---

## 13. 在 Editor 中測試執行

由於 VR 應用需要實體裝置才能完整體驗，但可以在 Editor 中進行基本功能測試。

### 13.1 先啟動 Python 後端伺服器

在執行 Unity 前，**必須先手動啟動 Python 伺服器**（應用運行時 Unity 會嘗試自動啟動，但 Editor 中測試建議手動啟動）：

開啟三個獨立的 PowerShell 視窗：

**視窗 1：圖形評分伺服器**
```powershell
cd "C:\Projects\vr-mmse-test\vr mmse test\Assets\Scripts"
.\.venv\Scripts\Activate.ps1
python draw.py
```
看到 `Running on http://127.0.0.1:5002` 表示啟動成功。

**視窗 2：語音辨識伺服器（Game 2/5）**
```powershell
cd "C:\Projects\vr-mmse-test\vr mmse test\Assets\Scripts"
.\.venv\Scripts\Activate.ps1
python audio_5.py
```
看到 `Running on http://127.0.0.1:5000` 表示啟動成功。

**視窗 3：造句評估伺服器（Game 13）**
```powershell
cd "C:\Projects\vr-mmse-test\vr mmse test\Assets\Scripts"
.\.venv\Scripts\Activate.ps1
python app_13.py
```
看到 `Running on http://127.0.0.1:5003` 表示啟動成功。

### 13.2 在 Unity Editor 執行

1. 在 Unity Project 視窗中找到 `Assets/Scenes/Login Scene.unity`，雙擊開啟
2. 確認場景已載入（Hierarchy 視窗應出現場景物件）
3. 點畫面上方的「Play」按鈕（▶）
4. 應出現登入畫面

> **注意**：在 Editor 中無法使用 VR 手部追蹤，但可以用滑鼠模擬點擊。若要完整測試 VR 功能，需要使用 Meta Quest Link 連線真實裝置，或使用 Meta XR Simulator。

### 13.3 使用 Meta XR Simulator（選用）

Meta XR Simulator 允許在電腦上模擬 VR 裝置：

1. 選單「Meta」→「XR Simulator」→「Toggle XR Simulator activiation」
2. 重新 Play，應可看到模擬的 VR 視角

---

## 14. Build 成 APK 並安裝到 Meta Quest

### 14.1 準備 Meta Quest

1. 在 Meta Quest 上啟用**開發者模式**：
   - 使用手機安裝「Meta Horizon」App
   - 在 App 中進入裝置設定 → 開發者模式 → 開啟
2. 用 USB 線連接 Meta Quest 到電腦
3. 戴上 Quest，在裝置上點擊「允許 USB 偵錯」

### 14.2 安裝 Meta Quest Developer Hub（建議）

前往 [https://developer.oculus.com/meta-quest-developer-hub/](https://developer.oculus.com/meta-quest-developer-hub/) 下載安裝，方便管理裝置連接。

### 14.3 在 Unity 中 Build

1. 選單「File」→「Build Settings」
2. 確認平台為 Android
3. 在「Scenes In Build」中確認以下場景都已勾選（按照順序）：
   - Login Scene
   - SampleScene_rule
   - GameIntroScene
   - SampleScene_7
   - Reward_Scene
   - SentenceGame_13
   - SampleScene_3
   - SampleScene_2
   - SampleScene_5
   - SampleScene_11_1
   - SampleScene_11
   - f1_8
   - SampleScene_6
   - SampleScene_14
   - Final_Scroe
   
   若清單是空的，點「Add Open Scenes」將目前場景加入，再手動將其餘場景拖入。

4. 在「Run Device」下拉選單中選擇你的 Meta Quest
5. 點「Build And Run」，選擇儲存 APK 的位置（如桌面）
6. 等待編譯完成（首次約需 5～15 分鐘）
7. 完成後 APK 會自動安裝並在 Quest 上啟動

### 14.4 在 Quest 中找到已安裝的應用

如果 APK 安裝後沒有自動啟動，在 Meta Quest 的應用程式清單中，切換到「未知來源（Unknown Sources）」類別即可找到。

---

## 15. 常見問題與排除

### Q: Unity 開啟時顯示「Version mismatch」錯誤
**A:** 必須使用 **Unity 6000.0.47f1** 版本。在 Unity Hub 的 Installs 中確認版本正確，或重新安裝正確版本。

### Q: Python 伺服器啟動後顯示「port already in use」
**A:** 代表對應的 port 被佔用。開啟 PowerShell 執行：
```powershell
# 找出並終止佔用 5002 port 的程序
netstat -ano | findstr :5002
taskkill /PID <上面找到的PID> /F
```
對 5000、5003 重複相同操作。

### Q: `pip install` 出現錯誤「Microsoft Visual C++ 14.0 is required」
**A:** 需要安裝 Visual C++ Build Tools：
1. 前往 [https://visualstudio.microsoft.com/visual-cpp-build-tools/](https://visualstudio.microsoft.com/visual-cpp-build-tools/)
2. 下載並安裝「Build Tools for Visual Studio」
3. 在安裝選項中勾選「C++ build tools」
4. 重新執行 pip install 指令

### Q: `python-Levenshtein` 安裝失敗
**A:** 試試改用 `rapidfuzz`：
```powershell
pip install rapidfuzz
pip install python-Levenshtein --no-build-isolation
```

### Q: Android Build 失敗，提示「SDK/NDK not found」
**A:**
1. 在 Unity 選單「Edit」→「Preferences」→「External Tools」
2. 確認 JDK、SDK、NDK 路徑正確（通常在 `C:\Program Files\Unity\Hub\Editor\6000.0.47f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK`）
3. 若路徑錯誤，勾選「Use the Unity installed version」

### Q: Meta Quest 無法被偵測到
**A:**
1. 確認已安裝 Android ADB 驅動程式（通常 Android SDK 包含）
2. 確認 Quest 上已允許 USB 偵錯
3. 嘗試更換 USB 線（需使用資料傳輸線，非純充電線）
4. 在 PowerShell 執行 `adb devices` 確認裝置是否顯示

### Q: 語音辨識不工作
**A:** 語音辨識使用 Google Web Speech API，需要網際網路連線。確認：
1. Python 伺服器正在執行（port 5000 和 5003）
2. Quest 連接到有網路的 Wi-Fi
3. 在 Firebase Console 中確認 Authentication 已啟用

### Q: 圖形評分伺服器啟動失敗，提示「ImportError: cv2」
**A:** 確認在虛擬環境中安裝了 opencv-python：
```powershell
.\.venv\Scripts\Activate.ps1
pip install opencv-python --upgrade
```

### Q: 測驗成績沒有儲存到 Firebase
**A:**
1. 確認 `Assets/google-services.json` 存在
2. 確認裝置有網路連線
3. 在 Firebase Console 中確認 Firestore 規則允許讀寫
4. 查看 Unity 的 Console 視窗，尋找 Firebase 相關錯誤訊息

### Q: 場景流程不正確或跳過某些場景
**A:** 確認在 Build Settings 的「Scenes In Build」中所有場景都已勾選且按正確順序排列（詳見步驟 14.3）。

---

## 完成後的系統架構圖

成功部署後，系統運作方式如下：

```
[Meta Quest 裝置]
       |
  [Unity APK]
  ↕ HTTP localhost（透過 WiFi/USB）
[電腦 Python 後端]
  - draw.py    :5002  （圖形評分）
  - audio_5.py :5000  （語音辨識）
  - app_13.py  :5003  （造句評估）
       |
  [Google Cloud]
  - Firebase Auth
  - Firestore DB
  - Google Speech API
```

> **重要注意**：Quest 裝置與執行 Python 伺服器的電腦**必須在同一個區域網路（WiFi）**，或使用 USB Link 連線。Python 伺服器監聽的是 `127.0.0.1`（localhost），代表它只接受來自同一台電腦的請求。若 Quest 以無線方式運行，需要將 Python 伺服器改為監聽 `0.0.0.0` 並修改 Unity 中對應的 IP 位址設定。

---

## 快速啟動清單（每次使用前）

每次要使用系統時，按以下順序操作：

- [ ] 1. 開啟三個 PowerShell 視窗，分別啟動 `draw.py`、`audio_5.py`、`app_13.py`
- [ ] 2. 確認三個伺服器都顯示「Running on http://127.0.0.1:XXXX」
- [ ] 3. 開啟 Meta Quest，確認連接到 WiFi
- [ ] 4. 在 Quest 應用程式庫（未知來源）中開啟「vr mmse test」
- [ ] 5. 登入或建立帳號
- [ ] 6. 開始測驗
