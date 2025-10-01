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

    // ====== 填滿主外輪廓並正規化 ======
    public static Mat NormalizeFilled(Mat src, int outSize = 300)
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

        Cv2.FindContours(bin, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

        var filled = new Mat(bin.Rows, bin.Cols, MatType.CV_8UC1, Scalar.Black);

        if (contours.Length > 0)
        {
            // 只填最大外輪廓
            int mainIdx = contours.Select((c, idx) => (area: Cv2.ContourArea(c), idx)).OrderByDescending(x => x.area).First().idx;
            Cv2.DrawContours(filled, contours, mainIdx, Scalar.White, thickness: -1);

            var allPts = contours[mainIdx];
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

    // ====== Hu-moments / matchShapes 距離 ======
    public static double HuDistance(Mat aNorm, Mat bNorm)
        => Cv2.MatchShapes(aNorm, bNorm, ShapeMatchModes.I1, 0);

    public static int HuScore100(double huDistance, float gain = 8f)
    {
        double s01 = Math.Exp(-gain * Math.Max(0.0, huDistance)); // 0..1
        return (int)Math.Round(100.0 * Math.Clamp(s01, 0.0, 1.0));
    }

    // ====== 覆蓋率 (可選) ======
    public static double CoverageScore_Strict(Mat userFilled, Mat templFilled)
    {
        using var userMask = new Mat();
        Cv2.Threshold(userFilled, userMask, 127, 255, ThresholdTypes.Binary);

        using var templMask = new Mat();
        Cv2.Threshold(templFilled, templMask, 127, 255, ThresholdTypes.Binary);

        using var hit = new Mat();
        Cv2.BitwiseAnd(userMask, templMask, hit);

        double userCount = Cv2.CountNonZero(userMask);
        double templCount = Cv2.CountNonZero(templMask);
        double denom = Math.Max(userCount, templCount);
        if (denom <= 1e-6) return 0.0;

        double hitCount = Cv2.CountNonZero(hit);
        return hitCount / denom;
    }

    // ====== Chamfer (可選) ======
    public static double TruncatedChamfer(Mat userFilled, Mat templFilled, int tauPx = 6)
    {
        using var inv = new Mat();
        Cv2.BitwiseNot(templFilled, inv);

        using var dist = new Mat();
        Cv2.DistanceTransform(inv, dist, DistanceTypes.L2, DistanceTransformMasks.Mask3);

        using var distClamped = new Mat();
        Cv2.Threshold(dist, distClamped, tauPx, tauPx, ThresholdTypes.Trunc);

        using var mask = new Mat();
        Cv2.Threshold(userFilled, mask, 127, 255, ThresholdTypes.Binary);

        using var dist32 = new Mat();
        distClamped.ConvertTo(dist32, MatType.CV_32F);

        using var masked = new Mat();
        dist32.CopyTo(masked, mask);

        var sum = Cv2.Sum(masked).Val0;
        double count = Cv2.CountNonZero(mask);
        if (count <= 1e-6) return tauPx;

        return sum / count;
    }

    // ====== 多邊形角數偵測（用填滿圖 + 寬鬆 approx） ======
    public static int CountPolyCorners(Mat bin, double approxEpsilonRatio = 0.06)
    {
        Cv2.FindContours(bin, out Point[][] contours, out _, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
        if (contours.Length == 0) return 0;
        var main = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
        double peri = Cv2.ArcLength(main, true);
        var approx = Cv2.ApproxPolyDP(main, approxEpsilonRatio * peri, true);
        return approx.Length;
    }

    // ====== 角數差異分數 ======
    public static double AngleScore(int userCorners, int templCorners)
    {
        int diff = Math.Abs(userCorners - templCorners);
        if (diff == 0) return 1.0;
        if (diff == 1) return 0.7;
        if (diff == 2) return 0.4;
        if (diff == 3) return 0.2;
        return 0.0;
    }
}