using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

/// <summary>
/// 將「材質上的貼圖」自動綁到 ShapeScorer：
/// - 白板：從材質/RawImage 取 RenderTexture → 指給 scorer.drawingRT
/// - 目標：把多個 Material 的貼圖轉成 Texture2D 陣列 → 指給 scorer.targetTextures
/// </summary>
public class WhiteboardMaterialBinder_7 : MonoBehaviour
{
    [Header("必填")]
    public ShapeScorer_7 scorer;

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
        // 只在 Play 綁定，避免 Editor 儲存暫時物件觸發 assert
        if (!Application.isPlaying) return; 

        if (scorer == null)
        {
            // 使用新的 API 替代過時的 FindObjectOfType
            #if UNITY_2023_1_OR_NEWER
            scorer = Object.FindFirstObjectByType<ShapeScorer_7>();
            #else
            scorer = Object.FindObjectOfType<ShapeScorer_7>();
            #endif

            if (scorer == null) 
            { 
                Debug.LogError("找不到 ShapeScorer，請在 Inspector 指定。"); 
                return; 
            }
        }

        Texture wbTex = null;
        if (whiteboardRenderer != null)
        {
            var mat = whiteboardRenderer.sharedMaterial; // 使用 sharedMaterial，不會產生 kDontSaveInEditor 實例
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
        }
        else if (wbTex is Texture2D wbT2D)
        {
            scorer.drawingTex2D = wbT2D;
        }

        var list = new List<Texture2D>();
        foreach (var m in targetMaterials)
        {
            if (!m) continue;
            var t = m.GetTexture(targetTexProperty); 
            if (t == null) t = m.mainTexture;

            if (t is Texture2D t2d) 
            {
                // 檢查是否為可讀寫的貼圖
                try 
                {
                    var _ = t2d.GetPixels(); // 測試是否可讀
                    list.Add(t2d);
                }
                catch 
                {
                    // 如果不可讀，轉換為可讀的貼圖
                    list.Add(MakeReadable(t2d));
                }
            }
            else if (t is RenderTexture rt) list.Add(CaptureFromRT(rt));
        }
        
        scorer.targetTextures = list.ToArray();
    }

    // —— 工具：將 RT 擷取成 Texture2D（目標陣列用） ——
    private Texture2D CaptureFromRT(RenderTexture rt)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;

        Texture2D tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        RenderTexture.active = prev;
        return tex;
    }

    // —— 工具：把不可讀的 Texture2D 臨時轉為可讀 ——
    private Texture2D MakeReadable(Texture2D src)
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

    // —— 工具：把 RT 清成指定顏色（可確保白底） ——
    private void ClearRT(RenderTexture rt, Color color)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, color);
        RenderTexture.active = prev;
    }
}