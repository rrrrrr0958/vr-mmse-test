using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;

public class ShapeScorer : MonoBehaviour
{
    [Header("Server Endpoints")]
    public string scoreUrl = "http://127.0.0.1:5000/score";
    public string triCheckUrl = "http://127.0.0.1:5000/check_tri2";

    [Header("Flow")]
    public bool requireTriCheckBeforeScore = true;
    [Tooltip("combo | ssim | chamfer | tm | hu")]
    public string scoreMethod = "combo";

    [Header("Weights (combo)")]
    public float wChamfer = 0.30f;
    public float wF1 = 0.50f;
    public float wSSIM = 0.20f;
    public float wTM = 0.00f;
    public float wHu = 0.15f;

    public enum SsimMode { edges, binary }
    public SsimMode ssimMode = SsimMode.edges;
    [Range(1, 5)] public int ssimScales = 3;

    [Header("Tuning (combo)")]
    public float chamferTauPxMin = 6.0f;
    public float chamferTauScale = 0.030f;
    public float f1TolPxMin = 3.0f;
    public float f1TolScale = 0.020f;
    public float oriTolDeg = 20.0f;
    public float oriPenaltyPer5 = 2.0f;
    public float oriPenaltyCap = 20.0f;
    public float decayRThreshold = 55.0f;
    public float decayRMin = 0.60f;
    public float decayPThreshold = 50.0f;
    public float decayPMin = 0.70f;

    [Header("Your Drawing Source（二選一）")]
    public RenderTexture drawingRT;
    public Texture2D drawingTex2D;

    [Header("Targets（可多張）")]
    public Texture2D[] targetTextures;
    public string[] targetFilePaths;

    [Header("Logs")]
    public bool verboseLogs = true;

    [Header("Auto run")]
    public bool autoTriCheckOnPlay = false;
    public bool autoScoreWithPrecheckOnPlay = false;

    // ---- Tri-check 回傳 ----
    [System.Serializable] public class Tri2Resp {
        public bool must_two;
        public string[] orientations;
        public bool orientation_ok;
        public bool vertical_order_ok;
        public float overlap_ratio;
        public bool overlap_ok;
        public bool overall_ok;
    }

    // ---- Score 回傳 ----
    [System.Serializable] public class Details {
        public float score, chamfer, template, ssim, hu, f1, precision, recall;
        public float w_chamfer, w_f1, w_ssim, w_tm, w_hu;
        public float penalty_orientation, penalty_overdraw, decay;
        public float nz_user, nz_target, angle_user, angle_target;
    }
    [System.Serializable] public class ResultItem { public int index; public string name; public float score; public Details details; }
    [System.Serializable] public class ScoreResp { public int best_index; public float best_score; public ResultItem[] results; }

    void Start()
    {
        if (autoTriCheckOnPlay) StartCoroutine(SendForTriCheck());
        if (autoScoreWithPrecheckOnPlay) StartCoroutine(SendForScoreWithPrecheck());
    }

    [ContextMenu("Check Two Triangles (only)")]
    public void SendForTriCheckButton() { if (!Application.isPlaying) { Debug.LogWarning("請在 Play 模式下使用。"); return; } StartCoroutine(SendForTriCheck()); }
    [ContextMenu("Send For Score (only)")]
    public void SendForScoreButton() { if (!Application.isPlaying) { Debug.LogWarning("請在 Play 模式下使用。"); return; } StartCoroutine(SendForScore()); }
    [ContextMenu("Score With Precheck (tri-check -> score)")]
    public void SendForScoreWithPrecheckButton() { if (!Application.isPlaying) { Debug.LogWarning("請在 Play 模式下使用。"); return; } StartCoroutine(SendForScoreWithPrecheck()); }

    // -------- Tri-check --------
    public IEnumerator SendForTriCheck()
    {
        byte[] userPng = CaptureUserPNG(); if (userPng == null) yield break;
        WWWForm form = new WWWForm(); form.AddBinaryData("user", userPng, "user.png", "image/png");
        using (var req = UnityWebRequest.Post(triCheckUrl, form))
        {
            req.timeout = 20; yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            { Debug.LogError($"[TriCheck] HTTP {req.responseCode} {req.error}\n{req.downloadHandler.text}"); yield break; }

            string json = req.downloadHandler.text;
            Debug.Log("[TriCheck] " + json);
            Tri2Resp data = JsonUtility.FromJson<Tri2Resp>(json);
            if (verboseLogs) PrintTriVerbose(data, json);
            Debug.Log($"[TriCheck] 兩三角：{data.must_two} | 方向OK：{data.orientation_ok} | 上下OK：{data.vertical_order_ok} | 交疊={data.overlap_ratio:F2} | 門檻OK：{data.overlap_ok} | 結論：{data.overall_ok}");
        }
    }

