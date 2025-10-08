using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class SingleTrialController : MonoBehaviour
{
    [Header("Refs")]
    public MicRecorder recorder;
    public AsrClient client;

    [Header("UI")]
    public TextMeshProUGUI titleText;     // Title（可保留提示用）
    public TextMeshProUGUI subtitleText;  // 狀態/倒數
    public Image countdownFill;           // Image: Filled/Radial
    public Image levelFill;               // Image: Filled/Horizontal
    // public GameObject resultPanel;     // ← 不再使用
    // public TextMeshProUGUI resultText; // ← 不再使用

    [Header("Config")]
    public float maxSeconds = 10f;

    [Header("Auto flow")]
    public bool autoStartOnSceneLoad = true;   // 場景載入就自動流程
    public AudioSource promptSource;           // 場景裡的 AudioSource
    public AudioClip promptClip;               // 要播放的提示音
    public float delayBeforePrompt = 0.2f;     // 場景穩定一下再播
    public float delayAfterPrompt = 0.15f;     // 播完留個短緩衝再開錄

    private float tRemain;
    private bool recording;

    void Start()
    {
        if (titleText) titleText.text = "請說出一句完整的句子";
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
            recorder.OnLevel += (lv) => { if (levelFill) levelFill.fillAmount = Mathf.Clamp01(lv); };
            recorder.OnWavReady += OnWavReady;
        }

        if (autoStartOnSceneLoad)
            StartCoroutine(AutoFlowRoutine());
        else
            if (subtitleText) subtitleText.text = "就緒";
    }

    IEnumerator AutoFlowRoutine()
    {
        if (delayBeforePrompt > 0f) yield return new WaitForSecondsRealtime(delayBeforePrompt);

        if (promptSource && promptClip)
        {
            if (titleText) titleText.text = "請先聽範例語句";
            if (subtitleText) subtitleText.text = "播放中…";

            promptSource.Stop();
            promptSource.clip = promptClip;
            promptSource.Play();

            yield return new WaitWhile(() => promptSource.isPlaying);

            if (delayAfterPrompt > 0f) yield return new WaitForSecondsRealtime(delayAfterPrompt);
        }

        StartTrial();
    }

    void StartTrial()
    {
        if (recording) return;

        recording = true;
        tRemain = maxSeconds;

        if (titleText) titleText.text = "請說出一句完整的句子";
        if (subtitleText) subtitleText.text = $"錄音中… {Mathf.CeilToInt(tRemain)} 秒";
        if (countdownFill) countdownFill.fillAmount = 0f;

        recorder.StartRecord();
    }

    void StopTrial()
    {
        if (!recording) return;
        recording = false;
        recorder.StopRecord();
        if (subtitleText) subtitleText.text = "上傳中…";
    }

    void Update()
    {
        if (!recording) return;

        tRemain -= Time.deltaTime;
        float progress = Mathf.Clamp01(1f - tRemain / maxSeconds);
        if (countdownFill) countdownFill.fillAmount = progress;
        if (subtitleText) subtitleText.text = $"錄音中… {Mathf.CeilToInt(Mathf.Max(0, tRemain))} 秒";

        if (tRemain <= 0f) StopTrial();
    }

    void OnWavReady(byte[] wav)
    {
        StartCoroutine(client.UploadWav(
            wav,
            onDone: (resp) =>
            {
                // 取得文字（/score 或 /recognize_speech）
                string text = (resp != null)
                              ? (!string.IsNullOrEmpty(resp.transcript) ? resp.transcript : resp?.transcription)
                              : null;
                int score = resp?.score ?? 0;

                // 存檔 + Console
                AsrResultLogger.Append(text ?? "", score);

                if (titleText) titleText.text = "錄音完成";
                if (subtitleText) subtitleText.text = "完成";
            },
            onError: (err) =>
            {
                // 失敗也寫一筆（score = -1）
                AsrResultLogger.Append($"<ERROR> {err}", -1);

                if (titleText) titleText.text = "連線失敗";
                if (subtitleText) subtitleText.text = "請確認伺服器與IP";
                Debug.LogError($"[ASR] Upload/Score failed: {err}");
            },
            onProgress: (phase, p) =>
            {
                if (subtitleText) subtitleText.text = $"{phase} {Mathf.RoundToInt(p * 100)}%";
            }
        ));
        SceneFlowManager.instance.LoadNextScene();
    }
}
