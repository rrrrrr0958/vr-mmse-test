using UnityEngine;
using UnityEngine.UI;
using OpenCvSharp;
using URect = UnityEngine.Rect;

public static class CvUnityBridge
{
    // RenderTexture -> Mat (BGRA)
    public static Mat FromRenderTexture(RenderTexture rt)
    {
        var prev = RenderTexture.active;
        RenderTexture.active = rt;

        var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new URect(0, 0, rt.width, rt.height), 0, 0);
        tex.Apply();

        RenderTexture.active = prev;

        byte[] png = tex.EncodeToPNG();
        Object.Destroy(tex);

        // Unchanged: 會保留 4 通道（BGRA）
        return Cv2.ImDecode(png, ImreadModes.Unchanged);
    }

    // Texture2D -> Mat (BGRA)
    public static Mat FromTexture2D(Texture2D tex)
    {
        var readable = tex;
        if (!tex.isReadable)
        {
            // 建一張可讀副本
            var rt = new RenderTexture(tex.width, tex.height, 0, RenderTextureFormat.ARGB32);
            Graphics.Blit(tex, rt);
            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            readable = new Texture2D(tex.width, tex.height, TextureFormat.RGBA32, false, false);
            readable.ReadPixels(new URect(0, 0, tex.width, tex.height), 0, 0); // ← 這行改用 URect
            readable.Apply();
            RenderTexture.active = prev;
            rt.Release();
        }

        byte[] png = readable.EncodeToPNG();
        if (readable != tex) Object.Destroy(readable);

        return Cv2.ImDecode(png, ImreadModes.Unchanged);
    }

    // Mat -> RawImage
    public static void SetRawImage(RawImage img, Mat mat)
    {
        if (img == null || mat == null || mat.Empty()) return;

        Cv2.ImEncode(".png", mat, out var bytes);

        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false, false);
        ImageConversion.LoadImage(tex, bytes);

        // 如需避免累積記憶體，可在合適時機 Destroy 舊的 Texture2D
        if (img.texture != null && img.texture is Texture2D old && old != tex)
        {
            // Object.Destroy(old);
        }
        img.texture = tex;
        // img.SetNativeSize(); // 需要時再開
    }
}
