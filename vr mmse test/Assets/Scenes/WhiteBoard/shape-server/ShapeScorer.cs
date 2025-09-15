// Assets/Scenes/WhiteBoard/shape-server/ShapeScorer.cs
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;   // 給 uGUI Text 用
using TMPro;            // 如果你用 TextMeshPro
using System.Collections;
using System.IO;          // ← 新增
#if UNITY_EDITOR
using UnityEditor;        // ← 為了自動刷新 Project 視窗
#endif

public class ShapeScorer : MonoBehaviour
{
    [Header("Server")]
    public string scoreUrl = "http://127.0.0.1:5000/score";

    [Header("Your Drawing Source（二選一）")]
    public RenderTexture drawingRT;
    public Texture2D drawingTex2D;

    [Header("Targets（可多張，二選一）")]
    public Texture2D[] targetTextures;
    public string[] targetFilePaths;

    [Header("Parameters（和 Python 對上）")]
    public string mode = "binary";   // "binary" | "edges"
    public float tau = 8f;
    public int side = 128;
    public float scanFrom = 0.85f, scanTo = 1.25f;
    public int scanN = 11;

    // 你指定的規則（和 server 一致）
    public float diaMin = 30f;
    public float diaLowFactor = 0.6f;
    

    [Header("Logs")]
    public bool showRawJson = false;
    public bool verboseLogs = true;

    [Header("Save On Judge (User Only)")]
    [Tooltip("勾選後，每次判斷會把『上傳的使用者繪圖』存到 Assets/Scenes/pics")]
    public bool saveUserOnJudge = false;

    [Tooltip("存檔檔名前綴")]
    public string saveFilePrefix = "user_";



    [System.Serializable] public class Details {
        public float chamfer;
        public float diamond;
        public float area_ratio;
        public float hu;
        public int quad;
        public float avg_d, d_ab, d_ba, best_scale;
    }
    [System.Serializable] public class ResultItem { public int index; public string name; public float score; public Details details; }
    [System.Serializable] public class ScoreResp { public int best_index; public float best_score; public ResultItem[] results; }

    [ContextMenu("Send For Score")]
    public void SendForScoreButton()
    {
        if (!Application.isPlaying) { Debug.LogWarning("請在 Play 模式下使用。"); return; }
        StartCoroutine(SendForScore());
    }

IEnumerator SendForScore()
{
byte[] userPng = CaptureUserPNG();
if (userPng == null) yield break;

// === 依設定存出你的繪圖（只存 user，不存 targets）到 Assets/scences/pics ===
if (saveUserOnJudge)
{
    try
    {
        string assetsPath = Application.dataPath; // 指到 Assets/
        string outDir = Path.Combine(assetsPath, "Scenes/pics");
        Directory.CreateDirectory(outDir);

        string ts = System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
        string fileName = $"{saveFilePrefix}{ts}.png";
        string fullPath = Path.Combine(outDir, fileName);

        File.WriteAllBytes(fullPath, userPng);
        Debug.Log($"[SaveOnJudge] 已保存你的繪圖：{fullPath}");

        #if UNITY_EDITOR
        AssetDatabase.Refresh(); // 讓檔案立刻出現在 Project 視窗
        #endif
    }
    catch (System.Exception e)
    {
        Debug.LogWarning($"[SaveOnJudge] 保存失敗：{e}");
    }
}



    WWWForm form = new WWWForm();
    form.AddBinaryData("user", userPng, "user.png", "image/png");

    // 和 server 欄位對齊
    form.AddField("mode", mode);
    form.AddField("tau", tau.ToString("0.#####"));
    form.AddField("side", side.ToString());
    form.AddField("scan_from", scanFrom.ToString("0.###"));
    form.AddField("scan_to", scanTo.ToString("0.###"));
    form.AddField("scan_n", scanN.ToString());
    form.AddField("dia_min", diaMin.ToString("0.###"));
    form.AddField("dia_low_factor", diaLowFactor.ToString("0.###"));

    if (targetTextures != null)
    {
        for (int i = 0; i < targetTextures.Length; i++)
        {
            var t = targetTextures[i];
            if (t == null) continue;
            byte[] png;
            try { png = t.EncodeToPNG(); }
            catch { var r = MakeReadable(t); png = r.EncodeToPNG(); Destroy(r); }
            form.AddBinaryData("targets", png, $"target_{i}.png", "image/png");
        }
    }
    if (targetFilePaths != null)
    {
        foreach (var path in targetFilePaths)
        {
            if (string.IsNullOrEmpty(path)) continue;
            if (!System.IO.File.Exists(path)) { Debug.LogWarning($"找不到目標檔案：{path}"); continue; }
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            form.AddBinaryData("targets", bytes, System.IO.Path.GetFileName(path), "image/png");
        }
    }

    using (UnityWebRequest req = UnityWebRequest.Post(scoreUrl, form))
    {
        req.timeout = 20;
        yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
        if (req.result != UnityWebRequest.Result.Success)
#else
        if (req.isNetworkError || req.isHttpError)
#endif
        {
            Debug.LogError($"[Score] HTTP {req.responseCode} {req.error}\n{req.downloadHandler.text}");
            yield break;
        }

        var json = req.downloadHandler.text;
        if (showRawJson) Debug.Log("[Score JSON]\n" + PrettyJson(json));

        var data = JsonUtility.FromJson<ScoreResp>(json);
        if (data == null || data.results == null || data.results.Length == 0)
        {
            Debug.LogError("[Score] 回傳內容異常。");
            yield break;
        }

        int bi = Mathf.Clamp(data.best_index, 0, data.results.Length - 1);
        string name = data.results[bi].name;
        float score = data.results[bi].score;
        Debug.Log($"[Score] 總分：{score:F1}（index={bi}, name={name}）");

        FindObjectOfType<ScoreUI>()?.UpdateScore(score);

        if (verboseLogs)
        {
            for (int i = 0; i < data.results.Length; i++)
            {
                var r = data.results[i];
                var d = r.details;
                string pen = (d != null && d.diamond < diaMin)
                    ? $" | penalty×{diaLowFactor:0.##} (diamond<{diaMin:0.#})"
                    : "";

                string line = $"[Score][{i}] {r.name}  總分 {r.score:F1}" +
                              (d==null ? "" :
                               $" | chamfer {d.chamfer:F1}" +
                               $" | diamond {d.diamond:F1}" +
                               $" | area {d.area_ratio:F3}" +
                               $" | hu {d.hu:F3}" +
                               $" | quad {d.quad}" +
                               $" | avg_d {d.avg_d:F2}" +
                               $" | d(A→T) {d.d_ab:F2}" +
                               $" | d(T→A) {d.d_ba:F2}" +
                               $" | best_scale {d.best_scale:F2}") +
                               pen;

                Debug.Log(line);
            }
        }
    }
}


