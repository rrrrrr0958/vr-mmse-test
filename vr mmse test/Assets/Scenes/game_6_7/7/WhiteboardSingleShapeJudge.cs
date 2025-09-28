using System;
using System.Linq;
using OpenCvSharp;
using UnityEngine;
using UnityEngine.UI;

public class WhiteboardSingleShapeJudge : MonoBehaviour
{
    public enum TargetShape { Triangle, Rectangle, Square /*, Circle*/ }

    [Header("Input")]
    public RenderTexture whiteboardRT;

    [Header("Target")]
    public TargetShape target = TargetShape.Triangle;

    [Header("Preprocess")]
    public int analysisMaxSide = 512;
    public bool useCanny = true;
    [Range(0, 1)] public double alphaThreshold = 0.05;
    [Range(0, 7)] public int dilateKernel = 3;
    public double blurSigma = 1.2;
    public int closeKernel = 0;

    [Header("Polygon Approx")]
    [Range(0.001f, 0.05f)] public float approxEpsFrac = 0.01f;
    public double minAreaFrac = 0.01;

    [Header("Scores & Tolerances")]
    public float rightAngleTolDeg = 20f;
    public float squareAspectTol = 0.25f;

    [Header("Debug UI")]
    public RawImage preview;
    public Text scoreText;

    [ContextMenu("Evaluate Now")]
    public void EvaluateNow()
    {
        if (whiteboardRT == null) { Report(0, "請指定 Whiteboard RT"); return; }

        using var matBgra = CvUnityBridge.FromRenderTexture(whiteboardRT);
        var (img, _) = ResizeToMaxSide(matBgra, analysisMaxSide);

        using var edges = ToEdges(img, useCanny, alphaThreshold, dilateKernel, blurSigma, closeKernel);

        // 找最大外輪廓
        Cv2.FindContours(
            edges,
            out Point[][] contours,
            out _,
            RetrievalModes.External,
            ContourApproximationModes.ApproxSimple   // ← 這裡改成 ApproxSimple
        );


        if (contours.Length == 0) { Report(0, "沒有輪廓"); return; }

        var imgArea = edges.Rows * edges.Cols;
        var cand = contours
            .Select(c => new { C = c, A = Math.Abs(Cv2.ContourArea(c)) })
            .Where(x => x.A >= imgArea * minAreaFrac)
            .OrderByDescending(x => x.A)
            .FirstOrDefault();

        if (cand == null) { Report(0, "輪廓太小"); return; }

        // 近似多邊形
        double peri = Cv2.ArcLength(cand.C, true);
        double eps = Math.Max(1.0, peri * approxEpsFrac);
        var approx = Cv2.ApproxPolyDP(cand.C, eps, true); // Point[]
        bool isConvex = Cv2.IsContourConvex(approx);
        var P = approx.Select(p => new Point2f(p.X, p.Y)).ToArray();
        int n = P.Length;

        // 直線性（原輪廓 vs 近似）
        double areaPoly = Math.Abs(Cv2.ContourArea(approx));
        double linearity = areaPoly > 1e-6 ? Math.Min(1.0, Math.Abs(cand.A / areaPoly)) : 0.0;

        float score;
        string detail;

        switch (target)
        {
            case TargetShape.Triangle:
            {
                float sVertex = (n == 3) ? 1f : Mathf.Exp(-Mathf.Abs(n - 3));
                float sConvex = isConvex ? 1f : 0f;
                float sLine   = Mathf.Clamp01((float)linearity);

                score = 100f * (0.45f * sVertex + 0.25f * sConvex + 0.30f * sLine);
                detail = $"n={n}, convex={isConvex}, linearity={linearity:0.00}";
                break;
            }
            case TargetShape.Rectangle:
            case TargetShape.Square:
            {
                float sVertex = (n == 4) ? 1f : Mathf.Exp(-Mathf.Abs(n - 4));
                float sConvex = isConvex ? 1f : 0f;

                var angles = QuadAnglesDeg(P);                       // double[]
                float rightScore = RightAngleScore(angles);          // 0..1

                var br = Cv2.BoundingRect(approx);
                float aspect = (br.Width > 0 && br.Height > 0) ? (float)br.Width / br.Height : 0f;
                if (aspect < 1f && aspect > 1e-6f) aspect = 1f / aspect;
                float sAspect =
                    (target == TargetShape.Square)
                    ? (1f - Mathf.Clamp01((aspect - 1f) / squareAspectTol))
                    : 1f;

                float sLine = Mathf.Clamp01((float)linearity);

                score = 100f * (0.25f * sVertex + 0.15f * sConvex + 0.35f * rightScore + 0.25f * sLine);
                if (target == TargetShape.Square)
                    score *= Mathf.Clamp01(0.5f + 0.5f * sAspect);

                detail = $"n={n}, convex={isConvex}, rightAngleScore={rightScore:0.00}, aspect≈{aspect:0.00}, linearity={linearity:0.00}";
                break;
            }
            default:
                score = 0f; detail = "未實作"; break;
        }

        // 視覺化
        if (preview != null)
        {
            using var vis = new Mat(edges.Rows, edges.Cols, MatType.CV_8UC3, new Scalar(255, 255, 255));
            using (var e1 = new Mat()) { Cv2.CvtColor(edges, e1, ColorConversionCodes.GRAY2BGR); Cv2.AddWeighted(vis, 0.7, e1, 0.3, 0, vis); }
            for (int i = 0; i < P.Length; i++)
            {
                var a = approx[i];
                var b = approx[(i + 1) % P.Length];
                Cv2.Line(vis, a, b, new Scalar(0, 200, 0), 2);
                Cv2.Circle(vis, a, 3, new Scalar(0, 120, 0), -1);
            }
            CvUnityBridge.SetRawImage(preview, vis);
        }

        Report(Mathf.RoundToInt(score), detail);
    }

