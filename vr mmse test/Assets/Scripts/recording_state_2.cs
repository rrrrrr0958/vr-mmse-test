using UnityEngine;
using System.Collections;
using TMPro;
using System.IO;
using System;

public class RecordingState2 : MonoBehaviour
{
    public TextMeshProUGUI statusText;
    public HostFlask2 flaskHost;  // 連結 Flask 傳送腳本

    public void StartRecording(float duration)
    {
        StartCoroutine(RecordPlayerVoice(duration));
    }

    IEnumerator RecordPlayerVoice(float duration)
    {
        statusText.text = "錄音中...";
        string micName = Microphone.devices[0];
        AudioClip clip = Microphone.Start(micName, false, (int)duration, 44100);

        yield return new WaitForSeconds(duration);

        Microphone.End(micName);
        statusText.text = "錄音完成";

        // 產生唯一檔名（使用時間戳記）
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"recorded_{timestamp}.wav";
        string filePath = Path.Combine(Application.persistentDataPath, fileName);

        SaveWave.SavWav.Save(clip, fileName);

        // 顯示儲存位置（可選）
        Debug.Log("儲存音檔：" + filePath);

        // 傳送到 Flask
        flaskHost.SendFileToWhisper(filePath);
    }
}
