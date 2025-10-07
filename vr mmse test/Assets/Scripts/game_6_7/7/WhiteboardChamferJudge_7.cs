using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;

public class WhiteboardChamferJudge_7 : MonoBehaviour
{
    [Header("Input")]
    public RenderTexture whiteboardRT;   // 你的白板 RenderTexture
    public Texture2D templatePNG;        // 目標圖（黑線白底或透明底 PNG）
    public int analysisMaxSide = 512;

    [Header("Debug UI (optional)")]
    public RawImage preview;   // 預覽
    public Text scoreText;     // 顯示分數

    [ContextMenu("Evaluate Now")]
    public void EvaluateNow()
    {
        if (whiteboardRT == null || templatePNG == null)
        {
            Debug.LogWarning("[Judge] 請指定 Whiteboard RT 與 Template PNG");
            return;
        }

        // 讀入 Mat
        using var userBgra = CvUnityBridge_7.FromRenderTexture(whiteboardRT);
        using var templBgra = CvUnityBridge_7.FromTexture2D(templatePNG);

        // 等比縮圖到同一尺度
        var (uSmall, _) = ShapeMatcher_7.ResizeToMaxSide(userBgra, analysisMaxSide);
        var (tSmall, _) = ShapeMatcher_7.ResizeToMaxSide(templBgra, analysisMaxSide);

        // 填滿主外輪廓並正規化
        using var uNorm = ShapeMatcher_7.NormalizeFilled(uSmall);
        using var tNorm = ShapeMatcher_7.NormalizeFilled(tSmall);

        // 主結構比對
        double huDist = ShapeMatcher_7.HuDistance(uNorm, tNorm);
        int huScore = ShapeMatcher_7.HuScore100(huDist);
        double huScore01 = huScore / 100.0;

        // 角數
        int userCorners = ShapeMatcher_7.CountPolyCorners(uNorm);
        int templCorners = ShapeMatcher_7.CountPolyCorners(tNorm);
        double angleScore = ShapeMatcher_7.AngleScore(userCorners, templCorners);

        // 覆蓋率（可選）
        double coverage = ShapeMatcher_7.CoverageScore_Strict(uNorm, tNorm);
        double coverage01 = Mathf.Clamp01((float)coverage);

        // Chamfer（可選）
        double chamferPx = ShapeMatcher_7.TruncatedChamfer(uNorm, tNorm, 6);
        double chamferScore = 1.0 - Mathf.Clamp01((float)(chamferPx / 6.0));

        // 綜合分數（主結構 80%，細節 20%，再乘以角數修正分）
        double detail = (coverage01 + chamferScore) / 2.0;
        double rawScore = 0.8 * huScore01 + 0.2 * detail;
        double finalScore01 = rawScore * angleScore;
        int finalScore100 = Mathf.RoundToInt((float)(finalScore01 * 100.0));

        // 預覽
        if (preview != null)
        {
            // 疊圖顯示：模板白，玩家紅
            using var vis = new Mat(uNorm.Rows, uNorm.Cols, MatType.CV_8UC3, new Scalar(0, 0, 0));
            vis.SetTo(new Scalar(255, 255, 255), tNorm);
            vis.SetTo(new Scalar(0, 0, 255), uNorm);

            CvUnityBridge_7.SetRawImage(preview, vis);
        }

        if (scoreText != null)
        {
            scoreText.text = $"Score {finalScore100}/100\n" +
                             $"Hu {huScore01:0.00} (dist={huDist:0.0000}), Angle {angleScore:0.00} ({userCorners}/{templCorners})\n" +
                             $"Coverage {coverage01:0.00}, Chamfer {chamferPx:0.00}px";
        }

        Debug.Log($"[Judge] Score={finalScore100}/100, Hu={huScore01:0.000}, Angle={angleScore:0.000} ({userCorners}/{templCorners}), coverage={coverage01:0.000}, chamfer={chamferPx:0.000}px");

        uSmall.Dispose();
        tSmall.Dispose();
    }
}