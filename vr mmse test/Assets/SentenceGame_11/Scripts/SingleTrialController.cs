using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SingleTrialController : MonoBehaviour
{
    [Header("Refs")]
    public MicRecorder recorder;
    public AsrClient client;

    [Header("UI")]
    public TextMeshProUGUI titleText;     // Title
    public TextMeshProUGUI subtitleText;  // 倒數/狀態
    public Image countdownFill;           // Image: Filled/Radial
    public Image levelFill;               // Image: Filled/Horizontal
    public GameObject resultPanel;
    public TextMeshProUGUI resultText;

    [Header("Config")]
    public float maxSeconds = 10f;

    private float tRemain;
    private bool recording;

    void Start()
    {
        if (resultPanel) resultPanel.SetActive(false);
        if (titleText) titleText.text = "請說出一句完整的句子";
        if (subtitleText) subtitleText.text = "按下開始錄音";

        if (countdownFill) { countdownFill.type = Image.Type.Filled; countdownFill.fillMethod = Image.FillMethod.Radial360; countdownFill.fillAmount = 0f; }
        if (levelFill) { levelFill.type = Image.Type.Filled; levelFill.fillMethod = Image.FillMethod.Horizontal; levelFill.fillAmount = 0f; }

        if (recorder != null)
        {
            recorder.OnLevel += (lv) => { if (levelFill) levelFill.fillAmount = Mathf.Clamp01(lv); };
            recorder.OnWavReady += OnWavReady;
        }
    }

    // 綁到 ActivationButton 的 OnClick
    public void OnAction()
    {
        if (!recording) StartTrial();
        else StopTrial();
    }

    void StartTrial()
    {
        if (resultPanel) resultPanel.SetActive(false);
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
                if (resultPanel) resultPanel.SetActive(true);
                if (resp != null && !string.IsNullOrEmpty(resp.transcript))
                {
                    string reason = (resp.reasons != null)
                        ? $"(主謂:{resp.reasons.has_subject_verb}, 可理解:{resp.reasons.understandable})"
                        : "";
                    if (resultText) resultText.text = $"辨識：{resp.transcript}\n分數：{resp.score} {reason}";
                    if (titleText) titleText.text = resp.score == 1 ? "你已完成" : "請再試一次";
                    if (subtitleText) subtitleText.text = "點按按鈕可重來";
                }
                else
                {
                    if (resultText) resultText.text = "伺服器回傳格式不正確";
                    if (subtitleText) subtitleText.text = "請重試";
                }
            },
            onError: (err) =>
            {
                if (resultPanel) resultPanel.SetActive(true);
                if (resultText) resultText.text = $"上傳/判分失敗：{err}";
                if (titleText) titleText.text = "連線失敗";
                if (subtitleText) subtitleText.text = "請確認伺服器與IP";
            },
            onProgress: (phase, p) =>
            {
                if (subtitleText) subtitleText.text = $"{phase} {Mathf.RoundToInt(p * 100)}%";
            }
        ));
    }
}
