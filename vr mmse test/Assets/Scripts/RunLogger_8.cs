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

            // ★ 新增：累積寫入專案內（或在 Build 環境寫到 persistentDataPath）的 results_8.csv
            AppendToResults8Csv(_records);

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

    /// <summary>
    /// 追加寫入 results_8.csv（Editor：Assets/Scripts/results_8.csv；Build：persistentDataPath/results_8.csv）
    /// 不影響既有輸出。不存在時會自動建立並寫入表頭。
    /// </summary>
    void AppendToResults8Csv(List<QARecord> list)
    {
        try
        {
#if UNITY_EDITOR
            string dir = Path.Combine(Application.dataPath, "Scripts");
#else
            string dir = Application.persistentDataPath;
#endif
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            string path = Path.Combine(dir, "results_8.csv");
            bool newFile = !File.Exists(path);

            using (var sw = new StreamWriter(path, append: true, Encoding.UTF8))
            {
                if (newFile)
                {
                    sw.WriteLine("timeISO,sceneName,vpKey,displayText,userChoiceKey,correct,rtMs");
                }

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
                }
            }

            UnityEngine.Debug.Log($"[RunLogger] results_8.csv updated at: {path}");
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError($"[RunLogger] results_8.csv write FAILED: {e.Message}");
        }
    }
}