    // -------- 只評分 --------
    public IEnumerator SendForScore()
    {
        if (string.IsNullOrEmpty(scoreUrl)) { Debug.LogError("scoreUrl 未設定。"); yield break; }
        byte[] userPng = CaptureUserPNG(); if (userPng == null) yield break;

        WWWForm form = BuildScoreForm(userPng);
        using (UnityWebRequest req = UnityWebRequest.Post(scoreUrl, form))
        {
            req.timeout = 20; yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            { Debug.LogError($"[Score] HTTP {req.responseCode} {req.error}\n{req.downloadHandler.text}"); yield break; }

            string json = req.downloadHandler.text;
            Debug.Log("[Score] " + json);
            var data = JsonUtility.FromJson<ScoreResp>(json);
            if (verboseLogs) PrintScoreVerbose(data, json);
            if (data != null && data.results != null && data.results.Length > 0)
            {
                int bi = Mathf.Clamp(data.best_index, 0, data.results.Length - 1);
                string name = data.results[bi].name;
                Debug.Log($"[Score] 最佳：{data.best_score:F1} 分（index={bi}, name={name}）");
            }
        }
    }

    // -------- 先檢查再評分 --------
    public IEnumerator SendForScoreWithPrecheck()
    {
        byte[] userPng = CaptureUserPNG(); if (userPng == null) yield break;

        Tri2Resp tri = null;
        {   WWWForm f = new WWWForm(); f.AddBinaryData("user", userPng, "user.png", "image/png");
            using (var req = UnityWebRequest.Post(triCheckUrl, f))
            {   req.timeout = 20; yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
                if (req.result != UnityWebRequest.Result.Success)
#else
                if (req.isNetworkError || req.isHttpError)
#endif
                { Debug.LogError($"[TriCheck] HTTP {req.responseCode} {req.error}\n{req.downloadHandler.text}"); yield break; }
                string json = req.downloadHandler.text;
                tri = JsonUtility.FromJson<Tri2Resp>(json);
                if (verboseLogs) PrintTriVerbose(tri, json);
                Debug.Log($"[TriCheck] 結論：{tri.overall_ok}（兩三角：{tri.must_two}  方向OK：{tri.orientation_ok}  上下OK：{tri.vertical_order_ok}  交疊={tri.overlap_ratio:F2}）");
            }
        }

        if (requireTriCheckBeforeScore && (tri == null || !tri.overall_ok))
        { Debug.LogWarning("[Flow] 兩三角檢查未通過，取消評分流程。"); yield break; }

        WWWForm form = BuildScoreForm(userPng);
        using (UnityWebRequest req = UnityWebRequest.Post(scoreUrl, form))
        {
            req.timeout = 20; yield return req.SendWebRequest();
#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            { Debug.LogError($"[Score] HTTP {req.responseCode} {req.error}\n{req.downloadHandler.text}"); yield break; }

            string json = req.downloadHandler.text;
            Debug.Log("[Score] " + json);
            var data = JsonUtility.FromJson<ScoreResp>(json);
            if (verboseLogs) PrintScoreVerbose(data, json);
            if (data != null && data.results != null && data.results.Length > 0)
            {
                int bi = Mathf.Clamp(data.best_index, 0, data.results.Length - 1);
                string name = data.results[bi].name;
                Debug.Log($"[Score] 最佳：{data.best_score:F1} 分（index={bi}, name={name}）");
            }
        }
    }

    // -------- 表單建構 --------
    private WWWForm BuildScoreForm(byte[] userPng)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("user", userPng, "user.png", "image/png");
        string method = string.IsNullOrEmpty(scoreMethod) ? "combo" : scoreMethod.ToLower().Trim();
        form.AddField("method", method);

