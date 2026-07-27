# 各關卡評分邏輯整理

> 本文件整理自程式碼中既有的註解、Tooltip 與判斷式（非新增邏輯），供交接者快速掌握「每一關到底怎麼判定對錯／怎麼給分」。若要修改評分規則，請直接找下方對應的檔案與行號。

## 關卡編號對照表（重要：三套編號並不一致）

開發期間「檔名尾碼的關卡序」「程式內部寫入 Firebase 的 `levelIndex`」「MMSE 情境編號」三者並沒有對齊，交接時容易搞混，先對照清楚：

| 檔名關卡序 | 測驗內容 | 主要程式檔 | Firebase `levelIndex` |
|---|---|---|---|
| 2 | 複誦句子 | `AnswerLogicManager_2.cs` | `"6"` |
| 3 | 攤位指向點擊 | `game_start_34.cs` | `"4"` |
| 4 | 魚攤/水果語音問答 | `game_start_34.cs` | `"5"` |
| 5 | 減法運算購物 | `QuestionManager_5.cs` | `"7"` |
| 6 | 指向物件並停留 | `game_6_7/6/QuizManager_6.cs` | `"10"` |
| 7 | 圖形仿繪 | `draw.py` + `ShapeScorer_7.cs`/`ScoreUI_7.cs` | 依場景而定 |
| 8 | 地點導覽（類別/樓層/攤位） | `RunLogger_8.cs` | `"9"` |
| 10/11 | 動物記憶（呈現+回憶） | `GameManager_menu_10.cs` + `GameManager_10.cs` | `"8_Round0"`, `"8_Round{n}"` |
| 13 | 口語造句 | `app_13.py` + `AsrClient_13.cs` 等 | - |
| 14 | 時間定向 | `SimpleTestManager_14.cs` | `"2"` |

---

## Game 7｜圖形仿繪（AI 評分，最複雜的一關）

分兩層：Python 後端算分 + C# 端顯示/門檻判斷。

**後端 `Assets/Scripts/draw.py`（Flask, port 5002）**：
1. `to_edges()` 把使用者畫的圖與目標圖都轉成二值邊緣圖
2. `best_scale_bichamfer()` 在 0.85~1.25 倍縮放範圍內掃描，取**雙向 Chamfer 距離**最小（即形狀最吻合）的分數，滿分 100
3. `diamond_exists_score()` 額外偵測「兩個圖形是否有相交形成菱形」——用 target 圖估計腰部位置，在使用者的畫裡找封閉的洞（contour 的 child），依洞的位置/扁平度/角度給 0~100 的存在分
4. `score_one()` 融合規則：若判定「有相交」（`s_exist >= exist_threshold`，預設 15 分），最終分數 = `chamfer_weight(0.6) * chamfer分 + 0.4 * 相交分`；若判定無相交，最終分數 = `chamfer分 * no_diamond_factor(0.55)`（懲罰）

**C# 端 `Assets/Scripts/game_6_7/7/shape-server_7/ShapeScorer_7.cs` / `ScoreUI_7.cs`**：
- `passThreshold = 60f` / `passCutoff = 60f` — 目前程式碼裡的及格門檻是 **60 分**（注意：commit history 中有一筆 `7 門檻改55分`，代表門檻在開發期間調過，現以程式碼中的 60 為準，實際 build 前建議再次確認這兩個檔案的數值一致）

---

## Game 13｜口語造句

**後端 `Assets/Scripts/app_13.py`（Flask, port 5003）**：
語音轉文字（Google Web Speech API, `zh-TW`）後做兩項 NLP 檢查，**兩者皆通過才算對（二元 0/1 分）**：
- `contains_subject_and_verb()`：文字中是否同時包含代名詞（我/你/他/大家…）與動詞（是/有/要/買/吃…），並用拼音距離（Levenshtein ≤1）容錯同音字
- `is_understandable()`：長度 ≥3 字，且用 `jieba` 斷詞後有效詞彙比例 ≥50%

---

## Game 2｜複誦句子

`Assets/Scripts/AnswerLogicManager_2.cs`：語音轉文字後（`audio_5.py` 只負責轉錄不計分），用 **Levenshtein 編輯距離換算相似度**，門檻 `SimilarityThreshold = 0.50f`（≥50% 相似即算對）。正確句庫目前只有 2 句啟用（「海鮮折扣快來買」「早起買菜精神好」），另有一句「雞豬牛羊都有賣」在程式碼中被註解停用（但對應音檔還留著，屬殘餘素材）。
> 附註：`MyTranscriptionResult_2.cs` 是一個沒有被任何地方引用的資料結構（DTO），屬於未串接的舊版/死碼，交接時不用理它。

## Game 3 + 4｜攤位點擊 + 語音問答（同一檔案 `game_start_34.cs`）

- **攤位點擊**：點擊物件名稱與目標攤位名稱做**字串完全比對**，無容錯，3 輪計分（滿分 3）
- **語音問答**（2 題）：`audio_5.py` 轉錄後，C# 端用**同音字/近音字清單**做「文字包含比對」（非精確比對）。Q1（魚攤）可接受清單多達 30 個近音變體；Q2（水果）僅接受「香蕉」「芭蕉」。逾時或無麥克風一律判錯。

## Game 5｜減法運算購物

`Assets/Scripts/QuestionManager_5.cs`：語音轉文字後，先用正規表示式去除所有非數字字元，再與預期剩餘金額做**字串完全比對**（無誤差容忍）。5 題隨機排序。

## Game 6｜指向物件並停留

`Assets/Scripts/game_6_7/6/QuizManager_6.cs`：目標 ID 完全相等即算對（二元 0/1）。題庫 4 選 1 隨機抽題。**注意**：`advanceOnAnySelection = true`，代表玩家選中「任何」物件（無論對錯）就會直接進入下一關，屬單次嘗試、不能重選。

## Game 8｜地點導覽

`Assets/Scripts/RunLogger_8.cs`：三階段（類別/樓層/攤位）各自的對錯判斷由呼叫端傳入布林值，本檔案只做加權彙總：類別對 +1、樓層對 +2、攤位對 +2，單題滿分 5。

## Game 10/11｜動物記憶

- 呈現階段 `GameManager_menu_10.cs`：玩家（或系統）點選的 3 隻動物直接被記錄為「正確答案」，此階段不計分
- 回憶階段 `GameManager_10.cs`：玩家需再次點選 3 隻動物，與剛才記錄的答案做**集合交集比對**（不看順序），`accuracy = 交集數 / 正確答案數`
- `ResultManager_10.cs` 幾乎是空殼，`OnRoundFinished` 內只有註解、未被呼叫，屬未完成的擴充點，交接時可忽略或視需要接手完成

## Game 14｜時間定向

`Assets/Scripts/SimpleTestManager_14.cs`：年/月/日/星期採**完全字串比對**；時辰（小時）**允許 ±2 小時容錯**（含跨日環狀計算）。5 題四選一按鈕，選項依系統當下時間動態產生。每題答對 +1（滿分 5）。

---

## 給接手者的建議
- 大多數關卡的「正確答案容錯規則」（同音字清單、相似度門檻、時辰誤差範圍）都是寫死在程式碼裡的**經驗值**，不是從論文或臨床標準推導的公式，若要調整嚴格度，直接找上面對應的常數改就好。
- Game 7 的評分是全專案最複雜的部分，牽涉 Python 影像處理 + C# 門檻雙邊調校，改動前建議先用 `Assets/Scripts/Game_*/csv_results` 裡留存的歷史畫圖紀錄做迴歸測試，避免改壞既有準確度。
