using System.Collections;
using UnityEngine;
using TMPro;
using System;
using System.IO;
using UnityEngine.UI;
using System.Collections.Generic;


public class RecordingState2 : MonoBehaviour
{
    private FirebaseManager_Firestore FirebaseManager;

    // 顯示錄音階段的狀態
    public TextMeshProUGUI statusText;
    public Image progressBar; 
    // ⚠️ 確保在 Inspector 中將 AudioToServerSender 組件拖曳到此處
    public AudioToServerSender audioSender; 
    
    [Header("錄音完成後播放的音檔")]
    public AudioSource audioSource;      // 播放音效的 AudioSource
    public AudioClip endClip;            // 錄音結束後播放的音檔
    public int currentQuestionIndex = 0;
    public string saveFolder = "Game_2";
    // 從第一個參考程式碼中複製過來的 WAV 轉換工具
    private byte[] ConvertAudioClipToWav(AudioClip clip)
    {
        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        int sampleCount = samples.Length;
        int sampleRate = clip.frequency;
        int channels = clip.channels;
        int byteRate = sampleRate * channels * 2; // 16-bit = 2 bytes

        using (MemoryStream stream = new MemoryStream())
        using (BinaryWriter writer = new BinaryWriter(stream))
        {
            // === WAV Header ===
            writer.Write(System.Text.Encoding.UTF8.GetBytes("RIFF"));
            writer.Write(36 + sampleCount * 2);
            writer.Write(System.Text.Encoding.UTF8.GetBytes("WAVE"));

            // fmt chunk
            writer.Write(System.Text.Encoding.UTF8.GetBytes("fmt "));
            writer.Write(16); // Subchunk1Size
            writer.Write((short)1); // PCM format
            writer.Write((short)channels);
            writer.Write(sampleRate);
            writer.Write(byteRate);
            writer.Write((short)(channels * 2));
            writer.Write((short)16); // Bits per sample

            // data chunk
            writer.Write(System.Text.Encoding.UTF8.GetBytes("data"));
            writer.Write(sampleCount * 2);

            // === 寫入音訊樣本 ===
            foreach (var sample in samples)
            {
                short intData = (short)(Mathf.Clamp(sample, -1f, 1f) * short.MaxValue);
                writer.Write(intData);
            }

            writer.Flush();
            return stream.ToArray();
        }
    }


    public void StartRecording(float duration, int questionIndex)
    {
        currentQuestionIndex = questionIndex; 
        StartCoroutine(RecordPlayerVoice(duration));
    }
    
    // 如果你的 StartRecording 只接收 duration，請使用這個重載方法：
    /*
    public void StartRecording(float duration)
    {
        // ⚠️ 注意：如果你的邏輯不在這裡提供 index，你可能需要從別處獲取
        StartCoroutine(RecordPlayerVoice(duration));
    }
    */


    IEnumerator RecordPlayerVoice(float duration)
    {
        if (Microphone.devices.Length == 0)
        { 
            statusText.text = "沒有偵測到麥克風"; 
            yield break; 
        } 

        string micName = Microphone.devices[0]; 
        int sampleRate = 44100; 
        AudioClip clip = Microphone.Start(micName, false, Mathf.CeilToInt(duration), sampleRate); 

        float remaining = duration;

        // ✅ 初始化進度條（滿格）
        if (progressBar != null)
        {
            progressBar.gameObject.SetActive(true);
            progressBar.fillAmount = 1f;
        }

        // ✅ 倒數與進度條同步更新
        while (remaining > 0f)
        {
            if (progressBar != null)
                progressBar.fillAmount = remaining / duration;

            statusText.text = $"錄音中... 剩餘 {Mathf.CeilToInt(remaining)} 秒";
            yield return new WaitForSeconds(1f);
            remaining -= 1f;
        }

        Microphone.End(micName);
        statusText.text = "錄音完成，正在傳送...";

        // ✅ 錄音結束時關閉進度條
        if (progressBar != null)
            progressBar.gameObject.SetActive(false);
        
        // ✅ 播放錄音結束提示音
        if (audioSource != null && endClip != null)
        {
            audioSource.PlayOneShot(endClip);
            Debug.Log("[RecordingState2] 播放錄音結束提示音");
        }

        // ✅ 轉成 WAV 資料
        byte[] wavData = ConvertAudioClipToWav(clip);

        // 上傳 Firebase
        string testId = FirebaseManager_Firestore.Instance.testId;
        string levelIndex = "6";
        var files = new Dictionary<string, byte[]>();
        files["重複語句_wavData"] = wavData;
        FirebaseManager_Firestore.Instance.UploadFilesAndSaveUrls(testId, levelIndex, files);

        // ✅ 本地存檔：Assets/Scripts/Game_2/
        try
        {
            string folderPath = Path.Combine(Application.dataPath, "Scripts", saveFolder);
            Directory.CreateDirectory(folderPath); // 自動建立資料夾

            string timeStamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            string filePath = Path.Combine(folderPath, $"record_{timeStamp}.wav");

            File.WriteAllBytes(filePath, wavData);
            Debug.Log($"[RecordingState2] 已存錄音檔案：{filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[RecordingState2] 存檔失敗：{ex.Message}");
        }

        // ✅ 傳送 byte[] 給 Flask 辨識
        if (audioSender != null)
        {
            audioSender.SendAudioForRecognition(wavData, currentQuestionIndex);
        }
        else
        {
            Debug.LogError("[RecordingState2] AudioToServerSender 未連結！");
        }

        // ✅ 錄音結束後清除
        if (progressBar != null)
            progressBar.fillAmount = 0f;

        Destroy(clip);
    }

}
