using System;
using System.Linq;
using OpenCvSharp;

public static class ShapeMatcher
{
    // ====== Resize ======
    public static (Mat small, double scale) ResizeToMaxSide(Mat src, int maxSide)
    {
        if (src.Empty()) return (src.Clone(), 1.0);

        int w = src.Cols, h = src.Rows;
        int maxWH = Math.Max(w, h);
        if (maxWH <= maxSide) return (src.Clone(), 1.0);

        double scale = (double)maxSide / maxWH;
        int nw = (int)(w * scale);
        int nh = (int)(h * scale);

        var dst = new Mat();
        Cv2.Resize(src, dst, new Size(nw, nh), 0, 0, InterpolationFlags.Area);
        return (dst, scale);
    }

    // ====== Edge extraction ======
    public static Mat ToEdges(Mat bgra,
                              bool useCanny,
                              double alphaThreshold,
                              int dilateKernel,
                              double blurSigma = 1.5,
                              int closeKernel = 0)
    {
        using var gray = new Mat();
        if (bgra.Channels() == 4)
        {
            using var bgr = new Mat();
            Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        }
        else if (bgra.Channels() == 3)
        {
            Cv2.CvtColor(bgra, gray, ColorConversionCodes.BGR2GRAY);
        }
        else bgra.CopyTo(gray);

        if (blurSigma > 0.1)
        {
            int k = ((int)(blurSigma * 6 + 1)) | 1;
            Cv2.GaussianBlur(gray, gray, new Size(k, k), blurSigma);
        }

        var edges = new Mat();
        if (useCanny)
            Cv2.Canny(gray, edges, 50, 120);
        else
            Cv2.Threshold(gray, edges, alphaThreshold * 255.0, 255, ThresholdTypes.BinaryInv);

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

    // ====== Coverage (0..1, 越大越好) 嚴格分母 ======
    public static double CoverageScore_Strict(Mat userEdges, Mat templEdges, int tolPx = 4)
    {
        using var se = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(tolPx * 2 + 1, tolPx * 2 + 1));
        using var templBand = new Mat();
        Cv2.Dilate(templEdges, templBand, se);

        using var userMask = new Mat();
        Cv2.Threshold(userEdges, userMask, 127, 255, ThresholdTypes.Binary);

        using var hit = new Mat();
        Cv2.BitwiseAnd(userMask, templBand, hit);

        double userCount = Cv2.CountNonZero(userMask);
        double templBandCount = Cv2.CountNonZero(templBand);
        double denom = Math.Max(userCount, templBandCount);
        if (denom <= 1e-6) return 0.0;

        double hitCount = Cv2.CountNonZero(hit);
        return hitCount / denom;
    }

    // ====== Truncated Chamfer (像素距離, 越小越好) ======
    public static double TruncatedChamfer(Mat userEdges, Mat templEdges, int tauPx = 4)
    {
        using var inv = new Mat();
        Cv2.BitwiseNot(templEdges, inv);

        using var dist = new Mat();
        Cv2.DistanceTransform(inv, dist, DistanceTypes.L2, DistanceTransformMasks.Mask3);

        using var distClamped = new Mat();
        Cv2.Threshold(dist, distClamped, tauPx, tauPx, ThresholdTypes.Trunc);

        using var mask = new Mat();
        Cv2.Threshold(userEdges, mask, 127, 255, ThresholdTypes.Binary);

        using var dist32 = new Mat();
        distClamped.ConvertTo(dist32, MatType.CV_32F);

        using var masked = new Mat();
        dist32.CopyTo(masked, mask);

        var sum = Cv2.Sum(masked).Val0;
        double count = Cv2.CountNonZero(mask);
        if (count <= 1e-6) return tauPx;

        return sum / count;
    }

    // ====== 筆劃寬度估計（像素） ======
    public static double EstimateStrokeWidth(Mat edges)
    {
        using var inv = new Mat();
        Cv2.BitwiseNot(edges, inv);
        using var dist = new Mat();
        Cv2.DistanceTransform(inv, dist, DistanceTypes.L2, DistanceTransformMasks.Mask3);
        return Cv2.Mean(dist).Val0 * 2.0;
    }

