using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using System.IO;

public class ShapeScorer : MonoBehaviour
{
    [Header("Server")]
    public string serverUrl = "http://127.0.0.1:5000/score";

    [Header("Your Drawing Source (二選一)")]
    public RenderTexture drawingRT;   // 你的白板 RenderTexture
    public Texture2D drawingTex2D;    // 若你是直接畫在 Texture2D 上

    [Header("Targets (可多張)")]
    public Texture2D[] targetTextures;   // 目標圖（專案內 Texture2D）
    public string[] targetFilePaths;     // 或磁碟 PNG 路徑（如 StreamingAssets/t1.png）

    [ContextMenu("Send For Score (Context Menu)")]
    public void SendForScoreMenu()
    {
        if (!Application.isPlaying) {
            Debug.LogWarning("請在 Play 模式下使用送分。");
            return;
        }
        StartCoroutine(SendForScore());
    }

    // 給 UI Button 用
    public void SendForScoreButton()
    {
        if (!Application.isPlaying) {
            Debug.LogWarning("請在 Play 模式下使用送分。");
            return;
        }
        StartCoroutine(SendForScore());
    }

    public IEnumerator SendForScore()
    {
        // 1) 準備你的繪圖 PNG bytes
        byte[] userPng = null;

        if (drawingRT != null)
        {
            Texture2D tex = CaptureFromRT(drawingRT);   // 從 RT 擷取
            userPng = tex.EncodeToPNG();
            Destroy(tex);
        }
        else if (drawingTex2D != null)
        {
            try {
                userPng = drawingTex2D.EncodeToPNG();
            }
            catch {
                Texture2D readable = MakeReadable(drawingTex2D);
                userPng = readable.EncodeToPNG();
                Destroy(readable);
            }
        }
        else
        {
            Debug.LogError("請在 Inspector 指定 drawingRT 或 drawingTex2D 其中之一。");
            yield break;
        }

        // 2) 組 multipart/form-data
        WWWForm form = new WWWForm();
        form.AddBinaryData("user", userPng, "user.png", "image/png");

        // 2a) 專案內的目標圖（Texture2D）
        if (targetTextures != null)
        {
            for (int i = 0; i < targetTextures.Length; i++)
            {
                var t = targetTextures[i];
                if (t == null) continue;

                byte[] tpng = null;
                try {
                    tpng = t.EncodeToPNG(); // 需要 Read/Write
                }
                catch {
                    Texture2D readable = MakeReadable(t); // 臨時轉可讀
                    tpng = readable.EncodeToPNG();
                    Destroy(readable);
                }

                form.AddBinaryData("targets", tpng, $"target_{i}.png", "image/png");
            }
        }

        // 2b) 磁碟上的目標圖（例如 StreamingAssets）
        if (targetFilePaths != null)
        {
            foreach (var path in targetFilePaths)
            {
                if (string.IsNullOrEmpty(path)) continue;
                if (!File.Exists(path)) { Debug.LogWarning($"找不到目標檔案：{path}"); continue; }

                byte[] tbytes = File.ReadAllBytes(path);
                string fname = Path.GetFileName(path);
                form.AddBinaryData("targets", tbytes, fname, "image/png");
            }
        }

        // 3) 送出 POST
        using (UnityWebRequest req = UnityWebRequest.Post(serverUrl, form))
        {
            req.timeout = 20;
            yield return req.SendWebRequest();

#if UNITY_2020_2_OR_NEWER
            if (req.result != UnityWebRequest.Result.Success)
#else
            if (req.isNetworkError || req.isHttpError)
#endif
            {
                Debug.LogError($"HTTP Error: {req.responseCode} {req.error}\n{req.downloadHandler.text}");
                yield break;
            }

            string json = req.downloadHandler.text;
            Debug.Log($"Score response: {json}");
            // 需要可再用 JsonUtility / Newtonsoft.Json 解析顯示
        }
    }

    // ====== 工具：從 RenderTexture 擷取為可讀 Texture2D ======
    private static Texture2D CaptureFromRT(RenderTexture rt)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        RenderTexture.active = prev;
        return tex;
    }

    // ====== 工具：將不可讀的 Texture2D 臨時轉為可讀 ======
    private static Texture2D MakeReadable(Texture2D src)
    {
        var tmp = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
        Graphics.Blit(src, tmp);

        var prev = RenderTexture.active;
        RenderTexture.active = tmp;

        Texture2D tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, src.width, src.height), 0, 0);
        tex.Apply();

        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(tmp);
        return tex;
    }
}
