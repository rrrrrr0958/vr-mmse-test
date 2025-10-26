using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using System.Text;
using System.Linq;
using System.Globalization;
using System.Diagnostics;

public class RunLogger : MonoBehaviour
{
    private FirebaseManager_Firestore FirebaseManager;

    [Serializable]
    public class QARecord
    {
        // 基礎資訊
        public string timeISO;
        public string sceneName;
        public int rtMs;            // 總反應時間 (ms)
        public bool finalCorrect;   // 總體是否正確 (based on finalCorrectMode)

        // --- Stage 1: 類別 ---
        public string categoryCorrect;
        public string categoryChosen;
        public bool categoryIsCorrect;

        // --- Stage 2: 樓層 ---
        public string floorCorrect;
        public string floorChosen;
        public bool floorIsCorrect;

        // --- Stage 3: 攤位 ---
        public string stallCorrectKey;      // 正解 vpName
        public string stallCorrectDisplay;  // 正解 顯示文字
        public string stallChosenKey;       // 使用者選的 vpName
        public string stallChosenDisplay;   // 使用者選的 顯示文字
        public bool stallIsCorrect;
    }

    string _runId;
    readonly List<QARecord> _records = new();
    
    // BeginQuestion 會快取這兩項，供 EndThreeStageQuestion 寫入
    public string CurrentTargetKey { get; private set; }
    string _displayTextCache;

    // 自動 RT 計時（用於計算三階段總時長）
    Stopwatch _questionTimer;

    /// <summary>（可選）設定當前題目的顯示文字快取。</summary>
    public void SetDisplayTextCache(string displayText) => _displayTextCache = displayText;

    /// <summary>開始一次新紀錄。</summary>
    public void StartRun()
    {
        _runId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        _records.Clear();
        _questionTimer = null;
        UnityEngine.Debug.Log($"[RunLogger] StartRun id={_runId}  path={Application.persistentDataPath}/runs");
    }

    /// <summary>開始一題（會重置 RT 碼錶）。</summary>
    public void BeginQuestion(string sceneName, string vpKey, string displayText)
    {
        CurrentTargetKey = vpKey;
        _displayTextCache = displayText;

        _questionTimer?.Stop();
        _questionTimer = new Stopwatch();
        _questionTimer.Start();
    }

    /// <summary>
    /// [版本 1 使用] 結束一題。若 rtMs <= 0，會以內部碼錶時間代入。
    /// </summary>
    public void EndQuestion(string userChoiceKey, bool correct, int rtMs)
    {
        // 若沒提供有效 RT，使用碼錶
        int rt = rtMs;
        if ((rtMs <= 0) && _questionTimer != null)
        {
            _questionTimer.Stop();
            try { rt = (int)_questionTimer.Elapsed.TotalMilliseconds; }
            catch { rt = 0; }
        }

        var rec = new QARecord
        {
            timeISO = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            rtMs = rt,
            finalCorrect = correct,

            // Stage 1
            categoryCorrect = "",
            categoryChosen = "",
            categoryIsCorrect = false,

            // Stage 2
            floorCorrect = "",
            floorChosen = "",
            floorIsCorrect = false,

            // Stage 3
            stallCorrectKey = CurrentTargetKey,
            stallCorrectDisplay = _displayTextCache,
            stallChosenKey = userChoiceKey ?? "",
            stallChosenDisplay = "", // 舊版 API 無法取得
            stallIsCorrect = correct
        };

        _records.Add(rec);
        CleanupAfterQuestion();
    }

    /// <summary>
    /// [版本 2 使用] 結束一題三階段問答。
    /// </summary>
    public void EndThreeStageQuestion(
        string categoryCorrect, string categoryChosen, bool categoryIsCorrect,
        string floorCorrect, string floorChosen, bool floorIsCorrect,
        string stallChosenKey, string stallChosenDisplay, bool stallIsCorrect,
        bool finalCorrect
        )
    {
        int rt = 0;
        if (_questionTimer != null)
        {
            _questionTimer.Stop();
            try { rt = (int)_questionTimer.Elapsed.TotalMilliseconds; }
            catch { rt = 0; }
        }

        var rec = new QARecord
        {
            timeISO = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
            sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            rtMs = rt,
            finalCorrect = finalCorrect,

            // Stage 1
            categoryCorrect = categoryCorrect,
            categoryChosen = categoryChosen,
            categoryIsCorrect = categoryIsCorrect,

            // Stage 2
            floorCorrect = floorCorrect,
            floorChosen = floorChosen,
            floorIsCorrect = floorIsCorrect,

            // Stage 3
            stallCorrectKey = CurrentTargetKey,       // From BeginQuestion
            stallCorrectDisplay = _displayTextCache,  // From BeginQuestion
            stallChosenKey = stallChosenKey,
            stallChosenDisplay = stallChosenDisplay,
            stallIsCorrect = stallIsCorrect
        };

        _records.Add(rec);
        CleanupAfterQuestion();
    }


