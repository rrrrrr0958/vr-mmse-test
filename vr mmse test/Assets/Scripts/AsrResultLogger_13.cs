using System.IO;
using System.Text;
using UnityEngine;

public static class AsrResultLogger
{
    // 會存到：<persistentDataPath>/AsrLogs/results.csv
    // Windows Editor 範例路徑：C:\Users\<你>\AppData\LocalLow\<CompanyName>\<ProductName>\AsrLogs\results.csv
    // Android/Quest：/sdcard/Android/data/<包名>/files/AsrLogs/results.csv（實際掛載受裝置影響）
    private static readonly string Dir = Path.Combine(Application.persistentDataPath, "AsrLogs");
    private static readonly string CsvPath = Path.Combine(Dir, "results.csv");

    public static void Append(string transcript, int score)
    {
        try
        {
            if (!Directory.Exists(Dir)) Directory.CreateDirectory(Dir);

            bool writeHeader = !File.Exists(CsvPath);
            using (var fs = new FileStream(CsvPath, FileMode.Append, FileAccess.Write, FileShare.Read))
            using (var sw = new StreamWriter(fs, Encoding.UTF8))
            {
                if (writeHeader)
                    sw.WriteLine("timestamp,score,transcript");

                // 基本 CSV 轉義：雙引號包裹並將內部雙引號重複
                string esc(string s) => "\"" + (s ?? "").Replace("\"", "\"\"") + "\"";

                string line = $"{esc(System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss.fff 'UTC'"))},{score},{esc(transcript)}";
                sw.WriteLine(line);
            }

            Debug.Log($"[ASR] Saved result → {CsvPath}\n[ASR] score={score}, transcript={transcript}");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"[ASR] Failed to write log: {ex.Message}");
        }
    }
}
