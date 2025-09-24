using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SingleTrialController : MonoBehaviour
{
    [Header("Refs")]
    public MicRecorder mic;
    public AsrClient client;
    public TextMeshProUGUI titleText;      // "請說出一句完整的句子？"
    public TextMeshProUGUI subText;        // "按開始後有10秒..."
    public Image countdownFill;            // radial fill (0..1)
    public Image levelBar;                 // width/scale by level
    public Button actionButton;            // 角色切換：開始/完成
    public TextMeshProUGUI actionLabel;    // 按鈕文字
    public GameObject resultPanel;
    public TextMeshProUGUI resultText;     // 轉錄 + 通過/未通過

    enum State { Ready, Recording, Uploading, Result }
    State _state = State.Ready;
    float _timeLeft;

    void Start()
    {
        mic.OnLevel += OnLevel;
        mic.OnWavReady += OnWavReady;
        mic.OnTick += OnTick;
        SetupReady();
        actionButton.onClick.AddListener(OnAction);
    }

    void SetupReady()
    {
        _state = State.Ready;
        _timeLeft = mic.maxRecordSeconds;
        countdownFill.fillAmount = 0f;
        levelBar.fillAmount = 0f;
        resultPanel.SetActive(false);
        actionLabel.text = "開始";
        subText.text = "按「開始」後有 10 秒；說完可按「完成」。";
    }

    void OnAction()
    {
        if (_state == State.Ready)
        {
            _state = State.Recording;
            _timeLeft = mic.maxRecordSeconds;
            actionLabel.text = "完成";
            mic.StartRecord();
        }
        else if (_state == State.Recording)
        {
            mic.StopRecord(); // 會觸發 OnWavReady
        }
        else if (_state == State.Result)
        {
            SetupReady();
        }
    }

    void OnTick()
    {
        if (_state != State.Recording) return;
        _timeLeft -= Time.deltaTime;
        countdownFill.fillAmount = 1f - Mathf.Clamp01(_timeLeft / mic.maxRecordSeconds);
        if (_timeLeft <= 0f)
        {
            mic.StopRecord();
        }
    }

    void OnLevel(float level)
    {
        levelBar.fillAmount = Mathf.Lerp(levelBar.fillAmount, Mathf.Clamp01(level), 0.5f);
    }

    void OnWavReady(byte[] wav)
    {
        _state = State.Uploading;
        actionLabel.text = "上傳中…";
        StartCoroutine(client.PostWav(
            wav,
            onDone: (resp) =>
            {
                _state = State.Result;
                actionLabel.text = "再來一次";
                resultPanel.SetActive(true);
                var pass = resp.score == 1 ? "<color=#00C853>通過</color>" : "<color=#D50000>未通過</color>";
                resultText.text = $"ASR：{resp.transcript}\n結果：{pass}\n" +
                                  $"(主詞+動詞={resp.reasons.has_subject_verb}, 可理解={resp.reasons.understandable})";
            },
            onError: (err) =>
            {
                _state = State.Result;
                actionLabel.text = "再試一次";
                resultPanel.SetActive(true);
                resultText.text = $"上傳/判分失敗：{err}";
            }
        ));
    }
}
