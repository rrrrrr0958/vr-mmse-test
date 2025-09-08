using System;
using OpenCvSharp;

public static class ShapeMatcher
{
    // 把輸入 Mat 等比縮到最大邊 <= maxSide，回傳(縮圖, 縮放倍數)
    public static (Mat mat, double scale) ResizeToMaxSide(Mat srcBgra, int maxSide)
    {
        if (srcBgra.Empty()) throw new ArgumentException("src empty");
        int w = srcBgra.Cols, h = srcBgra.Rows;
        int side = Math.Max(w, h);
        if (side <= maxSide) return (srcBgra.Clone(), 1.0);

        double s = maxSide / (double)side;
        int nw = (int)Math.Round(w * s);
        int nh = (int)Math.Round(h * s);

        var dst = new Mat();
        Cv2.Resize(srcBgra, dst, new Size(nw, nh), 0, 0, InterpolationFlags.Area);
        return (dst, s);
    }

    // 轉成二值邊緣圖（單通道 8U，0/255）
    public static Mat ToEdges(Mat srcBgra, bool useCanny, double alphaThreshold, int dilateKernel)
    {
        var bgra = srcBgra;
        var gray = new Mat();
        if (bgra.Channels() == 4)
        {
            // 取 RGB 作灰階（忽略 Alpha）
            Cv2.CvtColor(bgra, gray, ColorConversionCodes.BGRA2GRAY);
        }
        else if (bgra.Channels() == 3)
        {
            Cv2.CvtColor(bgra, gray, ColorConversionCodes.BGR2GRAY);
        }
        else
        {
            gray = bgra.Clone();
        }

        Mat edges = new Mat();
        if (useCanny)
        {
            // Otsu 自動門檻可行；也能給定固定門檻
            double th = Cv2.Threshold(gray, new Mat(), 0, 255, ThresholdTypes.Otsu);
            Cv2.Canny(gray, edges, th * 0.5, th);
        }
        else
        {
            // 單純灰階門檻
            double t = alphaThreshold * 255.0;
            Cv2.Threshold(gray, edges, t, 255, ThresholdTypes.Binary);
        }

        if (dilateKernel > 0)
        {
            var k = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(dilateKernel, dilateKernel));
            Cv2.Dilate(edges, edges, k);
        }