        if (method == "combo")
        {
            int scales = Mathf.Clamp(ssimScales, 1, 5);
            form.AddField("w_chamfer", wChamfer.ToString("0.#####"));
            form.AddField("w_f1",      wF1.ToString("0.#####"));
            form.AddField("w_ssim",    wSSIM.ToString("0.#####"));
            form.AddField("w_tm",      wTM.ToString("0.#####"));
            form.AddField("w_hu",      wHu.ToString("0.#####"));
            form.AddField("ssim_mode", ssimMode.ToString().ToLower());
            form.AddField("ssim_scales", scales.ToString());

            // 調參
            form.AddField("chamfer_tau_px_min", chamferTauPxMin.ToString("0.#####"));
            form.AddField("chamfer_tau_scale",  chamferTauScale.ToString("0.#####"));
            form.AddField("f1_tol_px_min",      f1TolPxMin.ToString("0.#####"));
            form.AddField("f1_tol_scale",       f1TolScale.ToString("0.#####"));
            form.AddField("ori_tol_deg",        oriTolDeg.ToString("0.#####"));
            form.AddField("ori_penalty_per5",   oriPenaltyPer5.ToString("0.#####"));
            form.AddField("ori_penalty_cap",    oriPenaltyCap.ToString("0.#####"));
            form.AddField("decay_r_threshold",  decayRThreshold.ToString("0.#####"));
            form.AddField("decay_r_min",        decayRMin.ToString("0.#####"));
            form.AddField("decay_p_threshold",  decayPThreshold.ToString("0.#####"));
            form.AddField("decay_p_min",        decayPMin.ToString("0.#####"));
        }

        if (targetTextures != null)
        {
            for (int i = 0; i < targetTextures.Length; i++)
            {
                var t = targetTextures[i];
                if (t == null) continue;
                byte[] tpng = null;
                try { tpng = t.EncodeToPNG(); }
                catch { var r = MakeReadable(t); tpng = r.EncodeToPNG(); Destroy(r); }
                form.AddBinaryData("targets", tpng, $"target_{i}.png", "image/png");
            }
        }
        if (targetFilePaths != null)
        {
            foreach (var path in targetFilePaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                if (!File.Exists(path)) { Debug.LogWarning($"找不到目標檔案：{path}"); continue; }
                byte[] tbytes = File.ReadAllBytes(path);
                form.AddBinaryData("targets", tbytes, Path.GetFileName(path), "image/png");
            }
        }
        return form;
    }

    // -------- 擷取 / 轉可讀 --------
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
            catch {
                Texture2D r = MakeReadable(drawingTex2D);
                byte[] png = r.EncodeToPNG();
                Destroy(r);
                return png;
            }
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
        RenderTexture.active = prev; return tex;
    }

    private static Texture2D MakeReadable(Texture2D src)
    {
        var tmp = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(src, tmp);
        var prev = RenderTexture.active; RenderTexture.active = tmp;
        Texture2D tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0); tex.Apply();
        RenderTexture.active = prev; RenderTexture.ReleaseTemporary(tmp); return tex;
    }

    // -------- 詳細輸出 --------
    private void PrintScoreVerbose(ScoreResp data, string rawJson)
    {
        Debug.Log("[Score JSON]\n" + PrettyJson(rawJson));
        if (data == null || data.results == null) return;
        Debug.Log($"[Score] 共 {data.results.Length} 個目標，最佳索引 {data.best_index}，最佳分數 {data.best_score:F1}");
        for (int i = 0; i < data.results.Length; i++)
        {
            var r = data.results[i];
            var d = r.details;
            string extra = "";
            if (d != null)
            {
                extra = $" | chamfer {d.chamfer:F1} / f1 {d.f1:F1} / ssim {d.ssim:F1} / hu {d.hu:F1} / tm {d.template:F1}";
                if (d.precision > 0 || d.recall > 0) extra += $" | P {d.precision:F1} / R {d.recall:F1}";
                if (d.penalty_orientation > 0 || d.penalty_overdraw > 0) extra += $" | pen(ori {d.penalty_orientation:F1}, over {d.penalty_overdraw:F1})";
                if (d.decay > 0 && d.decay < 1) extra += $" | decay×{d.decay:F2}";
                if (d.nz_user > 0 || d.nz_target > 0) extra += $" | nz U/T = {d.nz_user}/{d.nz_target}";
                if (d.angle_user != 0 || d.angle_target != 0) extra += $" | angle U/T = {d.angle_user:F1}/{d.angle_target:F1}";
            }
            Debug.Log($"[Score][{i}] {r.name}  總分 {r.score:F1}{extra}");
        }
    }

    private void PrintTriVerbose(Tri2Resp data, string rawJson)
    {
        Debug.Log("[TriCheck JSON]\n" + PrettyJson(rawJson));
        if (data == null) return;
        var ori = (data.orientations != null) ? string.Join(",", data.orientations) : "(null)";
        Debug.Log($"[TriCheck] must_two={data.must_two} | orientations=[{ori}] | orientation_ok={data.orientation_ok} | vertical_order_ok={data.vertical_order_ok} | overlap={data.overlap_ratio:F3} | overlap_ok={data.overlap_ok} | overall_ok={data.overall_ok}");
    }

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
}
