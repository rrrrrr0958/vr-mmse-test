using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;

public class WhiteboardChamferJudge : MonoBehaviour
{
    [Header("Input")]
    public RenderTexture whiteboardRT;     // 你在畫的那塊 RT
    public Texture2D templatePNG;          // 目標圖（PNG）

    [Tooltip("白板/模板最大邊縮到這個尺寸後再比，越小越快")]
    public int analysisMaxSide = 512;

    [Header("Edge Extract")]
    public bool useCanny = true;
    [Range(0, 1)] public double alphaThreshold = 0.05; // 非 Canny 時的灰階閾值
    [Range(0, 7)] public int dilateKernel = 3;         // 連線粗一點避免斷

    [Header("Search Range")]
    public float scaleMin = 0.90f;
    public float scaleMax = 1.10f;
    public float scaleStep = 0.05f;

    public float angleMin = -10f;
    public float angleMax = 10f;
    public float angleStep = 2f;

    [Header("Debug Output (optional)")]
    public RawImage preview;     // 疊圖顯示（紅=玩家、青=模板、白=重疊）
    public Text scoreText;       // 分數顯示（可用 Text 或 TextMeshProUGUI 改型別）

    // 讓你可以在 Inspector 右鍵快速觸發
    [ContextMenu("Evaluate Now")]
    public void EvaluateNow()
    {
        if (whiteboardRT == null || templatePNG == null)
        {
            Debug.LogWarning("[Judge] 請指定 Whiteboard RT 與 Template PNG");
            return;
        }

        // 讀 Mat
        using var userBgra = CvUnityBridge.FromRenderTexture(whiteboardRT);
        using var templBgra = CvUnityBridge.FromTexture2D(templatePNG);

        // 等比縮圖（先把兩張都壓到 maxSide）
        var (uSmall, uScale) = ShapeMatcher.ResizeToMaxSide(userBgra, analysisMaxSide);
        var (tSmall, tScale) = ShapeMatcher.ResizeToMaxSide(templBgra, analysisMaxSide);

        // 取邊緣
        using var uEdges = ShapeMatcher.ToEdges(uSmall, useCanny, alphaThreshold, dilateKernel);
        using var tEdges = ShapeMatcher.ToEdges(tSmall, useCanny, alphaThreshold, dilateKernel);

        // 搜尋最佳縮放/角度（以模板對齊玩家）
        var (sim, ang, sc, shift, overlay) =
            ShapeMatcher.MatchByChamfer(
                uEdges, tEdges,
                scaleMin, scaleMax, scaleStep,
                angleMin, angleMax, angleStep
            );

        // 顯示
        if (preview != null && overlay != null)
            CvUnityBridge.SetRawImage(preview, overlay);

        if (scoreText != null)
            scoreText.text = $"Similarity = {sim:0.000} (angle={ang:0}°, scale={sc:0.00})";

        Debug.Log($"[Judge] Similarity={sim:0.000}, angle={ang:0.0} deg, scale={sc:0.000}, shift=({shift.X:0},{shift.Y:0})");

        overlay?.Dispose();
        uSmall.Dispose(); tSmall.Dispose();
    }
}
