using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class AsrResultLogger
{
    // === 目錄規範 ===
    // Editor：Assets/Scripts/game_13
    // Build（Quest/Android/Standalone Player）：<persistentDataPath>/game_13
#if UNITY_EDITOR
    private static readonly string BaseDir = Path.Combine(Application.dataPath, "Scripts", "game_13");
#else
    private static readonly string BaseDir = Path.Combine(Application.persistentDataPath, "game_13");
#endif

    private const string CsvFileName = "results_13.csv";
    private static string CsvPath => Path.Combine(BaseDir, CsvFileName);

    // Excel/記事本友好：UTF-8 with BOM
    private static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    public static string GetOutputDirectory() => BaseDir;

    /// 保存此次錄音的 WAV 檔到指定目錄；回傳完整路徑
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

    /// 追加一筆結果到 CSV（自動建立資料夾、檔案、表頭；舊檔會自動轉 BOM）
    /// 欄位：timestamp,score,wav_path,transcript
    public static void Append(string transcript, int score, string wavPath = null)
    {
        try
        {
            Directory.CreateDirectory(BaseDir);
            EnsureCsvReady();

            using (var fs = new FileStream(CsvPath, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs, Utf8Bom))
            {
                string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                sw.WriteLine($"{CsvEscape(ts)},{score},{CsvEscape(wavPath ?? "")},{CsvEscape(transcript ?? "")}");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AsrResultLogger] Append failed: {ex.Message}");
        }
    }

    // ===== 內部 =====

    private static void EnsureCsvReady()
    {
        if (!File.Exists(CsvPath) || new FileInfo(CsvPath).Length == 0)
        {
            WriteHeader();
            return;
        }

        // 舊檔轉為 UTF-8 with BOM（一次性）
        if (!HasUtf8Bom(CsvPath))
        {
            try
            {
                byte[] bytes = File.ReadAllBytes(CsvPath);
                string text = new UTF8Encoding(false, false).GetString(bytes);

                using (var fs = new FileStream(CsvPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var sw = new StreamWriter(fs, Utf8Bom))
                {
                    sw.Write(text);
                }
                Debug.Log("[AsrResultLogger] Migrated results_13.csv to UTF-8 with BOM.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AsrResultLogger] CSV BOM migration failed: {ex.Message}");
            }
        }

        // 無表頭就補上
        EnsureHeaderExists();
    }

    private static void WriteHeader()
    {
        using (var fs = new FileStream(CsvPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        using (var sw = new StreamWriter(fs, Utf8Bom))
        {
            sw.WriteLine("timestamp,score,wav_path,transcript");
        }
    }

    private static void EnsureHeaderExists()
    {
        try
        {
            using (var fs = new FileStream(CsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, detectEncodingFromByteOrderMarks: true))
            {
                string firstLine = sr.ReadLine();
                if (string.IsNullOrEmpty(firstLine) ||
                    !firstLine.Contains("timestamp") || !firstLine.Contains("transcript"))
                {
                    string rest = sr.ReadToEnd();
                    using (var wfs = new FileStream(CsvPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    using (var sw = new StreamWriter(wfs, Utf8Bom))
                    {
                        sw.WriteLine("timestamp,score,wav_path,transcript");
                        if (!string.IsNullOrEmpty(firstLine)) sw.WriteLine(firstLine);
                        if (!string.IsNullOrEmpty(rest)) sw.Write(rest);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[AsrResultLogger] EnsureHeaderExists warn: {ex.Message}");
        }
    }

    private static bool HasUtf8Bom(string path)
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

    private static string CsvEscape(string s)
    {
        if (s == null) return "\"\"";
        return "\"" + s.Replace("\"", "\"\"") + "\"";
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars()) name = name.Replace(c, '_');
        return name;
    }
}
