using UnityEngine;
using System.IO;

public static class WavUtility
{
    private const int HEADER_SIZE = 44; // WAV �ɮ��Y���зǤj�p

    // �N AudioClip �ഫ�� WAV �榡�� Byte �}�C
    public static byte[] FromAudioClip(AudioClip clip)
    {
        using (System.IO.MemoryStream stream = new System.IO.MemoryStream())
        {
            // �g�J WAV �ɮ��Y
            WriteHeader(stream, clip);

            // �g�J���T���
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
        // �g�J "RIFF" ���� (4 bytes)
        stream.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"), 0, 4);
        // �g�J �ɮ��`���� - 8 (4 bytes)
        int fileSize = (int)stream.Length + HEADER_SIZE + (clip.samples * clip.channels * 2) - 8;
        stream.Write(System.BitConverter.GetBytes(fileSize), 0, 4);
        // �g�J "WAVE" ���� (4 bytes)
        stream.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"), 0, 4);

        // �g�J "fmt " �l�� (4 bytes)
        stream.Write(System.Text.Encoding.ASCII.GetBytes("fmt "), 0, 4);
        // �g�J �榡�l������ (16) (4 bytes)
        stream.Write(System.BitConverter.GetBytes(16), 0, 4);
        // �g�J ���T�榡 (1 = PCM) (2 bytes)
        stream.Write(System.BitConverter.GetBytes((ushort)1), 0, 2);
        // �g�J �n�D�� (2 bytes)
        stream.Write(System.BitConverter.GetBytes((ushort)clip.channels), 0, 2);
        // �g�J ���˲v (4 bytes)
        stream.Write(System.BitConverter.GetBytes(clip.frequency), 0, 4);
        // �g�J Byte Rate (4 bytes)
        int byteRate = clip.frequency * clip.channels * 2; // (���˲v * �n�D�� * 2 bytes/sample)
        stream.Write(System.BitConverter.GetBytes(byteRate), 0, 4);
        // �g�J Block Align (2 bytes)
        ushort blockAlign = (ushort)(clip.channels * 2); // (�n�D�� * 2 bytes/sample)
        stream.Write(System.BitConverter.GetBytes(blockAlign), 0, 2);
        // �g�J Bits Per Sample (16) (2 bytes)
        stream.Write(System.BitConverter.GetBytes((ushort)16), 0, 2);

        // �g�J "data" �l�� (4 bytes)
        stream.Write(System.Text.Encoding.ASCII.GetBytes("data"), 0, 4);
        // �g�J ���T��ƪ��� (4 bytes)
        int dataSize = clip.samples * clip.channels * 2;
        stream.Write(System.BitConverter.GetBytes(dataSize), 0, 4);
    }
}