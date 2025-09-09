using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;

public class WhiteboardChamferJudge : MonoBehaviour
{
    [Header("Input")]
    public RenderTexture whiteboardRT;   // 你的白板 RenderTexture
    public Texture2D templatePNG;        // 目標圖（黑線白底或透明底 PNG）

    [Tooltip("分析前先把圖等比壓到這個最長邊，越小越快")]
    public int analysisMaxSide = 512;

    [Header("Edge Extract")]
    public bool useCanny = true;
    [Range(0, 1)] public double alphaThreshold = 0.05;   // 非 Canny 時用
    [Range(0, 7)] public int dilateKernel = 3;           // 邊緣加粗避免斷線
    [Tooltip("Gaussian 消抖（玩家邊）")]
    public double blurSigmaUser = 1.6;
    [Tooltip("Gaussian 消抖（模板邊）")]
    public double blurSigmaTemplate = 1.2;
    [Tooltip("形態學 Close 修補小洞 (0=關閉)")]
    public int closeKernel = 0;

    [Header("Robustness / Tolerance")]
    [Tooltip("最低抖動容忍（像素）；實際容忍 = max(此值, 估計線寬*0.6)")]
    public int jitterTolerancePx = 4;

    [Range(0f, 1f), Tooltip("總分中：覆蓋率的權重（其餘給 Chamfer）")]
    public float coverageWeight = 0.7f;

    [Range(0.5f, 1.2f), Tooltip("分數曲線寬鬆度（<1 更寬鬆，>1 更嚴格）")]
    public float gamma = 0.85f;

    [Header("Debug UI (optional)")]
    public RawImage preview;   // 右下角疊圖（青=模板容忍帶、紅=玩家邊）
    public Text scoreText;     // 顯示分數/細節

    [ContextMenu("Evaluate Now")]
    public void EvaluateNow()
    {
        if (whiteboardRT == null || templatePNG == null)
        {
            Debug.LogWarning("[Judge] 請指定 Whiteboard RT 與 Template PNG");
            return;
        }

        // 讀入 Mat
        using var userBgra = CvUnityBridge.FromRenderTexture(whiteboardRT);
        using var templBgra = CvUnityBridge.FromTexture2D(templatePNG);

        // 等比縮圖到同一尺度（減少尺度差的影響）
        var (uSmall, _) = ShapeMatcher.ResizeToMaxSide(userBgra, analysisMaxSide);
        var (tSmall, _) = ShapeMatcher.ResizeToMaxSide(templBgra, analysisMaxSide);

        // 取邊緣（加入穩定化）
        using var uEdges = ShapeMatcher.ToEdges(uSmall, useCanny, alphaThreshold, dilateKernel, blurSigmaUser, closeKernel);
        using var tEdges = ShapeMatcher.ToEdges(tSmall, useCanny, alphaThreshold, dilateKernel, blurSigmaTemplate, closeKernel);

        // 動態容差：跟筆劃粗細一起放寬
        double sw   = ShapeMatcher.EstimateStrokeWidth(uEdges);
        int effTol  = Mathf.Max(jitterTolerancePx, Mathf.RoundToInt((float)(sw * 0.6f)));

        // 1) 覆蓋率（玩家邊緣落在模板膨脹帶內）—越大越好
        double coverage = ShapeMatcher.CoverageScore(uEdges, tEdges, effTol);   // 0..1

        // 2) 截斷 Chamfer（平均距離，像素）—越小越好 → 轉成 0..1 分
        double chamferPx  = ShapeMatcher.TruncatedChamfer(uEdges, tEdges, effTol); // 0..tau
        double chamferScore = 1.0 - Mathf.Clamp01((float)(chamferPx / (double)effTol)); // 0..1

        // 3) 加權 + γ 曲線，得到 0..100
        double combined = coverageWeight * coverage + (1 - coverageWeight) * chamferScore;
        double score01  = System.Math.Pow(Mathf.Clamp01((float)combined), gamma);
        int score100    = Mathf.RoundToInt((float)(score01 * 100.0));

        // 視覺化（青 = 模板容忍帶，紅 = 玩家邊）
        if (preview != null)
        {
            using var se = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(effTol * 2 + 1, effTol * 2 + 1));
            using var templBand = new Mat();
            Cv2.Dilate(tEdges, templBand, se);

            using var vis = new Mat(uEdges.Rows, uEdges.Cols, MatType.CV_8UC4, new Scalar(0, 0, 0, 0));
            // 青色帶
            vis.SetTo(new Scalar(255, 255, 0, 255), templBand);
            // 玩家紅邊
            using var userMask = new Mat();
            Cv2.Threshold(uEdges, userMask, 127, 255, ThresholdTypes.Binary);
            vis.SetTo(new Scalar(0, 0, 255, 255), userMask);

            CvUnityBridge.SetRawImage(preview, vis);
        }

        if (scoreText != null)
            scoreText.text = $"Score {score100}/100\nCoverage {coverage:0.00}, Chamfer {chamferPx:0.00}px (tol={effTol}px)";

        Debug.Log($"[Judge] Score={score100}/100, coverage={coverage:0.000}, chamfer={chamferPx:0.000}px, tol={effTol}px");

        uSmall.Dispose();
        tSmall.Dispose();
    }
}
