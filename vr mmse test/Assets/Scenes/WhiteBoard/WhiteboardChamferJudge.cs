using System;
using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;

public class WhiteboardChamferJudge : MonoBehaviour
{
    [Header("Input")]
    public RenderTexture whiteboardRT;
    public Texture2D templatePNG;

    [Tooltip("Both images are resized so the longer side equals this before comparison.")]
    public int analysisMaxSide = 512;

    [Header("Edge Extract")]
    public bool useCanny = false;
    [Range(0, 1)] public double alphaThreshold = 0.06;
    [Range(0, 7)] public int dilateKernel = 3;

    [Header("Search Range (size not enforced)")]
    public float scaleMin = 0.5f;
    public float scaleMax = 1.8f;
    public float scaleStep = 0.05f;

    public float angleMin = -20f;
    public float angleMax = 20f;
    public float angleStep = 2f;

    [Header("Scoring (0–100)")]
    [Tooltip("Strictness in pixels at analysisMaxSide=512. Larger = more tolerant.")]
    public float tauPxAt512 = 6f;
    [Range(0,100)] public int passScore = 75;

    [Header("Debug Output")]
    public RawImage preview;     // overlay: red=user, cyan=template, white=overlap
    public Text scoreText;

    [ContextMenu("Evaluate Now")]
    public void EvaluateNow()
    {
        if (whiteboardRT == null || templatePNG == null)
        {
            Debug.LogWarning("[Judge] Please assign Whiteboard RT and Template PNG.");
            return;
        }

        using var userBgra = CvUnityBridge.FromRenderTexture(whiteboardRT);
        using var templBgra = CvUnityBridge.FromTexture2D(templatePNG);

        var (uSmall, _) = ShapeMatcher.ResizeToMaxSide(userBgra, analysisMaxSide);
        var (tSmall, _) = ShapeMatcher.ResizeToMaxSide(templBgra, analysisMaxSide);

        using var uEdges = ShapeMatcher.ToEdges(uSmall, useCanny, alphaThreshold, dilateKernel);
        using var tEdges = ShapeMatcher.ToEdges(tSmall, useCanny, alphaThreshold, dilateKernel);

        var (sim, ang, sc, shift, overlay) = ShapeMatcher.MatchByChamfer(
            uEdges, tEdges,
            scaleMin, scaleMax, scaleStep,
            angleMin, angleMax, angleStep
        );

        // --- 0~100 打分 ---
        int score100 = ToScore100(sim, tauPxAt512, analysisMaxSide);
        bool pass = (score100 >= passScore);

        if (preview != null && overlay != null)
            CvUnityBridge.SetRawImage(preview, overlay);

        if (scoreText != null)
            scoreText.text = $"Score {score100}/100  {(pass ? "PASS" : "FAIL")}\n"
                           + $"sim={sim:0.###} px, angle={ang:0.#}°, scale={sc:0.###}, shift=({shift.X:0},{shift.Y:0})";

        Debug.Log($"[Judge] Score={score100}/100 ({(pass?"PASS":"FAIL")}) | sim={sim:0.###} px, angle={ang:0.#}°, scale={sc:0.###}, shift=({shift.X:0},{shift.Y:0})");

        overlay?.Dispose();
        uSmall.Dispose(); tSmall.Dispose();
    }

    // 將像素誤差映射為 0~100 分（自動隨解析度縮放嚴格度）
    static int ToScore100(double simPx, float tauAt512Px, int maxSide)
    {
        double tau = tauAt512Px * (maxSide / 512.0);
        double s = 100.0 * Math.Exp(-simPx / Math.Max(1e-6, tau));
        return Mathf.Clamp(Mathf.RoundToInt((float)s), 0, 100);
    }
}
