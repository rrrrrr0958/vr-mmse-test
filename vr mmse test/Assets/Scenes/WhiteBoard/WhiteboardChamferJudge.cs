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

    [Header("Structure Judge")]
    [Tooltip("筆劃太短（像素）直接 0 分")]
    public int minStrokePx = 100;

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

        // 等比縮圖到同一尺度
        var (uSmall, _) = ShapeMatcher.ResizeToMaxSide(userBgra, analysisMaxSide);
        var (tSmall, _) = ShapeMatcher.ResizeToMaxSide(templBgra, analysisMaxSide);

        // 取邊緣
        using var uEdges = ShapeMatcher.ToEdges(uSmall, useCanny, alphaThreshold, dilateKernel, blurSigmaUser, closeKernel);
        using var tEdges = ShapeMatcher.ToEdges(tSmall, useCanny, alphaThreshold, dilateKernel, blurSigmaTemplate, closeKernel);

        // 動態容差
        double sw   = ShapeMatcher.EstimateStrokeWidth(uEdges);
        int effTol  = Mathf.Max(jitterTolerancePx, Mathf.RoundToInt((float)(sw * 0.6f)));

        // --- 新評分流程 ---

        // 1. 主結構 Hu-moments（填滿後正規化）
        using var uNorm = ShapeMatcher.NormalizeFilled(uSmall);
        using var tNorm = ShapeMatcher.NormalizeFilled(tSmall);
        double huDist = ShapeMatcher.HuDistance(uNorm, tNorm); // 越小越好
        int huScore = ShapeMatcher.HuScore100(huDist);         // 0~100, 越高越像
        double huScore01 = huScore / 100.0;

        // 2. 覆蓋率 (max(user, templBand))
        double coverage = ShapeMatcher.CoverageScore_Strict(uEdges, tEdges, effTol);
        double coverage01 = Mathf.Clamp01((float)coverage);

        // 3. Chamfer
        double chamferPx  = ShapeMatcher.TruncatedChamfer(uEdges, tEdges, effTol); // 0..tau
        double chamferScore = 1.0 - Mathf.Clamp01((float)(chamferPx / (double)effTol));

        // 4. 筆劃長度過濾
        double userEdgePx = Cv2.CountNonZero(uEdges);
        if (userEdgePx < minStrokePx)
        {
            huScore01 = 0.0;
            coverage01 = 0.0;
            chamferScore = 0.0;
        }

        // 5. 綜合分數（主結構為主，細節為輔）
        double detail = (coverage01 + chamferScore) / 2.0;
        double finalScore01 = 0.8 * huScore01 + 0.2 * detail;
        int finalScore100 = Mathf.RoundToInt((float)(finalScore01 * 100.0));

        // 視覺化
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
        {
            scoreText.text = $"Score {finalScore100}/100\n" +
                             $"Hu {huScore01:0.00}, Coverage {coverage01:0.00}, Chamfer {chamferPx:0.00}px\n" +
                             $"(userEdgePx={userEdgePx:0}, tol={effTol}px)";
        }

        Debug.Log($"[Judge] Score={finalScore100}/100, Hu={huScore01:0.000}, coverage={coverage01:0.000}, chamfer={chamferPx:0.000}px, userEdgePx={userEdgePx:0}, tol={effTol}px");

        uSmall.Dispose();
        tSmall.Dispose();
    }
}