    // -------- 擷取畫面/轉可讀 --------
    private byte[] CaptureUserPNG()
    {
        if (drawingRT != null)
        {
            Texture2D tex = CaptureFromRT(drawingRT);
            byte[] png = tex.EncodeToPNG();
            Destroy(tex);
            return png;
        }
        if (drawingTex2D != null)
        {
            try { return drawingTex2D.EncodeToPNG(); }
            catch { var r = MakeReadable(drawingTex2D); var png = r.EncodeToPNG(); Destroy(r); return png; }
        }
        Debug.LogError("請在 Inspector 指定 drawingRT 或 drawingTex2D 其中之一。");
        return null;
    }

    private static Texture2D CaptureFromRT(RenderTexture rt)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
        RenderTexture.active = prev;
        return tex;
    }

    private static Texture2D MakeReadable(Texture2D src)
    {
        var tmp = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(src, tmp);
        var prev = RenderTexture.active; RenderTexture.active = tmp;
        Texture2D tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0); tex.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(tmp);
        return tex;
    }

    // -------- JSON pretty print（沿用你原本的）--------
    private string PrettyJson(string json)
    {
        if (string.IsNullOrEmpty(json)) return json;
        int indent = 0; bool quoted = false;
        System.Text.StringBuilder sb = new System.Text.StringBuilder(json.Length * 2);
        for (int i = 0; i < json.Length; i++)
        {
            char ch = json[i];
            switch (ch)
            {
                case '{': case '[':
                    sb.Append(ch); if (!quoted) { sb.AppendLine(); sb.Append(new string(' ', ++indent * 2)); }
                    break;
                case '}': case ']':
                    if (!quoted) { sb.AppendLine(); sb.Append(new string(' ', --indent * 2)); }
                    sb.Append(ch); break;
                case '"':
                    sb.Append(ch);
                    bool escaped = false; int j = i;
                    while (j > 0 && json[--j] == '\\') escaped = !escaped;
                    if (!escaped) quoted = !quoted; break;
                case ',':
                    sb.Append(ch); if (!quoted) { sb.AppendLine(); sb.Append(new string(' ', indent * 2)); } break;
                case ':':
                    sb.Append(quoted ? ":" : ": "); break;
                default: sb.Append(ch); break;
            }
        }
        return sb.ToString();
    }

    // -------- 小工具：安全寫檔 + 時戳 --------
    private static void SafeWriteAllBytes(string path, byte[] data)
    {
        try { File.WriteAllBytes(path, data); }
        catch (System.Exception e) { Debug.LogWarning($"寫檔失敗：{path}\n{e}"); }
    }
    private static void SafeWriteAllText(string path, string text)
    {
        try { File.WriteAllText(path, text); }
        catch (System.Exception e) { Debug.LogWarning($"寫檔失敗：{path}\n{e}"); }
    }
    private static string TimeStamp() => System.DateTime.Now.ToString("yyyyMMdd_HHmmss_fff");
}
