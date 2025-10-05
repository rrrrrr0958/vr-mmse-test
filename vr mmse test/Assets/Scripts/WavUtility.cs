using UnityEngine;
using System.IO;

public static class WavUtility
{
    private const int HEADER_SIZE = 44; // WAV 檔案頭的標準大小

    // 將 AudioClip 轉換為 WAV 格式的 Byte 陣列
    public static byte[] FromAudioClip(AudioClip clip)
    {
        using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
        {
            // 寫入 WAV 檔案頭
            WriteHeader(stream, clip);

            // 寫入音訊資料
            float[] samples = new float[clip.samples * clip.channels];
            clip.GetData(samples, 0);

            byte[] bytes = new byte[samples.Length * 2]; // 16 bit = 2 bytes
            int byteIndex = 0;
            foreach (float sample in samples)
            {
                short shortSample = (short)(sample * short.MaxValue);
                bytes[byteIndex++] = (byte)(shortSample & 0xFF);
                bytes[byteIndex++] = (byte)((shortSample >> 8) & 0xFF);
            }
            stream.Write(bytes, 0, bytes.Length);

            return stream.ToArray();
        }
    }

    private static void WriteHeader(System.IO.MemoryStream stream, AudioClip clip)
    {
        // 寫入 "RIFF" 標識 (4 bytes)
        stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, 4);
        // 寫入 檔案總長度 - 8 (4 bytes)
        int fileSize = (int)stream.Length + HEADER_SIZE + (clip.samples * clip.channels * 2) - 8;
        stream.Write(System.BitConverter.GetBytes(fileSize), 0, 4);
        // 寫入 "WAVE" 標識 (4 bytes)
        stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, 4);

        // 寫入 "fmt " 子塊 (4 bytes)
        stream.Write(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, 4);
        // 寫入 格式子塊長度 (16) (4 bytes)
        stream.Write(System.BitConverter.GetBytes(16), 0, 4);
        // 寫入 音訊格式 (1 = PCM) (2 bytes)
        stream.Write(System.BitConverter.GetBytes((ushort)1), 0, 2);
        // 寫入 聲道數 (2 bytes)
        stream.Write(System.BitConverter.GetBytes((ushort)clip.channels), 0, 2);
        // 寫入 取樣率 (4 bytes)
        stream.Write(System.BitConverter.GetBytes(clip.frequency), 0, 4);
        // 寫入 Byte Rate (4 bytes)
        int byteRate = clip.frequency * clip.channels * 2; // (取樣率 * 聲道數 * 2 bytes/sample)
        stream.Write(System.BitConverter.GetBytes(byteRate), 0, 4);
        // 寫入 Block Align (2 bytes)
        ushort blockAlign = (ushort)(clip.channels * 2); // (聲道數 * 2 bytes/sample)
        stream.Write(System.BitConverter.GetBytes(blockAlign), 0, 2);
        // 寫入 Bits Per Sample (16) (2 bytes)
        stream.Write(System.BitConverter.GetBytes((ushort)16), 0, 2);

        // 寫入 "data" 子塊 (4 bytes)
        stream.Write(System.Text.Encoding.ASCII.GetBytes("data"), 0, 4);
        // 寫入 音訊資料長度 (4 bytes)
        int dataSize = clip.samples * clip.channels * 2;
        stream.Write(System.BitConverter.GetBytes(dataSize), 0, 4);
    }
}