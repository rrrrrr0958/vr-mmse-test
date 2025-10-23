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
        public string timeISO;
        public string sceneName;
        public string vpKey;          // 正解 vpName
        public string displayText;    // 題目顯示（正解描述）
        public string userChoiceKey;  // 使用者選的 vpName（空字串=未選）
        public bool correct;
        public int rtMs;              // 反應時間（毫秒）
    }

    string _runId;
    readonly List<QARecord> _records = new();
    public string CurrentTargetKey { get; private set; }
    string _displayTextCache;

    // 自動 RT 計時
    Stopwatch _questionTimer;

    /// <summary>（可選）設定當前題目的顯示文字快取。</summary>
    public void SetDisplayTextCache(string displayText) => _displayTextCache = displayText;

    /// <summary>開始一次新紀錄。</summary>
    public void StartRun()
    {
        _runId = DateTime.Now.ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
        _records.Clear();
        _questionTimer = null;
        UnityEngine.Debug.Log($"[RunLogger] StartRun id={_runId}  path={Application.persistentDataPath}/runs");
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
    /// 結束一題。若 rtMs <= 0，會以內部碼錶時間代入。
    /// userChoiceKey 建議填選項對應的 vpName；若不知道可傳空字串。
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
            vpKey = CurrentTargetKey,
            displayText = _displayTextCache,
            userChoiceKey = userChoiceKey ?? "",
            correct = correct,
            rtMs = rt
        };

        _records.Add(rec);

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

        // 準備統計
        float accuracy = _records.Count > 0
            ? _records.Count(r => r.correct) / (float)_records.Count
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

            // ★ 新增/修改：累積寫入 results_8.csv（Editor: Assets/Scripts/game_8；Build: persistentDataPath/game_8）
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
        sb.AppendLine("timeISO,sceneName,vpKey,displayText,userChoiceKey,correct,rtMs");
        foreach (var r in list)
        {
            sb.AppendLine(string.Join(",",
                Escape(r.timeISO),
                Escape(r.sceneName),
                Escape(r.vpKey),
                Escape(r.displayText),
                Escape(r.userChoiceKey),
                r.correct ? "1" : "0",
                r.rtMs.ToString(CultureInfo.InvariantCulture)
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
    /// 追加寫入 results_8.csv（Editor：Assets/Scripts/game_8；Build：persistentDataPath/game_8）
    /// 不影響既有輸出。不存在時會自動建立與寫入表頭；舊檔若無 BOM 會自動轉換為 UTF-8 with BOM。
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
                    sw.WriteLine(string.Join(",",
                        Escape(r.timeISO),
                        Escape(r.sceneName),
                        Escape(r.vpKey),
                        Escape(r.displayText),
                        Escape(r.userChoiceKey),
                        r.correct ? "1" : "0",
                        r.rtMs.ToString(CultureInfo.InvariantCulture)
                    ));

                    string testId = FirebaseManager_Firestore.Instance.testId;
                    string levelIndex = "9"; // 用場景名稱當關卡索引
                    string correctOption = r.vpKey;
                    string chosenOption = r.userChoiceKey;
                    
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

    void EnsureResultsCsvReady()
    {
        if (!File.Exists(ResultsCsvPath) || new FileInfo(ResultsCsvPath).Length == 0)
        {
            WriteResultsHeader();
            return;
        }

        // 舊檔轉為 UTF-8 with BOM（一次性）
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
            sw.WriteLine("timeISO,sceneName,vpKey,displayText,userChoiceKey,correct,rtMs");
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
                if (string.IsNullOrEmpty(firstLine) ||
                    !firstLine.Contains("timeISO") || !firstLine.Contains("rtMs"))
                {
                    string rest = sr.ReadToEnd();
                    using (var wfs = new FileStream(ResultsCsvPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    using (var sw = new StreamWriter(wfs, Utf8Bom))
                    {
                        sw.WriteLine("timeISO,sceneName,vpKey,displayText,userChoiceKey,correct,rtMs");
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
