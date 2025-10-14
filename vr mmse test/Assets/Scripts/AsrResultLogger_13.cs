using System;
using System.IO;
using System.Text;
using UnityEngine;
using System.Collections.Generic;

public static class AsrResultLogger
{
    // === 儲存結果的結構體 (JSON 格式) ===
    [Serializable]
    public class LogEntry
    {
        public string timestamp;
        public int score;
        public string wav_path;
        public string transcript;
        public string error;
        public AsrClient.Reasons reasons;
    }
    
    // JSON 陣列的容器
    [Serializable]
    private class LogContainer { public List<LogEntry> entries = new List<LogEntry>(); }

    // === 目錄規範 ===
#if UNITY_EDITOR
    private static readonly string BaseDir = Path.Combine(Application.dataPath, "Scripts", "game_13");
#else
    private static readonly string BaseDir = Path.Combine(Application.persistentDataPath, "game_13");
#endif

    private const string JsonFileName = "results_13.json";
    private static string JsonPath => Path.Combine(BaseDir, JsonFileName);

    public static string GetOutputDirectory() => BaseDir;

    /// <summary>
    /// 保存此次錄音的 WAV 檔到指定目錄；回傳完整路徑
    /// </summary>
    public static string SaveWav(byte[] wavBytes, string preferredName = null)
    {
        try
        {
            Directory.CreateDirectory(BaseDir);

            string fileName = string.IsNullOrEmpty(preferredName)
                ? $"record_{DateTime.Now:yyyyMMdd_HHmmssfff}.wav"
                : (preferredName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? preferredName : preferredName + ".wav");

            fileName = SanitizeFileName(fileName);
            string fullPath = Path.Combine(BaseDir, fileName);

            File.WriteAllBytes(fullPath, wavBytes);
            Debug.Log($"[AsrResultLogger] WAV saved: {fullPath}");
            return fullPath;
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AsrResultLogger] SaveWav failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// 【新功能】追加一筆結果到 JSON 檔案
    /// </summary>
    public static void AppendJson(AsrClient.GoogleASRResponse response, string wavPath = null)
    {
        try
        {
            Directory.CreateDirectory(BaseDir);
            
            // 1. 讀取現有 JSON 內容
            LogContainer container = new LogContainer();
            if (File.Exists(JsonPath))
            {
                string jsonText = File.ReadAllText(JsonPath);
                if (!string.IsNullOrEmpty(jsonText))
                {
                    // 為了避免讀取整個陣列的麻煩，我們用包裝器來讀取
                    container = JsonUtility.FromJson<LogContainer>(jsonText) ?? new LogContainer();
                }
            }
            
            // 2. 建立新的紀錄項目
            LogEntry newEntry = new LogEntry
            {
                timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                score = response.score,
                wav_path = wavPath ?? "",
                transcript = response.Text ?? "",
                error = response.error,
                reasons = response.reasons
            };
            
            // 3. 加入新項目
            container.entries.Add(newEntry);

            // 4. 寫回 JSON 檔案
            string finalJson = JsonUtility.ToJson(container, true); // true 表示美化輸出
            File.WriteAllText(JsonPath, finalJson);

            Debug.Log($"[AsrResultLogger] JSON appended: {JsonPath}, score: {response.score}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AsrResultLogger] AppendJson failed: {ex.Message}");
        }
    }

    // ===== 內部工具函式 (僅保留 SanitizeFileName) =====

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }
}