using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;
using OpenCvSharp.Unity;

public static class CvUnityBridge
{
    // 把 RenderTexture 讀成 Mat（RGBA32 -> BGRA）
    public static Mat FromRenderTexture(RenderTexture rt)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;
        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, true);
        tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();
        RenderTexture.active = prev;

        var mat = Unity.TextureToMat(tex); // BGRA
        Object.Destroy(tex);
        return mat;
    }

    // Texture2D 到 Mat
    public static Mat FromTexture2D(Texture2D tex)
    {
        return Unity.TextureToMat(tex); // BGRA
    }

    // Mat 到 Texture2D（顯示用）
    public static Texture2D ToTexture(Mat mat)
    {
        return Unity.MatToTexture(mat);
    }

    // 把兩張邊緣圖（單通道 0/255）疊成彩色：A=紅、B=青，重疊=白
    public static Mat MakeEdgeOverlay(Mat edgesA, Mat edgesB)
    {
        Mat r = new Mat(edgesA.Size(), MatType.CV_8UC3, Scalar.Black);
        var chR = new Mat(); var chG = new Mat(); var chB = new Mat();
        Cv2.Merge(new[] { chB, chG, chR }, r); // 先建 3 通道

        // A 畫紅
        var red = new Scalar(0, 0, 255);
        r.SetTo(red, edgesA);

        // B 畫青
        var cyan = new Scalar(255, 255, 0);
        r.SetTo(cyan, edgesB);

        // 重疊處變白
        var both = new Mat();
        Cv2.BitwiseAnd(edgesA, edgesB, both);
        r.SetTo(new Scalar(255, 255, 255), both);

        chR.Dispose(); chG.Dispose(); chB.Dispose(); both.Dispose();
        return r;
    }

    // 把 Mat 貼到 RawImage（方便預覽）
    public static void SetRawImage(RawImage img, Mat mat)
    {
        if (img == null) return;
        var tex = ToTexture(mat);
        img.texture = tex;
        img.SetNativeSize();
    }
}
