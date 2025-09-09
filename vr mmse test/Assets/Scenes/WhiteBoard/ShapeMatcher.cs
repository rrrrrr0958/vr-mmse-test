using OpenCvSharp;

public static class ShapeMatcher
{
    public static (Mat small, double scale) ResizeToMaxSide(Mat src, int maxSide)
    {
        if (src.Empty()) return (src.Clone(), 1.0);

        int w = src.Cols, h = src.Rows;
        int maxWH = w > h ? w : h;
        if (maxWH <= maxSide) return (src.Clone(), 1.0);

        double scale = (double)maxSide / maxWH;
        int nw = (int)(w * scale);
        int nh = (int)(h * scale);

        var dst = new Mat();
        Cv2.Resize(src, dst, new Size(nw, nh), 0, 0, InterpolationFlags.Area);
        return (dst, scale);
    }

    public static Mat ToEdges(Mat bgra,
                              bool useCanny,
                              double alphaThreshold,
                              int dilateKernel,
                              double blurSigma = 1.5,
                              int closeKernel = 0)
    {
        using var bgr = new Mat();
        Cv2.CvtColor(bgra, bgr, ColorConversionCodes.BGRA2BGR);

        using var gray = new Mat();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);

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

    public static double CoverageScore(Mat userEdges, Mat templEdges, int tolPx = 4)
    {
        using var se = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(tolPx * 2 + 1, tolPx * 2 + 1));
        using var templBand = new Mat();
        Cv2.Dilate(templEdges, templBand, se);

        using var userMask = new Mat();
        Cv2.Threshold(userEdges, userMask, 127, 255, ThresholdTypes.Binary);

        using var hit = new Mat();
        Cv2.BitwiseAnd(userMask, templBand, hit);

        double userCount = Cv2.CountNonZero(userMask);
        if (userCount <= 1e-6) return 0.0;

        double hitCount = Cv2.CountNonZero(hit);
        return hitCount / userCount;
    }

    public static double TruncatedChamfer(Mat userEdges, Mat templEdges, int tauPx = 4)
    {
        // 先做模板的距離變換（對模板邊緣取反 → 邊緣附近距離小）
        using var inv = new Mat();
        Cv2.BitwiseNot(templEdges, inv);

        using var dist = new Mat();
        Cv2.DistanceTransform(inv, dist, DistanceTypes.L2, DistanceTransformMasks.Mask3);

        // 把距離截斷到 tau 以內，避免遠處懲罰過大
        using var distClamped = new Mat();
        Cv2.Min(dist, tauPx, distClamped);

        // 只在「玩家邊」的位置取樣距離
        using var mask = new Mat();
        Cv2.Threshold(userEdges, mask, 127, 255, ThresholdTypes.Binary);

        using var dist32 = new Mat();
        distClamped.ConvertTo(dist32, MatType.CV_32F);

        using var masked = new Mat();
        dist32.CopyTo(masked, mask);

        var sum = Cv2.Sum(masked).Val0;
        double count = Cv2.CountNonZero(mask);
        if (count <= 1e-6) return tauPx;

        return sum / count; // 單位：像素
    }

    // 估計使用者筆劃粗細（像素）
    // 傳入的 edges：前處理後的二值邊緣影像（255=邊，0=背景）
    public static double EstimateStrokeWidth(Mat edges)
    {
        using var inv = new Mat();
        Cv2.BitwiseNot(edges, inv);
        using var dist = new Mat();
        Cv2.DistanceTransform(inv, dist, DistanceTypes.L2, DistanceTransformMasks.Mask3);
        return Cv2.Mean(dist).Val0 * 2.0;
    }

}

