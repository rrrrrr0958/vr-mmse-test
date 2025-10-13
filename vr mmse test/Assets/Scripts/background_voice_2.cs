using UnityEngine;
using System.Collections;
using TMPro;

public class BackgroundVoice2 : MonoBehaviour
{
    [Header("元件參考")]
    public AudioSource broadcastSource; 	// 拖有 AudioSource 的物件
    public RecordingState2 recorder; 	// 你的錄音腳本
    // ⚠️ 修正點 1: 將 HostFlask2 替換為 AudioToServerSender
    public AudioToServerSender hostFlask; 	// Flask 上傳腳本（Flask_manager）
    public TextMeshProUGUI statusText; 	// 顯示提示文字

    [Header("前導音檔 (一開始播放)")]
    public AudioClip introClip;

    [Header("題目音檔")]
    public AudioClip clip1; 	// 海鮮折扣快來買
    // public AudioClip clip2; 	// 雞豬牛羊都有賣
    public AudioClip clip3; 	// 早起買菜精神好

    [Header("流程參數")]
    public float waitAfterIntro = 2f; 	// 前導播完後再等
    public float waitAfterQuestion = 0f; 	// 題目播完後再等
    public float recordDuration = 7f; 	// 錄音秒數

    private readonly string[] sentences = {
        "海鮮折扣快來買",
        // "雞豬牛羊都有賣",
        "早起買菜精神好"
    };

    void Start()
    {
        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        // 防呆
        if (broadcastSource == null) { Debug.LogError("[BackgroundVoice2] broadcastSource 未綁定"); yield break; }
        if (recorder == null) 	    { Debug.LogError("[BackgroundVoice2] recorder 未綁定"); 	    yield break; }
        // ⚠️ 修正點 2: hostFlask.targetSentence 已被移除，因為新邏輯不需要
        // 新邏輯只傳送音訊，不需要目標文字。這裡我們移除這行。
        // if (hostFlask == null) 	    { Debug.LogError("[BackgroundVoice2] hostFlask 未綁定"); 	    yield break; } // 保留 hostFlask 的防呆檢查
        if (statusText == null) 	{ Debug.LogError("[BackgroundVoice2] statusText 未綁定"); 	yield break; }

        // Step 0: 前導音檔
        if (introClip != null)
        {
            statusText.text = "請仔細聆聽";
            broadcastSource.clip = introClip;
            broadcastSource.Play();
            Debug.Log("[BackgroundVoice2] ▶ 前導音檔播放");
            yield return new WaitForSeconds(broadcastSource.clip.length + waitAfterIntro);
        }

        // Step 1: 隨機抽題
        int index = UnityEngine.Random.Range(0, sentences.Length);
        // hostFlask.targetSentence = sentences[index]; // ⚠️ 移除此行：新的 AudioToServerSender 沒有這個欄位
        
        // 對應題目音檔（已刪掉中間那題）
        AudioClip qClip = (index == 0) ? clip1 : clip3;
        
        string currentQuestionText = sentences[index]; // 記住題目文字，雖然這裡沒用到，但流程需要

        statusText.text = "請仔細聆聽";
        broadcastSource.clip = qClip;
        broadcastSource.Play();
        Debug.Log("[BackgroundVoice2] ▶ 題目播放：「" + currentQuestionText + "」");
        yield return new WaitForSeconds(broadcastSource.clip.length + waitAfterQuestion);

        // Step 2: 錄音
        statusText.text = "請直接說出來，錄音中...";
        
        // 由於你的錄音腳本 (RecordingState2) 現在需要問題索引 (index)
        // 請確保你的 RecordingState2 有 public void StartRecording(float duration, int questionIndex) 這個方法
        recorder.StartRecording(recordDuration, index); 

        // Step 3: 錄音結束後 → 提示正在辨識
        // ⚠️ 注意：這行程式碼會讓遊戲**繼續執行**，而不是等待辨識結果。
        // 正確做法是讓辨識邏輯在 SendAudioToServer 結束後，自行觸發下一步。
        yield return new WaitForSeconds(recordDuration);
        statusText.text = "錄音完成，正在辨識...";
    }
}