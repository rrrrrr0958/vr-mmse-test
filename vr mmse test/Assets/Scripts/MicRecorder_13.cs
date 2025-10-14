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

    public event Action<float> OnLevel; 	// 0..1 音量
    public event Action OnTick; 			// 每幀回呼
    public event Action<byte[]> OnWavReady; // 錄完給 wav bytes

    void Awake()
    {
        if (Microphone.devices.Length > 0)
        {
            selectedDevice = Microphone.devices[0];
            Debug.Log($"[MicRecorder] Using device: {selectedDevice}"); // 日誌
        }
        else
        {
            selectedDevice = null;
            Debug.LogWarning("[MicRecorder] No microphone detected. Will use dummy audio for demo."); // 日誌
        }
    }

    public void StartRecord()
    {
        if (_recording) return;

        _samples = new float[sampleRate * maxRecordSeconds];
        _pos = 0;
        _recording = true;
        Debug.Log($"[MicRecorder] Start recording on device '{selectedDevice ?? "Dummy"}'. Max duration: {maxRecordSeconds}s."); // 日誌

        if (selectedDevice != null)
        {
            try
            {
                _clip = Microphone.Start(selectedDevice, false, maxRecordSeconds, sampleRate);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MicRecorder] Microphone.Start failed, fallback to dummy. {e.Message}"); // 日誌
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
            Debug.LogWarning("[MicRecorder] No data captured, output 1s silence."); // 日誌
            _samples = new float[sampleRate];
            length = _samples.Length;
        }

        var data = new float[length];
        Array.Copy(_samples, data, length);

        var wav = WavUtility.FromAudioFloat(data, sampleRate);
        OnWavReady?.Invoke(wav);
        Debug.Log($"[MicRecorder] Done. {length} samples ({length / (float)sampleRate:0.00}s). WAV ready."); // 日誌
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
