using System.Collections;
using UnityEngine;
using TMPro;
using System;
using System.IO;

public class RecordingState2 : MonoBehaviour
{
    public TextMeshProUGUI statusText;
    public HostFlask2 flaskHost;

    public void StartRecording(float duration)
    {
        StartCoroutine(RecordPlayerVoice(duration));
    }

    IEnumerator RecordPlayerVoice(float duration)
    {
        statusText.text = "請直接說出來，錄音中...";
        if (Microphone.devices.Length == 0) {
            statusText.text = "沒有偵測到麥克風";
            yield break;
        }

        string micName = Microphone.devices[0];
        int sampleRate = 44100;
        AudioClip clip = Microphone.Start(micName, false, Mathf.CeilToInt(duration), sampleRate);

        yield return new WaitForSeconds(duration);

        Microphone.End(micName);
        statusText.text = "錄音完成";

        // save with timestamped filename
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string filename = $"recorded_{timestamp}.wav";
        string savedPath = SaveWave.SavWav.Save(clip, filename);

        Debug.Log("Saved wav: " + savedPath);
        statusText.text = "已儲存：" + Path.GetFileName(savedPath);

        // call flask
        flaskHost.SendFileToWhisper(savedPath);
    }
}
