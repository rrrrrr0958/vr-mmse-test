using System;
using System.IO;
using UnityEngine;

public class MicRecorder : MonoBehaviour
{
    public int sampleRate = 16000;
    public int maxRecordSeconds = 10;
    public string selectedDevice;
    AudioClip _clip;
    float[] _samples;
    bool _recording;
    int _pos;

    public event Action<float> OnLevel;         // 0..1 音量
    public event Action OnTick;                 // 每幀回呼
    public event Action<byte[]> OnWavReady;     // 錄完給 wav bytes

    void Awake()
    {
        if (Microphone.devices.Length > 0)
            selectedDevice = Microphone.devices[0];
        else
            Debug.LogWarning("No microphone found.");
    }

    public void StartRecord()
    {
        if (_recording) return;
        _clip = Microphone.Start(selectedDevice, false, maxRecordSeconds, sampleRate);
        _recording = true;
        _pos = 0;
    }

    public void StopRecord()
    {
        if (!_recording) return;
        Microphone.End(selectedDevice);
        _recording = false;

        // 取實際長度樣本
        int length = _pos;
        var data = new float[length];
        Array.Copy(_samples, data, length);

        // 轉 wav
        var wav = WavUtility.FromAudioFloat(data, sampleRate);
        OnWavReady?.Invoke(wav);
    }

    void Update()
    {
        if (!_recording || _clip == null) return;

        int micPos = Microphone.GetPosition(selectedDevice);
        if (micPos < 0) return;

        if (_samples == null || _samples.Length != sampleRate * maxRecordSeconds)
            _samples = new float[sampleRate * maxRecordSeconds];

        int read = _clip.GetData(_samples, 0) ? micPos : 0;
        _pos = Mathf.Clamp(read, 0, _samples.Length);

        // 音量估計（RMS）
        float rms = 0f;
        int count = Mathf.Min(1024, _pos);
        for (int i = 0; i < count; i++) rms += _samples[_pos - 1 - i] * _samples[_pos - 1 - i];
        rms = Mathf.Sqrt(rms / Mathf.Max(1, count));
        OnLevel?.Invoke(Mathf.Clamp01(rms * 10f));

        OnTick?.Invoke();

        // 自動到時停止
        if (_pos >= _samples.Length)
            StopRecord();
    }
}

// ===== 小型 WAV 工具（PCM16）=====
public static class WavUtility
{
    public static byte[] FromAudioFloat(float[] samples, int sampleRate)
    {
        short[] intData = new short[samples.Length];
        byte[] bytesData = new byte[intData.Length * 2];

        const float rescale = 32767f;
        for (int i = 0; i < samples.Length; i++)
            intData[i] = (short)Mathf.Clamp(samples[i] * rescale, short.MinValue, short.MaxValue);

        Buffer.BlockCopy(intData, 0, bytesData, 0, bytesData.Length);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        int byteRate = sampleRate * 2; // mono 16-bit
        // RIFF header
        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + bytesData.Length);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        // fmt
        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);             // PCM
        bw.Write((short)1);             // channels
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((short)2);             // block align
        bw.Write((short)16);            // bits
        // data
        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(bytesData.Length);
        bw.Write(bytesData);
        bw.Flush();
        return ms.ToArray();
    }
}