    // ---------- helpers ----------
    void Report(int score100, string detail)
    {
        if (scoreText != null)
            scoreText.text = $"{target}: {score100}/100\n{detail}";
        Debug.Log($"[Judge] {target}: {score100}/100 | {detail}");
    }

    static (Mat small, double scale) ResizeToMaxSide(Mat src, int maxSide)
    {
        if (src.Empty()) return (src.Clone(), 1.0);
        int w = src.Cols, h = src.Rows, m = Math.Max(w, h);
        if (m <= maxSide) return (src.Clone(), 1.0);
        double s = (double)maxSide / m;
        var dst = new Mat();
        Cv2.Resize(src, dst, new Size((int)(w * s), (int)(h * s)), 0, 0, InterpolationFlags.Area);
        return (dst, s);
    }

    static Mat ToEdges(Mat bgra, bool useCanny, double alphaThreshold, int dilateKernel, double blurSigma, int closeKernel)
    {
        using var bgr  = new Mat(); Cv2.CvtColor(bgra, bgr,  ColorConversionCodes.BGRA2BGR);
        using var gray = new Mat(); Cv2.CvtColor(bgr,  gray, ColorConversionCodes.BGR2GRAY);

        if (blurSigma > 0.1)
        {
            int k = ((int)(blurSigma * 6 + 1)) | 1;
            Cv2.GaussianBlur(gray, gray, new Size(k, k), blurSigma);
        }

        var edges = new Mat();
        if (useCanny) Cv2.Canny(gray, edges, 50, 120);
        else          Cv2.Threshold(gray, edges, alphaThreshold * 255.0, 255, ThresholdTypes.BinaryInv);

        if (closeKernel > 0)
        {
            using var seClose = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(closeKernel, closeKernel));
            Cv2.MorphologyEx(edges, edges, MorphTypes.Close, seClose);
        }
        if (dilateKernel > 0)
        {
            using var seDil = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(dilateKernel, dilateKernel));
            Cv2.Dilate(edges, edges, seDil);
        }
        return edges;
    }

    static double[] QuadAnglesDeg(Point2f[] p)
    {
        if (p.Length != 4) return Array.Empty<double>();
        double[] a = new double[4];
        for (int i = 0; i < 4; i++)
        {
            var prev = p[(i + 3) % 4];
            var cur  = p[i];
            var next = p[(i + 1) % 4];
            a[i] = AngleDeg(prev, cur, next);
        }
        return a;
    }

    static float RightAngleScore(double[] angles)
    {
        if (angles == null || angles.Length != 4) return 0f;
        double s = angles.Select(a =>
        {
            double d = Math.Abs(a - 90.0);
            double t = d / Math.Max(1e-6, (double)UnityEngine.Mathf.Max(1f, rightAngleTolDegStatic));
            return Clamp01(1.0 - t);
        }).Average();
        return (float)Clamp01(s);
    }

    // 因為上面是 static 方法，需要把 rightAngleTolDeg 傳成 static 供計算（EvaluateNow 內會先同步）
    static float rightAngleTolDegStatic = 20f;

    static double AngleDeg(Point2f a, Point2f b, Point2f c)
    {
        var v1 = new Point2f(a.X - b.X, a.Y - b.Y);
        var v2 = new Point2f(c.X - b.X, c.Y - b.Y);
        double dot = v1.X * v2.X + v1.Y * v2.Y;
        double n1 = Math.Sqrt(v1.X * v1.X + v1.Y * v1.Y);
        double n2 = Math.Sqrt(v2.X * v2.X + v2.Y * v2.Y);
        if (n1 < 1e-6 || n2 < 1e-6) return 0;
        double cos = Clamp(dot / (n1 * n2), -1.0, 1.0);
        return Math.Acos(cos) * 180.0 / Math.PI;
    }

    // 小工具（為了相容性自行做 double 版 Clamp）
    static double Clamp(double v, double lo, double hi) => (v < lo) ? lo : (v > hi) ? hi : v;
    static double Clamp01(double v) => Clamp(v, 0.0, 1.0);

    // 在 EvaluateNow 一開始同步 rightAngleTolDeg 到 static 供計算
    void OnValidate() { rightAngleTolDegStatic = rightAngleTolDeg; }
    void Awake()      { rightAngleTolDegStatic = rightAngleTolDeg; }
}
