// SingleTrialController_13.cs
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;


public class SingleTrialController : MonoBehaviour
{
    private FirebaseManager_Firestore FirebaseManager;

    [Header("Refs")]
    public MicRecorder recorder;
    public AsrClient client;

    [Header("UI")]
    public TextMeshProUGUI titleText; 	// 標題
    public TextMeshProUGUI subtitleText; // 狀態/倒數
    public Image countdownFill; 		// Image: Filled/Radial
    public Image levelFill; 			// Image: Filled/Horizontal

    [Header("Config")]
    public float maxSeconds = 10f;

    [Header("Auto flow")]
    public bool autoStartOnSceneLoad = true; 	// 場景載入自動流程
    public AudioSource promptSource; 			// 播題目用 AudioSource
    public AudioClip promptClip; 				// 題目音檔
    public float delayBeforePrompt = 0.2f; 	// 場景穩定一下再播
    public float delayAfterPrompt = 0.0f; 	// 題目播完後緩衝（你希望立刻錄音就設 0）

    [Header("DSP Scheduling")]
    public bool useDspScheduling = true; 		// 使用 DSP 精準排程讓題目一結束就錄音
    public double dspLeadIn = 0.03; 			// 預留少量啟動緩衝

    // [Header("Diagnostics")]
    // public bool verboseLog = true; // <--- 移除此行
    public float waitMaxGuardSeconds = 10f;

    private float tRemain;
    private bool recording;
    private bool isProcessingComplete = false; // <-- 新增此旗標

    void Start()
    {
        Debug.Log("[SingleTrial] Scene started. Initializing UI and Recorder."); // 加入日誌
        if (titleText) titleText.text = "請聽題目";
        if (subtitleText) subtitleText.text = "準備中…";

        if (countdownFill)
        {
            countdownFill.type = Image.Type.Filled;
            countdownFill.fillMethod = Image.FillMethod.Radial360;
            countdownFill.fillAmount = 0f;
        }
        if (levelFill)
        {
            levelFill.type = Image.Type.Filled;
            levelFill.fillMethod = Image.FillMethod.Horizontal;
            levelFill.fillAmount = 0f;
        }

        if (recorder != null)
        {
            recorder.OnLevel += (lv) =>
            {
                if (levelFill) levelFill.fillAmount = Mathf.Clamp01(lv);
            };
            recorder.OnWavReady += OnWavReady; 	// 錄完觸發
        }
        else
        {
            Debug.LogError("[SingleTrial] Recorder is NULL.");
        }

        if (autoStartOnSceneLoad)
            StartCoroutine(AutoFlowRoutine());
        else
            if (subtitleText) subtitleText.text = "就緒";
    }

    IEnumerator AutoFlowRoutine()
    {
        Debug.Log($"[SingleTrial] Auto flow starting. Delay {delayBeforePrompt:F2}s."); // 加入日誌
        if (delayBeforePrompt > 0f)
            yield return new WaitForSecondsRealtime(delayBeforePrompt);

        if (promptSource && promptClip)
        {
            if (titleText) titleText.text = "請聽題目";
            if (subtitleText) subtitleText.text = "播放中…";

            // --- 播放設定 ---
            promptSource.Stop();
            promptSource.clip = promptClip;
            promptSource.loop = false;

            // 1) 計算實際播放秒數（考慮 pitch）
            double duration = promptClip.length / Mathf.Max(0.01f, promptSource.pitch);
            Debug.Log($"[SingleTrial] clip='{promptClip.name}', len={promptClip.length:F3}s, pitch={promptSource.pitch:F3}, dur={duration:F3}s"); // 日誌

            // 2) 用 DSP 播，但用固定秒數等待（Hybrid）
            double startDsp = AudioSettings.dspTime + dspLeadIn; // 小緩衝避免丟頭
            promptSource.PlayScheduled(startDsp);
            Debug.Log($"[SingleTrial] Playing scheduled at DSP: {startDsp:F3}"); // 日誌

            // 等待「題目長度」這麼久（不受 timescale 影響）
            yield return new WaitForSecondsRealtime((float)duration + (float)dspLeadIn);
            Debug.Log($"[SingleTrial] Prompt duration finished. Waiting for tail guard."); // 日誌

            // 3) 尾端保險：最多再等 0.2s 或直到不再播放
            float tailGuard = 0f;
            while (promptSource.isPlaying && tailGuard < 0.2f)
            {
                tailGuard += Time.unscaledDeltaTime;
                yield return null;
            }

            // 4) 你若真的要 0 延遲，將 delayAfterPrompt 設 0
            if (delayAfterPrompt > 0f)
            {
                Debug.Log($"[SingleTrial] Waiting for delay after prompt: {delayAfterPrompt:F2}s"); // 日誌
                yield return new WaitForSecondsRealtime(delayAfterPrompt);
            }
        }
        else
        {
            Debug.LogWarning("[SingleTrial] Missing promptSource or promptClip. Skipping prompt.");
        }

        StartTrial(); // 題目結束 → 立刻開始錄音
    }