    // ====== Hu-moments 規範化 & 距離 ======
    public static Mat NormalizeFilled(Mat src, int outSize = 300, int closeK = 3, int dilateK = 2)
    {
        using var gray = new Mat();
        if (src.Channels() == 4)
        {
            using var bgr = new Mat();
            Cv2.CvtColor(src, bgr, ColorConversionCodes.BGRA2BGR);
            Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        }
        else if (src.Channels() == 3)
        {
            Cv2.CvtColor(src, gray, ColorConversionCodes.BGR2GRAY);
        }
        else src.CopyTo(gray);

        using var bin = new Mat();
        Cv2.Threshold(gray, bin, 0, 255, ThresholdTypes.BinaryInv | ThresholdTypes.Otsu);

        if (closeK > 0)
        {
            using var seC = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(closeK, closeK));
            Cv2.MorphologyEx(bin, bin, MorphTypes.Close, seC);
        }
        if (dilateK > 0)
        {
            using var seD = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(dilateK, dilateK));
            Cv2.Dilate(bin, bin, seD);
        }

        Cv2.FindContours(bin, out Point[][] contours, out _, RetrievalModes.External,
                         ContourApproximationModes.ApproxSimple);

        var filled = new Mat(bin.Rows, bin.Cols, MatType.CV_8UC1, Scalar.Black);

        if (contours.Length > 0)
        {
            for (int i = 0; i < contours.Length; i++)
                Cv2.DrawContours(filled, contours, i, Scalar.White, thickness: -1);

            var allPts = contours.SelectMany(c => c).ToArray();
            var r = Cv2.BoundingRect(allPts);
            int margin = Math.Max(2, (int)(Math.Max(r.Width, r.Height) * 0.04));
            r.X = Math.Max(0, r.X - margin);
            r.Y = Math.Max(0, r.Y - margin);
            r.Width  = Math.Min(filled.Cols - r.X, r.Width  + 2 * margin);
            r.Height = Math.Min(filled.Rows - r.Y, r.Height + 2 * margin);

            using var crop = new Mat(filled, r);
            var norm = new Mat();
            Cv2.Resize(crop, norm, new Size(outSize, outSize), 0, 0, InterpolationFlags.Area);
            return norm;
        }

        return new Mat(outSize, outSize, MatType.CV_8UC1, Scalar.Black);
    }

    public static double HuDistance(Mat aNorm, Mat bNorm)
        => Cv2.MatchShapes(aNorm, bNorm, ShapeMatchModes.I1, 0);

    public static int HuScore100(double huDistance, float gain = 8f)
    {
        double s01 = Math.Exp(-gain * Math.Max(0.0, huDistance)); // 0..1
        return (int)Math.Round(100.0 * Math.Clamp(s01, 0.0, 1.0));
    }

    // ====== 多邊形角數判斷（用邊緣圖） ======
    public static int CountPolyCorners(Mat edge, double approxEpsilonRatio = 0.02)
    {
        Cv2.FindContours(edge, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        int maxCorners = 0;
        foreach (var c in contours)
        {
            double peri = Cv2.ArcLength(c, true);
            var approx = Cv2.ApproxPolyDP(c, approxEpsilonRatio * peri, true);
            if (approx.Length > maxCorners) maxCorners = approx.Length;
        }
        return maxCorners;
    }

    // 嚴格版角數相似度
    public static double CornerSimilarity(int userCorners, int templCorners)
    {
        if (userCorners <= 0 || templCorners <= 0) return 0.0;
        int diff = Math.Abs(userCorners - templCorners);
        if (diff >= 2) return 0.0; // 差兩個以上就直接0分
        if (diff == 1) return 0.3; // 差一個給低分
        return 1.0; // 完全吻合才滿分
    }
}