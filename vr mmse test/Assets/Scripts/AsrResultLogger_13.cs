using System;
using System.IO;
using System.Text;
using UnityEngine;

public static class AsrResultLogger
{
    // === 路徑設定：Editor 寫 Assets/Scripts；裝置上寫 persistentDataPath/Exports ===
    private static string BaseDir
    {
        get
        {
#if UNITY_EDITOR
            return Path.Combine(Application.dataPath, "Scripts");
#else
            return Path.Combine(Application.persistentDataPath, "Exports");
#endif
        }
    }

    private static string CsvPath => Path.Combine(BaseDir, "results_13.csv");

    // UTF-8 with BOM（Excel/記事本友好）
    private static readonly Encoding Utf8Bom = new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

    /// <summary>外部呼叫：新增一筆紀錄</summary>
    public static void Append(string transcript, int score, string wavPath = null)
    {
        try
        {
            Directory.CreateDirectory(BaseDir);

            EnsureCsvReady(); // 確保有 BOM + 表頭；必要時將舊檔改寫成有 BOM

            using (var fs = new FileStream(CsvPath, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs, Utf8Bom))
            {
                string ts = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
                sw.WriteLine($"{CsvEscape(ts)},{score},{CsvEscape(wavPath ?? "")},{CsvEscape(transcript ?? "")}");
            }

            // 可選：在 Console 確認路徑
            // Debug.Log($"[AsrResultLogger] CSV appended: {CsvPath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[AsrResultLogger] Append failed: {ex.Message}");
        }
    }

    /// <summary>保存 WAV 檔（Editor：Assets/Scripts；裝置：persistentDataPath/Exports）</summary>
    public static string SaveWav(byte[] wavBytes, string preferredName = null)
    {
        try
        {
            Directory.CreateDirectory(BaseDir);

            string fileName = string.IsNullOrEmpty(preferredName)
                ? $"record_{DateTime.Now:yyyyMMdd_HHmmssfff}.wav"
                : (preferredName.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ? preferredName : preferredName + ".wav");

            string fullPath = Path.Combine(BaseDir, SanitizeFileName(fileName));
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

    // ===== 內部工具 =====

    /// <summary>
    /// 確保 CSV 檔存在且為 UTF-8 with BOM，沒表頭就寫表頭。
    /// 若偵測到既有檔案「沒有 BOM」，會自動改寫成有 BOM 的 UTF-8。
    /// </summary>
    private static void EnsureCsvReady()
    {
        Directory.CreateDirectory(BaseDir);

        if (!File.Exists(CsvPath) || new FileInfo(CsvPath).Length == 0)
        {
            // 新檔：直接用 UTF-8 with BOM 建立並寫表頭
            using (var fs = new FileStream(CsvPath, FileMode.Create, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs, Utf8Bom))
            {
                sw.WriteLine("timestamp,score,wav_path,transcript");
            }
            return;
        }

        // 檔案存在：檢查是否有 BOM
        if (!HasUtf8Bom(CsvPath))
        {
            try
            {
                // 以目前檔案的「實際位元組」讀入，再以 UTF-8（不論有無亂碼）重寫為「UTF-8 with BOM」
                byte[] bytes = File.ReadAllBytes(CsvPath);
                string text;

                // 嘗試先用 UTF-8（無 BOM）解碼，不拋例外
                text = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: false).GetString(bytes);

                // 以 Create 覆蓋並寫回 UTF-8 with BOM
                using (var fs = new FileStream(CsvPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                using (var sw = new StreamWriter(fs, Utf8Bom))
                {
                    sw.Write(text);
                }

                Debug.Log("[AsrResultLogger] Migrated results.csv to UTF-8 with BOM.");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[AsrResultLogger] Failed to migrate CSV to UTF-8 BOM: {ex.Message}. Will continue appending with BOM.");
                // 即使遷移失敗，之後的 append 仍會用 BOM；建議手動刪除舊檔讓它重建。
            }
        }

        // 檢查首行是否表頭，沒有就補上
        EnsureHeaderExists();
    }

    private static void EnsureHeaderExists()
    {
        try
        {
            using (var fs = new FileStream(CsvPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var sr = new StreamReader(fs, detectEncodingFromByteOrderMarks: true))
            {
                string firstLine = sr.ReadLine();
                if (firstLine == null) // 空檔案
                {
                    WriteHeader();
                    return;
                }

                if (!firstLine.Contains("timestamp") || !firstLine.Contains("transcript"))
                {
                    // 沒有表頭：把原文保留下來，重寫加表頭
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
            Debug.LogWarning($"[AsrResultLogger] EnsureHeaderExists failed: {ex.Message}");
        }
    }

    private static void WriteHeader()
    {
        using (var fs = new FileStream(CsvPath, FileMode.Create, FileAccess.Write, FileShare.Read))
        using (var sw = new StreamWriter(fs, Utf8Bom))
        {
            sw.WriteLine("timestamp,score,wav_path,transcript");
        }
    }

    private static bool HasUtf8Bom(string path)
    {
        try
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                if (fs.Length < 3) return false;
                int b1 = fs.ReadByte();
                int b2 = fs.ReadByte();
                int b3 = fs.ReadByte();
                return (b1 == 0xEF && b2 == 0xBB && b3 == 0xBF);
            }
        }
        catch
        {
            return false;
        }
    }

    private static string CsvEscape(string s)
    {
        if (s == null) return "\"\"";
        s = s.Replace("\"", "\"\"");
        return $"\"{s}\"";
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }
}
