using System;
using System.IO;
using UnityEngine;

public class MicRecorder : MonoBehaviour
{
    [Header("Recording Settings")]
    public int sampleRate = 44100;
    public int maxRecordSeconds = 10;
    [NonSerialized] public string selectedDevice;

    private AudioClip _clip;
    private float[] _samples;
    private bool _recording;
    private int _pos;

    public event Action<float> OnLevel;     // 0..1 音量
    public event Action OnTick;             // 每幀回呼
    public event Action<byte[]> OnWavReady; // 錄完給 wav bytes

    void Awake()
    {
        if (Microphone.devices.Length > 0)
        {
            selectedDevice = Microphone.devices[0];
            Debug.Log($"[MicRecorder] Using device: {selectedDevice}");
        }
        else
        {
            selectedDevice = null;
            Debug.LogWarning("[MicRecorder] No microphone detected. Will use dummy audio for demo.");
        }
    }

    public void StartRecord()
    {
        if (_recording) return;

        _samples = new float[sampleRate * maxRecordSeconds];
        _pos = 0;
        _recording = true;

        if (selectedDevice != null)
        {
            try
            {
                _clip = Microphone.Start(selectedDevice, false, maxRecordSeconds, sampleRate);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MicRecorder] Microphone.Start failed, fallback to dummy. {e.Message}");
                _clip = CreateDummyClip();
            }
        }
        else
        {
            _clip = CreateDummyClip();
        }
    }

    public void StopRecord()
    {
        if (!_recording) return;
        _recording = false;

        if (selectedDevice != null)
            Microphone.End(selectedDevice);

        int length = Mathf.Clamp(_pos, 0, _samples?.Length ?? 0);
        if (length <= 0)
        {
            Debug.LogWarning("[MicRecorder] No data captured, output 1s silence.");
            _samples = new float[sampleRate];
            length = _samples.Length;
        }

        var data = new float[length];
        Array.Copy(_samples, data, length);

        var wav = WavUtility.FromAudioFloat(data, sampleRate);
        OnWavReady?.Invoke(wav);
        Debug.Log($"[MicRecorder] Done. {length} samples ({length / (float)sampleRate:0.00}s)");
    }

    void Update()
    {
        if (!_recording || _clip == null) return;

        int micPos = 0;
        if (selectedDevice != null)
        {
            micPos = Microphone.GetPosition(selectedDevice);
            if (micPos < 0) return;
        }
        else
        {
            micPos = Mathf.Min(_pos + Mathf.RoundToInt(Time.deltaTime * sampleRate), _samples.Length);
        }

        if (_samples == null || _samples.Length != sampleRate * maxRecordSeconds)
            _samples = new float[sampleRate * maxRecordSeconds];

        if (selectedDevice != null)
        {
            _clip.GetData(_samples, 0);
        }
        else
        {
            for (int i = _pos; i < micPos; i++)
                _samples[i] = Mathf.Sin(2 * Mathf.PI * 440 * i / sampleRate) * 0.2f;
        }

        _pos = Mathf.Clamp(micPos, 0, _samples.Length);

        float rms = 0f;
        int count = Mathf.Min(1024, _pos);
        for (int i = 0; i < count; i++)
        {
            float v = _samples[_pos - 1 - i];
            rms += v * v;
        }
        rms = Mathf.Sqrt(rms / Mathf.Max(1, count));
        OnLevel?.Invoke(Mathf.Clamp01(rms * 10f));

        OnTick?.Invoke();

        if (_pos >= _samples.Length)
            StopRecord();
    }

    private AudioClip CreateDummyClip()
    {
        int samples = sampleRate * maxRecordSeconds;
        var clip = AudioClip.Create("DummyClip", samples, 1, sampleRate, false);
        var data = new float[samples];
        clip.SetData(data, 0);
        clip.hideFlags |= HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        return clip;
    }
}

public static class WavUtility
{
    public static byte[] FromAudioFloat(float[] samples, int sampleRate)
    {
        short[] intData = new short[samples.Length];
        byte[] bytesData = new byte[intData.Length * 2];

        const float rescale = 32767f;
        for (int i = 0; i < samples.Length; i++)
            intData[i] = (short)Mathf.Clamp(samples[i] * rescale, short.MinValue, short.MaxValue); // ✅ 修正這行

        Buffer.BlockCopy(intData, 0, bytesData, 0, bytesData.Length);

        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms);
        int byteRate = sampleRate * 2; // mono 16-bit

        bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        bw.Write(36 + bytesData.Length);
        bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

        bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        bw.Write(16);
        bw.Write((short)1);             // PCM
        bw.Write((short)1);             // channels
        bw.Write(sampleRate);
        bw.Write(byteRate);
        bw.Write((short)2);             // block align
        bw.Write((short)16);            // bits

        bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        bw.Write(bytesData.Length);
        bw.Write(bytesData);
        bw.Flush();
        return ms.ToArray();
    }
}
