using System.Collections;
using UnityEngine;
using TMPro;
using System;
using System.IO;

public class RecordingState2 : MonoBehaviour
{
    public TextMeshProUGUI statusText;
    // ⚠️ 將 HostFlask2 替換為新的類別名稱
    public AudioToServerSender audioSender; 
    
    public int currentQuestionIndex = 0; 
    
    // 從第一個參考程式碼中複製過來的 WAV 轉換工具
    private byte[] ConvertAudioClipToWav(AudioClip clip)
    {
        const int headerSize = 44;
        byte[] bytes = new byte[clip.samples * 2 * clip.channels + headerSize];

        int format = 1;
        int channels = clip.channels;
        int sampleRate = clip.frequency;
        int bitDepth = 16;
        int byteRate = sampleRate * channels * (bitDepth / 8);
        int blockAlign = channels * (bitDepth / 8);

        System.Text.Encoding.UTF8.GetBytes("RIFF").CopyTo(bytes, 0);
        System.BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
        System.Text.Encoding.UTF8.GetBytes("WAVE").CopyTo(bytes, 8);
        System.Text.Encoding.UTF8.GetBytes("fmt ").CopyTo(bytes, 12);
        System.BitConverter.GetBytes(16).CopyTo(bytes, 16);
        System.BitConverter.GetBytes((short)format).CopyTo(bytes, 20);
        System.BitConverter.GetBytes((short)channels).CopyTo(bytes, 22);
        System.BitConverter.GetBytes(sampleRate).CopyTo(bytes, 24);
        System.BitConverter.GetBytes(byteRate).CopyTo(bytes, 28);
        System.BitConverter.GetBytes((short)blockAlign).CopyTo(bytes, 32);
        System.BitConverter.GetBytes((short)bitDepth).CopyTo(bytes, 34);
        System.Text.Encoding.UTF8.GetBytes("data").CopyTo(bytes, 36);
        System.BitConverter.GetBytes(clip.samples * blockAlign).CopyTo(bytes, 40);

        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        for (int i = 0; i < samples.Length; i++)
        {
            short pcmValue = (short)(samples[i] * short.MaxValue);
            System.BitConverter.GetBytes(pcmValue).CopyTo(bytes, headerSize + i * 2);
        }

        return bytes;
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
        statusText.text = "錄音完成，正在傳送..."; 
        
        // 關鍵變更：不存檔，直接轉 byte[]
        byte[] wavData = ConvertAudioClipToWav(clip);
        
        // 釋放 AudioClip 資源
        Destroy(clip); 
        
        // 直接傳送 byte[] 數據給 Sender
        if (audioSender != null)
        {
            // 這裡傳遞 wavData 和當前問題索引
            audioSender.SendAudioForRecognition(wavData, currentQuestionIndex);
        }
        else
        {
            Debug.LogError("[RecordingState2] AudioToServerSender 未連結！");
            statusText.text = "錯誤：發送器未設定";
        }
    }
}