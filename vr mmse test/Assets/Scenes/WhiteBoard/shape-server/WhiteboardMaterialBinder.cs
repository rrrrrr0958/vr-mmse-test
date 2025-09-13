using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// 將「材質上的貼圖」自動綁到 ShapeScorer：
/// - 白板：從材質/RawImage 取 RenderTexture → 指給 scorer.drawingRT
/// - 目標：把多個 Material 的貼圖轉成 Texture2D 陣列 → 指給 scorer.targetTextures
public class WhiteboardMaterialBinder : MonoBehaviour
{
    [Header("必填")]
    public ShapeScorer scorer;

    [Header("白板來源（擇一）")]
    public Renderer whiteboardRenderer;     // Quad/Plane/Mesh 上的材質
    public RawImage whiteboardRawImage;     // 如果你用 UI RawImage 顯示白板
    public string whiteboardTexProperty = "_MainTex"; // 或 URP Lit 的 "_BaseMap"

    [Header("目標材質")]
    public Material[] targetMaterials;      // 把你的目標圖所用的材質拖進來
    public string targetTexProperty = "_MainTex"; // 或 "_BaseMap"

    [Header("擷取選項")]
    public bool forceWhiteBackgroundForRT = false; // 若你的白板 RT 需要清成白底
    public Color clearColor = Color.white;

    void Awake()
    {
        // ★ 只在 Play 模式做綁定，避免 Editor 儲存暫時物件觸發 assert
        if (!Application.isPlaying) return;

        if (scorer == null)
        {
            #if UNITY_2023_1_OR_NEWER
scorer = Object.FindFirstObjectByType<ShapeScorer>(); // 或 FindAnyObjectByType 更快
#else
scorer = FindObjectOfType<ShapeScorer>();
#endif

            if (scorer == null) { Debug.LogError("找不到 ShapeScorer，請在 Inspector 指定。"); return; }
        }

        // —— 綁白板貼圖到 scorer.drawingRT / drawingTex2D ——
        Texture wbTex = null;
        if (whiteboardRenderer != null)
        {
            // ★ 用 sharedMaterial 避免產生 DontSave 的材質實例
            var mat = whiteboardRenderer.sharedMaterial;
            if (mat != null)
            {
                wbTex = mat.GetTexture(whiteboardTexProperty);
                if (wbTex == null) wbTex = mat.mainTexture;
            }
        }
        else if (whiteboardRawImage != null)
        {
            wbTex = whiteboardRawImage.texture;
        }

        if (wbTex is RenderTexture wbRT)
        {
            scorer.drawingRT = wbRT;
            if (forceWhiteBackgroundForRT) ClearRT(wbRT, clearColor);
            Debug.Log("[Binder] 白板來源：RenderTexture → 已指到 ShapeScorer.drawingRT");
        }
        else if (wbTex is Texture2D wbT2D)
        {
            scorer.drawingTex2D = wbT2D;
            Debug.Log("[Binder] 白板來源：Texture2D → 已指到 ShapeScorer.drawingTex2D");
        }
        else
        {
            Debug.LogWarning("[Binder] 白板貼圖不是 RT/Texture2D，請檢查 whiteboardTexProperty 名稱或材質設定。");
        }

        // —— 載入目標材質貼圖到 scorer.targetTextures ——
        var list = new List<Texture2D>();
        foreach (var m in targetMaterials)
        {
            if (!m) continue;
            Texture t = m.GetTexture(targetTexProperty);
            if (t == null) t = m.mainTexture;

            if (t is Texture2D t2d)
            {
                list.Add(TryMakeReadable(t2d)); // 不可讀時自動轉可讀
            }
            else if (t is RenderTexture rt)
            {
                list.Add(CaptureFromRT(rt));    // 目標若是 RT，擷取成 T2D
            }
            else
            {
                Debug.LogWarning($"[Binder] 目標材質 {m.name} 的貼圖不是 RT/T2D，略過。");
            }
        }

        scorer.targetTextures = list.ToArray();
        Debug.Log($"[Binder] 已載入目標圖數量：{scorer.targetTextures.Length}");
    }

    // —— 工具：把不可讀 T2D 轉可讀（避免一定要勾 Read/Write） ——
    private Texture2D TryMakeReadable(Texture2D src)
    {
        try { var _ = src.GetPixels32(); return src; } // 可讀
        catch
        {
            var tmp = RenderTexture.GetTemporary(src.width, src.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear);
            Graphics.Blit(src, tmp);
            var prev = RenderTexture.active; RenderTexture.active = tmp;
            var tex = new Texture2D(src.width, src.height, TextureFormat.RGBA32, false, false);
            tex.ReadPixels(new Rect(0,0,src.width,src.height), 0, 0);
            tex.Apply();
            RenderTexture.active = prev; RenderTexture.ReleaseTemporary(tmp);
            return tex;
        }
    }

    // —— 工具：將 RT 擷取成 Texture2D（目標陣列用） ——
    private Texture2D CaptureFromRT(RenderTexture rt)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;
        return tex;
    }

    // —— 工具：把 RT 清成指定顏色（可確保白底） ——
    private void ClearRT(RenderTexture rt, Color color)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, color);
        RenderTexture.active = prev;
    }
}