    void CleanupAfterQuestion()
    {
        // 清理當前題目狀態
        _displayTextCache = null;
        CurrentTargetKey = null;

        // 重置碼錶，避免下一題誤用
        _questionTimer?.Reset();
    }


    /// <summary>結束此次紀錄並寫檔（CSV 與 JSON）。沒有紀錄則不輸出。</summary>
    public void EndRun()
    {
        if (string.IsNullOrEmpty(_runId) || _records.Count == 0) return;

        // 準備統計 (改用 finalCorrect)
        float accuracy = _records.Count > 0
            ? _records.Count(r => r.finalCorrect) / (float)_records.Count
            : 0f;

        var validRts = _records.Where(r => r.rtMs > 0).Select(r => r.rtMs).ToList();
        int avgRt = validRts.Count > 0 ? (int)validRts.Average() : 0;

        string folder = Path.Combine(Application.persistentDataPath, "runs");
        string baseName = Path.Combine(folder, $"run_{_runId}");

        try
        {
            Directory.CreateDirectory(folder);

            // 原有輸出：每次 run 產出一組 csv/json 到 persistentDataPath/runs
            File.WriteAllText(baseName + ".csv", ToCSV(_records), Encoding.UTF8);

            var wrap = new Wrapper
            {
                items = _records,
                accuracy = accuracy,
                avgRtMs = avgRt,
                n = _records.Count,
                createdAt = DateTime.Now.ToString("o", CultureInfo.InvariantCulture),
            };
            File.WriteAllText(baseName + ".json", JsonUtility.ToJson(wrap, true), Encoding.UTF8);

            // ★ 新增/修改：累積寫入 results_8.csv
            AppendToResults13Csv(_records);

            UnityEngine.Debug.Log($"[RunLogger] Saved:\n  {baseName}.csv\n  {baseName}.json\n  (n={_records.Count}, acc={accuracy:P1}, avgRT={avgRt} ms)");
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogError($"[RunLogger] Save FAILED: {ex.Message}\nPath tried: {baseName}*");
        }
        finally
        {
            _runId = null;
            _records.Clear();
            _questionTimer = null;
        }
    }

    [Serializable]
    class Wrapper
    {
        public List<QARecord> items;
        public float accuracy;
        public int avgRtMs;
        public int n;
        public string createdAt;
    }

    // ======== 內部工具 ========

    string ToCSV(List<QARecord> list)
    {
        var sb = new StringBuilder();
        // 更新 CSV 標頭
        sb.AppendLine("timeISO,sceneName,rtMs,finalCorrect,categoryCorrect,categoryChosen,categoryIsCorrect,floorCorrect,floorChosen,floorIsCorrect,stallCorrectKey,stallCorrectDisplay,stallChosenKey,stallChosenDisplay,stallIsCorrect");
        foreach (var r in list)
        {
            sb.AppendLine(string.Join(",",
                Escape(r.timeISO),
                Escape(r.sceneName),
                r.rtMs.ToString(CultureInfo.InvariantCulture),
                r.finalCorrect ? "1" : "0",

                Escape(r.categoryCorrect),
                Escape(r.categoryChosen),
                r.categoryIsCorrect ? "1" : "0",

                Escape(r.floorCorrect),
                Escape(r.floorChosen),
                r.floorIsCorrect ? "1" : "0",

                Escape(r.stallCorrectKey),
                Escape(r.stallCorrectDisplay),
                Escape(r.stallChosenKey),
                Escape(r.stallChosenDisplay),
                r.stallIsCorrect ? "1" : "0"
            ));
        }
        return sb.ToString();
    }

    string Escape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }

    // ========================= 累積結果：results_8.csv =========================

    // 目錄：Editor -> Assets/Scripts/game_8；Build -> <persistentDataPath>/game_8
#if UNITY_EDITOR
    private static readonly string ResultsBaseDir = Path.Combine(Application.dataPath, "Scripts", "game_8");
#else
    private static readonly string ResultsBaseDir = Path.Combine(Application.persistentDataPath, "game_8");