    void StartTrial()
    {
        if (recording) return;
        if (promptSource && promptSource.isPlaying) return; // 保險

        recording = true;
        tRemain = maxSeconds;

        if (titleText) titleText.text = "請說出你的答案";
        if (subtitleText) subtitleText.text = $"錄音中… {Mathf.CeilToInt(tRemain)} 秒";
        if (countdownFill) countdownFill.fillAmount = 0f;

        if (recorder != null)
        {
            Debug.Log($"[SingleTrial] StartRecord() for {maxSeconds:F1}s."); // 日誌
            recorder.StartRecord();
        }
        else
        {
            Debug.LogError("[SingleTrial] Recorder is null; cannot start recording.");
        }
    }

    void StopTrial()
    {
        if (!recording) return;
        recording = false;

        if (recorder != null)
        {
            Debug.Log("[SingleTrial] StopRecord()"); // 日誌
            recorder.StopRecord();
        }

        if (subtitleText) subtitleText.text = "上傳中…";
    }

    void Update()
    {
        if (!recording) return;

        tRemain -= Time.deltaTime;
        float progress = Mathf.Clamp01(1f - tRemain / maxSeconds);

        if (countdownFill) countdownFill.fillAmount = progress;
        if (subtitleText) subtitleText.text = $"錄音中… {Mathf.CeilToInt(Mathf.Max(0, tRemain))} 秒";

        if (tRemain <= 0f)
            StopTrial();
    }

    void OnWavReady(byte[] wav)
    {
        Debug.Log($"[SingleTrial] WAV ready. Byte size: {wav.Length}"); // 日誌

        if (client == null)
        {
            Debug.LogError("[SingleTrial] AsrClient is null.");
            if (titleText) titleText.text = "上傳失敗";
            if (subtitleText) subtitleText.text = "沒有 ASR 客戶端";
            return;
        }

        // 先存 WAV 到 Assets/Scripts/game_13（Editor）或 persistentDataPath/game_13（裝置）
        string savedWavPath = AsrResultLogger.SaveWav(wav);

        StartCoroutine(client.UploadWav(
      wav,
      onDone: (resp) =>
      {
          if (isProcessingComplete) return; // <-- 保護：如果已完成，直接退出
          isProcessingComplete = true; // <-- 成功後標記為已完成

          string text = resp?.transcript ?? resp?.transcription;
          int score = resp?.score ?? 0;

          // 變更：改為儲存 JSON(Overwrite)
          AsrResultLogger.OverwriteJson(resp, savedWavPath);
          Debug.Log($"[SingleTrial] ASR success. Transcript: '{text ?? ""}', Score: {score}."); // 日誌

          if (titleText) titleText.text = "錄音完成";
          if (subtitleText) subtitleText.text = "完成";
          string testId = FirebaseManager_Firestore.Instance.testId;
          string levelIndex = "3";
          FirebaseManager_Firestore.Instance.totalScore = FirebaseManager_Firestore.Instance.totalScore + score;
          FirebaseManager_Firestore.Instance.SaveLevelData(testId, levelIndex, score);
          var files = new Dictionary<string, byte[]>();
          files["sentence_wav"] = wav;
          FirebaseManager_Firestore.Instance.UploadFilesAndSaveUrls(testId, levelIndex, files);
          SceneFlowManager.instance.LoadNextScene(); // 成功後才切換場景
      },
      onError: (err) =>
      {
          if (isProcessingComplete) return; // <-- 保護：如果已完成，直接退出
          isProcessingComplete = true; // <-- 失敗後標記為已完成

          // ❗ 變更：失敗也儲存 JSON 紀錄
          AsrClient.GoogleASRResponse errorResp = new AsrClient.GoogleASRResponse
          {

              error = err,
              score = -1,
              transcript = $"<ERROR> {err}"
          };
          //AsrResultLogger.AppendJson(errorResp, savedWavPath);
          AsrResultLogger.OverwriteJson(errorResp, savedWavPath);

          if (titleText) titleText.text = "連線失敗";
          if (subtitleText) subtitleText.text = "請確認伺服器與IP";
          Debug.LogError($"[ASR] Upload/Score failed: {err}"); // 日誌
          string testId = FirebaseManager_Firestore.Instance.testId;
          string levelIndex = "3";
          FirebaseManager_Firestore.Instance.SaveLevelData(testId, levelIndex, 0);//score設定為0
          SceneFlowManager.instance.LoadNextScene(); // 失敗後也切換場景
      },
            onProgress: (phase, p) =>
            {
                if (subtitleText) subtitleText.text = $"{phase} {Mathf.RoundToInt(p * 100)}%";
            }
        ));
        // ❗ 注意：場景切換已移至 onDone/onError 內部，以等待 ASR 結果。
    }
}