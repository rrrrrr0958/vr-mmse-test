using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System;
using System.Text;
using System.Linq;

public class RunLogger : MonoBehaviour {
    [Serializable]
    public class QARecord {
        public string timeISO;
        public string sceneName;
        public string vpKey;         // 正解 vpName
        public string displayText;   // 題目顯示（正解描述）
        public string userChoiceKey; // 使用者選的 vpName（空字串=我不知道/未選）
        public bool correct;
        public int rtMs;
    }

    string runId;
    readonly List<QARecord> records = new();
    public string CurrentTargetKey { get; private set; }
    string displayTextCache;

    public void SetDisplayTextCache(string displayText) => displayTextCache = displayText;

    public void StartRun() {
        runId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        records.Clear();
        Debug.Log($"[RunLogger] StartRun id={runId}");
    }

    public void BeginQuestion(string sceneName, string vpKey, string displayText) {
        CurrentTargetKey = vpKey;
        displayTextCache = displayText;
    }

    public void EndQuestion(string userChoiceKey, bool correct, int rtMs) {
        var rec = new QARecord {
            timeISO      = DateTime.Now.ToString("o"),
            sceneName    = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
            vpKey        = CurrentTargetKey,
            displayText  = displayTextCache,
            userChoiceKey= userChoiceKey,
            correct      = correct,
            rtMs         = rtMs
        };
        records.Add(rec);
        displayTextCache = null;
        CurrentTargetKey = null;
    }

    public void EndRun() {
        if (string.IsNullOrEmpty(runId) || records.Count == 0) return;

        var acc = records.Count > 0 ? records.Count(r => r.correct) / (float)records.Count : 0f;
        var avgRt = records.Where(r => r.rtMs >= 0).DefaultIfEmpty().Average(r => r?.rtMs ?? 0);

        var folder = Application.persistentDataPath + "/runs";
        Directory.CreateDirectory(folder);

        var baseName = $"{folder}/run_{runId}";
        File.WriteAllText(baseName + ".csv", ToCSV(records), Encoding.UTF8);
        File.WriteAllText(baseName + ".json", JsonUtility.ToJson(new Wrapper{ items = records, accuracy=acc, avgRtMs=(int)avgRt }, true), Encoding.UTF8);

        Debug.Log($"[RunLogger] Saved: {baseName}.csv / .json  (acc={acc:P1}, avgRT={avgRt:0} ms)");

        runId = null;
        records.Clear();
    }

    [Serializable] class Wrapper {
        public List<QARecord> items;
        public float accuracy;
        public int avgRtMs;
    }

    string ToCSV(List<QARecord> list) {
        var sb = new StringBuilder();
        sb.AppendLine("timeISO,sceneName,vpKey,displayText,userChoiceKey,correct,rtMs");
        foreach (var r in list) {
            sb.AppendLine(string.Join(",",
                Escape(r.timeISO),
                Escape(r.sceneName),
                Escape(r.vpKey),
                Escape(r.displayText),
                Escape(r.userChoiceKey),
                r.correct ? "1" : "0",
                r.rtMs.ToString()
            ));
        }
        return sb.ToString();
    }
    string Escape(string s) {
        if (s == null) return "";
        if (s.Contains(",") || s.Contains("\"") || s.Contains("\n"))
            return "\"" + s.Replace("\"", "\"\"") + "\"";
        return s;
    }
}