#endif
    private const string ResultsFileName = "results_8.csv";
    private static string ResultsCsvPath => Path.Combine(ResultsBaseDir, ResultsFileName);
    private static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    /// <summary>
    /// 追加寫入 results_8.csv
    /// </summary>
    void AppendToResults13Csv(List<QARecord> list)
    {
        try
        {
            Directory.CreateDirectory(ResultsBaseDir);
            EnsureResultsCsvReady();

            using (var fs = new FileStream(ResultsCsvPath, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs, Utf8Bom))
            {
                foreach (var r in list)
                {
                    // 更新 CSV 欄位
                    sw.WriteLine(string.Join(",",
                        Escape(r.timeISO),
                        Escape(r.sceneName),
                        r.rtMs.ToString(CultureInfo.InvariantCulture),
                        r.finalCorrect ? "1" : "0",

                        Escape(r.categoryCorrect),
                        Escape(r.categoryChosen),
                        r.categoryIsCorrect ? "1" : "0",

                        Escape(r.floorCorrect),
                        Escape(r.floorChosen),
                        r.floorIsCorrect ? "1" : "0",

                        Escape(r.stallCorrectKey),
                        Escape(r.stallCorrectDisplay),
                        Escape(r.stallChosenKey),
                        Escape(r.stallChosenDisplay),
                        r.stallIsCorrect ? "1" : "0"
                    ));

                    string testId = FirebaseManager_Firestore.Instance.testId;
                    string levelIndex = "9"; // 用場景名稱當關卡索引
                    string correctOption = r.stallCorrectKey;
                    string chosenOption = r.stallChosenKey;
                    
                    var correctDict = new Dictionary<string, string> { { "option", correctOption } };
                    var chosenDict = new Dictionary<string, string> { { "option", chosenOption } };

                    FirebaseManager_Firestore.Instance.SaveLevelOptions(testId, levelIndex, correctDict, chosenDict);
                }
            }

            UnityEngine.Debug.Log($"[RunLogger] results_8.csv updated at: {ResultsCsvPath}");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[RunLogger] results_8.csv write FAILED: {e.Message}");
        }
    }

    const string CsvHeader = "timeISO,sceneName,rtMs,finalCorrect,categoryCorrect,categoryChosen,categoryIsCorrect,floorCorrect,floorChosen,floorIsCorrect,stallCorrectKey,stallCorrectDisplay,stallChosenKey,stallChosenDisplay,stallIsCorrect";

    void EnsureResultsCsvReady()
    {
        if (!File.Exists(ResultsCsvPath) || new FileInfo(ResultsCsvPath).Length == 0)
        {
            WriteResultsHeader();
            return;
        }

        if (!HasUtf8Bom(ResultsCsvPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(ResultsCsvPath);
                string text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false).GetString(bytes);

                using (var fs = new FileStream(ResultsCsvPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var sw = new StreamWriter(fs, Utf8Bom))
                {
                    sw.Write(text);
                }
                UnityEngine.Debug.Log("[RunLogger] Migrated results_8.csv to UTF-8 with BOM.");
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogWarning($"[RunLogger] CSV BOM migration failed: {ex.Message}");
            }
        }

        EnsureResultsHeaderExists();
    }

    void WriteResultsHeader()
    {
        using (var fs = new FileStream(ResultsCsvPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        using (var sw = new StreamWriter(fs, Utf8Bom))
        {
            sw.WriteLine(CsvHeader);
        }
    }

    void EnsureResultsHeaderExists()
    {
        try
        {
            using (var fs = new FileStream(ResultsCsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, detectEncodingFromByteOrderMarks: true))
            {
                string firstLine = sr.ReadLine();
                // 檢查新標頭中的關鍵欄位
                if (string.IsNullOrEmpty(firstLine) ||
                    !firstLine.Contains("timeISO") || !firstLine.Contains("stallIsCorrect"))
                {
                    string rest = sr.ReadToEnd();
                    using (var wfs = new FileStream(ResultsCsvPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    using (var sw = new StreamWriter(wfs, Utf8Bom))
                    {
                        sw.WriteLine(CsvHeader); // 寫入新標頭
                        if (!string.IsNullOrEmpty(firstLine)) sw.WriteLine(firstLine);
                        if (!string.IsNullOrEmpty(rest)) sw.Write(rest);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            UnityEngine.Debug.LogWarning($"[RunLogger] EnsureResultsHeaderExists warn: {ex.Message}");
        }
    }

    bool HasUtf8Bom(string path)
    {
        try
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length < 3) return false;
                int b1 = fs.ReadByte(), b2 = fs.ReadByte(), b3 = fs.ReadByte();
                return (b1 == 0xEF && b2 == 0xBB && b3 == 0xBF);
            }
        }
        catch { return false; }
    }
}