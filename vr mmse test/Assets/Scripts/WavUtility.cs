using System;
using System.IO;
using UnityEngine;

/// <summary>
/// 靜態工具類，用於將 Unity 的 float 陣列音訊資料轉換為標準的 WAV 格式位元組陣列。
/// </summary>
public static class WavUtility
{
    /// <summary>
    /// 將 float 陣列的音訊取樣資料轉換為 WAV 檔案格式的 byte 陣列。
    /// </summary>
    /// <param name="samples">音訊取樣的 float 陣列。</param>
    /// <param name="sampleRate">取樣率。</param>
    /// <returns>WAV 格式的 byte 陣列。</returns>
    public static byte[] FromAudioFloat(float[] samples, int sampleRate)
    {
        // 1. 轉換 float 到 16-bit short (PCM)
        short[] intData = new short[samples.Length];
        byte[] bytesData = new byte[intData.Length * 2];

        // 縮放因子：將 -1.0 到 1.0 的 float 轉換為 -32767 到 32767 的 short
        const float rescale = 32767f;
        for (int i = 0; i < samples.Length; i++)
            intData[i] = (short)Mathf.Clamp(samples[i] * rescale, short.MinValue, short.MaxValue);

        // 將 short 陣列複製到 byte 陣列 (小端序)
        Buffer.BlockCopy(intData, 0, bytesData, 0, bytesData.Length);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        int byteRate = sampleRate * 2; // mono (1 channel) * 16-bit (2 bytes)

        // RIFF 區塊 (標頭)
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + bytesData.Length); // 檔案總大小 - 8
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        // FMT 區塊 (格式塊)
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);               // Sub-chunk size (16 for PCM)
        bw.Write((short)1);         // Audio Format (1 = PCM)
        bw.Write((short)1);         // Channels (Mono)
        bw.Write(sampleRate);       // Sample Rate
        bw.Write(byteRate);         // Byte Rate (SampleRate * Channels * BitsPerSample / 8)
        bw.Write((short)2);         // Block Align (Channels * BitsPerSample / 8)
        bw.Write((short)16);        // Bits Per Sample

        // DATA 區塊
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(bytesData.Length); // Data size
        bw.Write(bytesData);        // Audio data
        
        bw.Flush();
        return ms.ToArray();
    }
}
