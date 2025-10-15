// Assets/Scripts/LevelIntro/IntroSceneDirector.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(AudioSource))]
public class IntroSceneDirector : MonoBehaviour
{
    [Header("UI 與語音")]
    public TextMeshProUGUI messageText;          // 若未指定會自動往子物件找
    public AudioSource audioSource;              // 若未指定會自動抓同物件的 AudioSource
    [Tooltip("第一～第十關語音 (index = 關卡 - 1)")]
    public AudioClip[] stageVoiceClips;          // 0=第一關, 1=第二關, ..., 9=第十關

    [Header("每關顯示文字")]
    public List<string> stageMessages = new List<string>
    {
        "第一關","第二關","第三關","第四關","第五關",
        "第六關","第七關","第八關","第九關","第十關"
    };

    [Header("顯示設定")]
    public float fadeInSeconds = 0.5f;
    public float fadeOutSeconds = 0.8f;

    CanvasGroup _cg;
    int _stageNumber;          // 這次要顯示的第幾關（每進來一次就+1）
    string _targetScene;       // 要進入的實際關卡（從 PlayerPrefs 讀）

    void Awake()
    {
        if (!audioSource) audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;

        if (!messageText) messageText = GetComponentInChildren<TextMeshProUGUI>(true);

        _cg = messageText ? messageText.GetComponentInParent<CanvasGroup>() : null;
        if (!_cg) _cg = gameObject.AddComponent<CanvasGroup>();
        _cg.alpha = 0f;
    }

    void Start()
    {
        // 1) 每進入一次 IntroScene，計數 +1 → 代表現在第幾關
        _stageNumber = PlayerPrefs.GetInt("IntroStageCount", 0) + 1;
        PlayerPrefs.SetInt("IntroStageCount", _stageNumber);
        // 可選：PlayerPrefs.Save();

        // 2) 顯示對應文字
        int msgIdx = Mathf.Clamp(_stageNumber - 1, 0, stageMessages.Count - 1);
        if (messageText) messageText.text = stageMessages[msgIdx];

        // 3) 讀取下一個目標關卡名稱（請在切到 GameIntroScene 之前寫入）
        _targetScene = PlayerPrefs.GetString("NextTargetScene", "");
        if (string.IsNullOrEmpty(_targetScene))
        {
            Debug.LogWarning("[Intro] PlayerPrefs.NextTargetScene 為空；無法在播完後切換場景。");
        }

        // 4) 播對應語音（若沒 clip 就等 2 秒）
        float wait = 2f;
        int clipIdx = Mathf.Clamp(_stageNumber - 1, 0, stageVoiceClips != null ? stageVoiceClips.Length - 1 : 0);
        if (stageVoiceClips != null && clipIdx < stageVoiceClips.Length && stageVoiceClips[clipIdx])
        {
            audioSource.clip = stageVoiceClips[clipIdx];
            audioSource.Play();
            wait = audioSource.clip.length;
        }

        StartCoroutine(PlayThenLoad(_targetScene, wait));
    }

    IEnumerator PlayThenLoad(string nextScene, float waitTime)
    {
        // 淡入
        for (float t = 0f; t < fadeInSeconds; t += Time.deltaTime)
        {
            _cg.alpha = t / fadeInSeconds;
            yield return null;
        }
        _cg.alpha = 1f;

        // 停留（語音長度或 2 秒）
        yield return new WaitForSeconds(waitTime);

        // 淡出
        for (float t = 0f; t < fadeOutSeconds; t += Time.deltaTime)
        {
            _cg.alpha = 1f - (t / fadeOutSeconds);
            yield return null;
        }
        _cg.alpha = 0f;

        if (!string.IsNullOrEmpty(nextScene))
            SceneManager.LoadScene(nextScene);
    }
}