        return edges;
    }

    // Chamfer 比對：窄範圍掃角度 + 縮放，回傳(相似分數、角度、縮放、平移、疊圖)
    public static (double score, double bestAngle, double bestScale, Point2f bestShift, Mat overlay)
        MatchByChamfer(
            Mat userEdges, Mat templEdges,
            float scaleMin, float scaleMax, float scaleStep,
            float angleMin, float angleMax, float angleStep)
    {
        if (userEdges.Empty() || templEdges.Empty()) throw new ArgumentException("edges empty");

        // 1) 把 userEdges 做距離變換（Chamfer 會在這張圖上取樣）
        //    先反相：物件=白(255)，背景=黑(0)；距離變換要以「零像素」當邊界。
        var inv = new Mat();
        Cv2.BitwiseNot(userEdges, inv);
        var dist = new Mat(); // 32F
        Cv2.DistanceTransform(inv, dist, DistanceTypes.L2, DistanceTransformMasks.Mask3);

        // 2) 掃縮放 & 角度
        double bestScore = double.MaxValue;
        double bestAngle = 0, bestScale = 1;
        Point2f bestShift = new Point2f(0, 0);
        Mat bestOverlay = null;

        for (float s = scaleMin; s <= scaleMax + 1e-6f; s += scaleStep)
        {
            // 對模板做縮放
            int tw = (int)Math.Round(templEdges.Cols * s);
            int th = (int)Math.Round(templEdges.Rows * s);
            if (tw < 4 || th < 4) continue;

            var scaled = new Mat();
            Cv2.Resize(templEdges, scaled, new Size(tw, th), 0, 0, InterpolationFlags.Linear);

            for (float a = angleMin; a <= angleMax + 1e-6f; a += angleStep)
            {
                // 旋轉模板
                var rot = Cv2.GetRotationMatrix2D(new Point2f(tw * 0.5f, th * 0.5f), a, 1.0);
                var rotated = new Mat();
                Cv2.WarpAffine(scaled, rotated, rot, new Size(tw, th),
                    InterpolationFlags.Linear, BorderTypes.Constant, Scalar.Black);

                // 以距離圖大小為畫布，嘗試把 rotated 放進來
                double score; Point2f shift;
                (score, shift) = EvaluateChamferScore(dist, rotated);

                if (score < bestScore)
                {
                    bestScore = score;
                    bestAngle = a;
                    bestScale = s;
                    bestShift = shift;

                    bestOverlay?.Dispose();
                    bestOverlay = MakeOverlay(userEdges, rotated, shift);
                }

                rotated.Dispose();
            }
            scaled.Dispose();
        }

        return (bestScore, bestAngle, bestScale, bestShift, bestOverlay);
    }

    // 將模板邊緣圖(0/255)蓋到距離圖上，取非零點的距離平均作為分數
    private static (double meanDist, Point2f shift) EvaluateChamferScore(Mat dist32F, Mat templEdge)
    {
        // 讓模板置中後再嘗試在 dist 畫布中間對齊（此處簡化：直接貼左上 = 0,0）
        // 也可做小範圍平移搜尋，但先給一版快速的。
        int W = dist32F.Cols, H = dist32F.Rows;
        int w = templEdge.Cols, h = templEdge.Rows;

        // 若模板比距離圖還大，先丟掉（你也可改用 padding）
        if (w > W || h > H) return (double.MaxValue, new Point2f(0, 0));

        // 粗暴對齊到中央
        int offX = (W - w) / 2;
        int offY = (H - h) / 2;

        // 取模板邊緣為 255 的像素位置去抽樣 dist
        using var mask = templEdge.Threshold(254, 255, ThresholdTypes.Binary);
        using var roi = new Mat(dist32F, new OpenCvSharp.Rect(offX, offY, w, h));
        Scalar mean = Cv2.Mean(roi, mask);

        return (mean.Val0, new Point2f(offX, offY));
    }

    // 疊圖（紅=使用者邊緣，青=模板邊緣，重疊處接近白）
    private static Mat MakeOverlay(Mat userEdges, Mat templEdge, Point2f shift)
    {
        var vis = new Mat();
        Cv2.CvtColor(userEdges, vis, ColorConversionCodes.GRAY2BGRA);

        int offX = (int)Math.Round(shift.X);
        int offY = (int)Math.Round(shift.Y);

        var dstRoi = new OpenCvSharp.Rect(
            Math.Max(0, offX), Math.Max(0, offY),
            Math.Min(templEdge.Cols, vis.Cols - Math.Max(0, offX)),
            Math.Min(templEdge.Rows, vis.Rows - Math.Max(0, offY)));

        if (dstRoi.Width <= 0 || dstRoi.Height <= 0) return vis;

        var srcRoi = new OpenCvSharp.Rect(
            Math.Max(0, -offX), Math.Max(0, -offY),
            dstRoi.Width, dstRoi.Height);

        using var tCrop = new Mat(templEdge, srcRoi);
        using var color = new Mat();
        Cv2.CvtColor(tCrop, color, ColorConversionCodes.GRAY2BGRA);

        // 青色：B=255, G=255, R=0（把非邊緣清成透明）
        var mask = tCrop.Threshold(254, 255, ThresholdTypes.Binary);
        color.SetTo(new Scalar(255, 255, 0, 255), mask);

        using var sub = new Mat(vis, dstRoi);
        Cv2.AddWeighted(sub, 1.0, color, 0.7, 0, sub);

        return vis;
    }
}
