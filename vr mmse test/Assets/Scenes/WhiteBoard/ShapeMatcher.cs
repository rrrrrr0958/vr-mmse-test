using System;
using OpenCvSharp;

public static class ShapeMatcher
{
    // 前處理：把彩圖轉單通道邊緣圖（0/255）
    public static Mat ToEdges(Mat srcBGRA, bool useCanny, double alphaThresh01, int dilateKernel)
    {
        using var srcBGR = new Mat();
        Cv2.CvtColor(srcBGRA, srcBGR, ColorConversionCodes.BGRA2BGR);

        using var gray = new Mat();
        Cv2.CvtColor(srcBGR, gray, ColorConversionCodes.BGR2GRAY);

        Mat edges = new Mat();
        if (useCanny)
        {
            double t1 = 50, t2 = 150; // 可再調
            Cv2.Canny(gray, edges, t1, t2, 3, true);
        }
        else
        {
            // 直接二值（畫板常是白底紅線，也OK）
            double thr = alphaThresh01 * 255.0;
            Cv2.Threshold(gray, edges, thr, 255, ThresholdTypes.BinaryInv);
        }

        if (dilateKernel > 0)
        {
            var k = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(dilateKernel, dilateKernel));
            Cv2.MorphologyEx(edges, edges, MorphTypes.Dilate, k);
            k.Dispose();
        }
        return edges;
    }

    // 把圖等比縮到最長邊 = maxSide（回傳縮放倍率）
    public static (Mat resized, double scale) ResizeToMaxSide(Mat src, int maxSide)
    {
        var s = src.Size();
        int mx = Math.Max(s.Width, s.Height);
        if (mx <= maxSide) return (src.Clone(), 1.0);

        double ratio = (double)maxSide / mx;
        var dst = new Mat();
        Cv2.Resize(src, dst, new Size((int)(s.Width * ratio), (int)(s.Height * ratio)), 0, 0, InterpolationFlags.Area);
        return (dst, ratio);
    }

    // Chamfer 單向距離：A->B（以 B 的距離變換做查值）
    public static double ChamferOneWay(Mat edgesA, Mat edgesB)
    {
        // 確保 8U 單通道
        using var a = edgesA.Threshold(1, 255, ThresholdTypes.Binary);
        using var b = edgesB.Threshold(1, 255, ThresholdTypes.Binary);

        // 距離變換要對「非邊緣」做（以便取得到最近邊緣的距離）
        using var bInv = new Mat();
        Cv2.BitwiseNot(b, bInv);

        using var dist = new Mat();
        Cv2.DistanceTransform(bInv, dist, DistanceTypes.L2, 3);

        // 找出 A 的邊緣點
        Cv2.FindNonZero(a, out Point[] pts);
        if (pts == null || pts.Length == 0) return double.MaxValue;

        double sum = 0;
        foreach (var p in pts)
        {
            // clamp 避免越界
            int x = Math.Clamp(p.X, 0, dist.Cols - 1);
            int y = Math.Clamp(p.Y, 0, dist.Rows - 1);
            sum += dist.At<float>(y, x);
        }
        return sum / pts.Length; // 平均距離（像素）
    }

    // 對稱 Chamfer（A->B 與 B->A 的平均）
    public static double ChamferSymmetric(Mat edgesA, Mat edgesB)
    {
        double d1 = ChamferOneWay(edgesA, edgesB);
        double d2 = ChamferOneWay(edgesB, edgesA);
        return (d1 + d2) * 0.5;
    }

    // 把距離轉成相似度（0~1；越大越相似）
    public static double DistanceToSimilarity(double d, double sigmaPixels = 10.0)
    {
        // 高斯形式，d=0 => 1；d≈3σ => ~0.011
        return Math.Exp(-(d * d) / (2.0 * sigmaPixels * sigmaPixels));
    }

    // 搜尋最佳縮放/旋轉，回傳（相似度、角度、縮放、平移、疊圖）
    public static (double sim, double angle, double scale, OpenCvSharp.Point2f shift, Mat overlay)
        MatchByChamfer(Mat userEdges, Mat templateEdges,
                       double scaleMin, double scaleMax, double scaleStep,
                       double angleMinDeg, double angleMaxDeg, double angleStepDeg)
    {
        double bestSim = -1;
        double bestAngle = 0;
        double bestScale = 1;
        OpenCvSharp.Point2f bestShift = new(0, 0);
        Mat bestOverlay = null;

        var uSize = userEdges.Size();
        var uCenter = new OpenCvSharp.Point2f(uSize.Width * 0.5f, uSize.Height * 0.5f);

        for (double sc = scaleMin; sc <= scaleMax + 1e-6; sc += scaleStep)
        {
            // 先縮放模板
            using var scaled = new Mat();
            Cv2.Resize(templateEdges, scaled, new Size(), sc, sc, InterpolationFlags.Linear);

            for (double ang = angleMinDeg; ang <= angleMaxDeg + 1e-6; ang += angleStepDeg)
            {
                // 旋轉
                var c = new OpenCvSharp.Point2f(scaled.Cols * 0.5f, scaled.Rows * 0.5f);
                using var rotMat = Cv2.GetRotationMatrix2D(c, ang, 1.0);
                using var rotated = new Mat();
                Cv2.WarpAffine(scaled, rotated, rotMat, scaled.Size(), interpolation: InterpolationFlags.Linear, borderMode: BorderTypes.Constant, borderValue: Scalar.Black);

                // 置中到 user 尺寸（簡單平移：把 rotated 中心貼到 user 中心）
                var shift = new OpenCvSharp.Point2f(uCenter.X - rotated.Cols * 0.5f, uCenter.Y - rotated.Rows * 0.5f);
                using var placed = new Mat(userEdges.Size(), MatType.CV_8UC1, Scalar.Black);
                var roi = new OpenCvSharp.Rect(
                    x: Math.Max(0, (int)shift.X),
                    y: Math.Max(0, (int)shift.Y),
                    width: Math.Min(rotated.Cols, placed.Cols - Math.Max(0, (int)shift.X)),
                    height: Math.Min(rotated.Rows, placed.Rows - Math.Max(0, (int)shift.Y))
                );
                if (roi.Width <= 0 || roi.Height <= 0) continue;
                var srcRoi = new OpenCvSharp.Rect(
                    x: Math.Max(0, - (int)shift.X),
                    y: Math.Max(0, - (int)shift.Y),
                    width: roi.Width,
                    height: roi.Height
                );
                rotated[srcRoi].CopyTo(placed[roi]);

                double d = ChamferSymmetric(userEdges, placed);
                // sigma 用圖尺寸 2% 的像素數，較穩定
                double sigma = Math.Max(uSize.Width, uSize.Height) * 0.02;
                double sim = DistanceToSimilarity(d, sigma);

                if (sim > bestSim)
                {
                    bestSim = sim;
                    bestAngle = ang;
                    bestScale = sc;
                    bestShift = shift;

                    bestOverlay?.Dispose();
                    bestOverlay = CvUnityBridge.MakeEdgeOverlay(userEdges, placed);
                }
            }
        }

        return (bestSim, bestAngle, bestScale, bestShift, bestOverlay);
    }
}
