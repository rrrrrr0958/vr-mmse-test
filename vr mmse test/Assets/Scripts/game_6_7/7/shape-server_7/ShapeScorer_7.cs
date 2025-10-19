// Assets/Scenes/WhiteBoard/shape-server/ShapeScorer.cs
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;   // 給 uGUI Text 用
using TMPro;            // 如果你用 TextMeshPro
using System.Collections;
using System.IO;
using Oculus.Platform.Models;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class ShapeScorer_7 : MonoBehaviour
{
    private FirebaseManager_Firestore FirebaseManager;
    [Header("Pass/Fail")]
    [Tooltip("大於此分數視為 1，否則 0")]
    public float passCutoff = 60f;

    [Header("Server")]
    public string scoreUrl = "http://127.0.0.1:5002/score";

    [Header("Your Drawing Source（二選一）")]
    public RenderTexture drawingRT;
    public Texture2D drawingTex2D;

    [Header("Targets（可多張，二選一）")]
    public Texture2D[] targetTextures;
    public string[] targetFilePaths;

    // -------- 與 Python 對齊（通用）--------
    [Header("Common Parameters（和 Python 對上）")]
    public string mode = "edges";    // "binary" | "edges"
    public float tau = 8f;
    public int side = 512;           // 建議 512：A 版推此值
    public float scanFrom = 0.90f, scanTo = 1.10f;
    public int scanN = 9;

    // -------- A版：洞（相交）檢測 相關參數 --------
    [Header("A版：洞檢測參數（和 Python 對上）")]
    [Tooltip("二值邊緣的輕微膨脹與封孔，讓輪廓閉合")]
    public int holeDilate = 3;
    public int holeClose = 3;

    [Tooltip("由 target 推估腰帶視窗高度比例（相對畫面高）")]
    public float waistBandFrac = 0.20f;

    [Tooltip("洞面積占整張影像的最小/最大比例")]
    public float holeAreaMinFrac = 0.0015f;
    public float holeAreaMaxFrac = 0.06f;

    [Tooltip("最小外接矩形的短/長比門檻（太扁則捨去）")]
    public float flatMin = 0.16f;

    [Tooltip("對角互補角度的容忍度（度）")]
    public float angleTol = 25.0f;

    [Tooltip("存在分數：中心靠腰線的權重 / 幾何形狀權重")]
    public float existCenterWeight = 0.40f;
    public float existShapeWeight = 0.60f;

    [Tooltip("近似四邊形的額外加分")]
    public float existQuadBonus = 0.10f;

    [Tooltip("存在分數門檻：低於此值視為『無菱形(無相交)』")]
    public float existThreshold = 15.0f;

    // -------- 總分融合 --------
    [Header("Score Blend")]
    [Tooltip("有相交時：final = chamferWeight * Chamfer + (1 - chamferWeight) * DiamondExist")]
    public float chamferWeight = 0.60f;

    [Tooltip("無相交時：final = Chamfer * noDiamondFactor")]
    public float noDiamondFactor = 0.55f;

    [Header("Logs")]
    public bool showRawJson = false;
    public bool verboseLogs = true;

    [Header("Save On Judge (User Only)")]
    [Tooltip("勾選後，每次判斷會把『上傳的使用者繪圖』存到 Assets/Scenes/pics")]
    public bool saveUserOnJudge = false;

    [Tooltip("存檔檔名前綴")]
    public string saveFilePrefix = "user_";

    // ------- 回傳資料模型（需與 server JSON 對齊）-------
    [System.Serializable]
    public class Details {
        public float chamfer;

        // A 版新增／取代舊 diamond 欄位
        public float diamond_exist;    // 0~100
        public bool has_diamond;       // true/false

        // Chamfer 細節
        public float avg_d, d_ab, d_ba, best_scale;

        // A 版洞檢測 debug 欄位（可為 null）
        public int[] waist_band;       // [y0, y1]
        public int[] hole_bbox;        // [x, y, w, h]
        public float hole_area_frac;
        public float flat;
        public float[] angles;         // 四角角度
        public int[] center_dist_px;   // [dx, dy]

        // --- 舊欄位的相容占位（若 server 仍回傳舊鍵不會崩，若無就維持預設值）---
        public float diamond;          // 舊：不再使用（server A版不會給）
        public float area_ratio;       // 舊：不再使用
        public float hu;               // 舊：不再使用
        public int   quad;             // 舊：不再使用
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

        // === 依設定存出你的繪圖（只存 user，不存 targets）到 Assets/Scenes/pics ===
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
                // ★★★ 新增：呼叫 VRTracker 存軌跡
                VRTracker1 tracker = FindFirstObjectByType<VRTracker1>();
                if (tracker != null)
                {
                    string csvPath = tracker.SaveTrajectoryToCsv();
                    Debug.Log($"🎯 已取得軌跡 CSV 路徑：{csvPath}");

                    byte[] csvData = File.ReadAllBytes(csvPath);
                    string testId = FirebaseManager_Firestore.Instance.testId;
                    string levelIndex = "1";

                    var files = new Dictionary<string, byte[]>();
                    files["trajectoryCsv"] = csvData;

                    FirebaseManager.UploadFilesAndSaveUrls(testId, levelIndex, files);
                }
                else
                {
                    Debug.LogWarning("[GM] 沒有找到 VRTracker 物件，無法保存軌跡。");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[SaveOnJudge] 保存失敗：{e}");
            }
        }

        WWWForm form = new WWWForm();
        form.AddBinaryData("user", userPng, "user.png", "image/png");

        // 和 server 欄位對齊（通用）
        form.AddField("mode", mode);
        form.AddField("tau", tau.ToString("0.#####"));
        form.AddField("side", side.ToString());
        form.AddField("scan_from", scanFrom.ToString("0.###"));
        form.AddField("scan_to", scanTo.ToString("0.###"));
        form.AddField("scan_n", scanN.ToString());

        // A 版：洞檢測參數
        form.AddField("hole_dilate", holeDilate.ToString());
        form.AddField("hole_close",  holeClose.ToString());
        form.AddField("waist_band_frac", waistBandFrac.ToString("0.###"));
        form.AddField("hole_area_min_frac", holeAreaMinFrac.ToString("0.#####"));
        form.AddField("hole_area_max_frac", holeAreaMaxFrac.ToString("0.#####"));
        form.AddField("flat_min", flatMin.ToString("0.#####"));
        form.AddField("angle_tol", angleTol.ToString("0.###"));
        form.AddField("exist_center_w", existCenterWeight.ToString("0.###"));
        form.AddField("exist_shape_w", existShapeWeight.ToString("0.###"));
        form.AddField("exist_quad_bonus", existQuadBonus.ToString("0.###"));
        form.AddField("exist_threshold", existThreshold.ToString("0.###"));

        // 總分融合
        form.AddField("chamfer_weight",   chamferWeight.ToString("0.###"));
        form.AddField("no_diamond_factor", noDiamondFactor.ToString("0.###"));

        // Targets
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
            int pass01 = score > passCutoff ? 1 : 0;
            Debug.Log($"[Score] 總分：{score:F1} | pass={pass01}（cutoff={passCutoff:F1}, index={bi}, name={name}）");


            FindObjectOfType<ScoreUI_7>()?.UpdateScore(score);

            string testId = FirebaseManager_Firestore.Instance.testId;
            string levelIndex = "1";
            FirebaseManager.SaveLevelData(testId, levelIndex, pass01);
            // 準備檔案字典（key 為你想在 firestore/storage 中標記的欄位名）
            var files = new Dictionary<string, byte[]>();
            files["userPng"] = userPng; // userPng 是你之前 CaptureUserPNG() 的 byte[]
            FirebaseManager.UploadFilesAndSaveUrls(testId, levelIndex, files);
            // 若 SceneFlowManager 沒掛，避免 NRE
            if (SceneFlowManager.instance != null)
                SceneFlowManager.instance.LoadNextScene();
            else
                Debug.LogWarning("[GM] SceneFlowManager.instance 為 null，略過切換場景");

            if (verboseLogs)
            {
                for (int i = 0; i < data.results.Length; i++)
                {
                    var r = data.results[i];
                    var d = r.details ?? new Details();

                    // A 版的懲罰邏輯：若 has_diamond==false，代表 server 端已用 noDiamondFactor 懲罰
                    string pen = (d.has_diamond ? "" : $" | penalty×{noDiamondFactor:0.##} (no diamond)");

                    string wb = (d.waist_band != null && d.waist_band.Length == 2)
                                ? $"[{d.waist_band[0]}, {d.waist_band[1]}]" : "[]";
                    string hb = (d.hole_bbox != null && d.hole_bbox.Length == 4)
                                ? $"[{d.hole_bbox[0]}, {d.hole_bbox[1]}, {d.hole_bbox[2]}, {d.hole_bbox[3]}]" : "[]";
                    string ang = (d.angles != null && d.angles.Length > 0)
                                ? string.Join(",", d.angles) : "";

                    string line =
                        $"[Score][{i}] {r.name}  總分 {r.score:F1}" +
                        $" | chamfer {d.chamfer:F1}" +
                        $" | diamond_exist {d.diamond_exist:F1}" +
                        $" | has_diamond {d.has_diamond}" +
                        $" | avg_d {d.avg_d:F2}" +
                        $" | d(A→T) {d.d_ab:F2}" +
                        $" | d(T→A) {d.d_ba:F2}" +
                        $" | best_scale {d.best_scale:F2}" +
                        $" | waist_band {wb}" +
                        $" | hole_bbox {hb}" +
                        $" | hole_area_frac {d.hole_area_frac:0.0000}" +
                        $" | flat {d.flat:0.000}" +
                        (string.IsNullOrEmpty(ang) ? "" : $" | angles {ang}") +
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
