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
    string _targetScene;       // 要進入的實際關卡（從 PlayerPrefs 讀）
    private int _stageNumber = 0;

    // ---- 進 Play 回合只重置一次的旗標（同一回合不影響關卡累計）----
    private static bool s_ResetDoneThisPlay = false;

    // ---- 在每次按下 Play 之前（載入任何場景前）執行，先把計數歸零 ----
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ResetStageIndexOnPlay()
    {
        PlayerPrefs.SetInt("IntroStageIndex", 0);
        PlayerPrefs.Save();
        s_ResetDoneThisPlay = true; // 標記此回合已經重置過
    }

    void Awake()
    {
        // 若因為某些設定（例如關掉 Domain Reload）導致上面的方法沒跑，
        // 這裡再做一次保險：本回合第一次看到這個腳本就清零一次。
        if (!s_ResetDoneThisPlay)
        {
            PlayerPrefs.SetInt("IntroStageIndex", 0);
            PlayerPrefs.Save();
            s_ResetDoneThisPlay = true;
        }

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
        // 0-based：每次「新按 Play」時已被歸零
        int stageIndex = PlayerPrefs.GetInt("IntroStageIndex", 0);
        _stageNumber = stageIndex;

        // 顯示文字
        int msgIdx = Mathf.Clamp(stageIndex, 0, stageMessages.Count - 1);
        if (messageText) messageText.text = stageMessages[msgIdx];

        // 讀下一關場景
        _targetScene = PlayerPrefs.GetString("NextTargetScene", "");

        // 播對應語音或等 2 秒
        float wait = 2f;
        if (stageVoiceClips != null && stageIndex < stageVoiceClips.Length && stageVoiceClips[stageIndex])
        {
            audioSource.clip = stageVoiceClips[stageIndex];
            audioSource.Play();
            wait = audioSource.clip.length;
        }

        StartCoroutine(PlayThenLoad(_targetScene, wait, stageIndex));
    }

    IEnumerator PlayThenLoad(string nextScene, float waitTime, int stageIndex)
    {
        // 淡入
        for (float t = 0f; t < fadeInSeconds; t += Time.deltaTime)
        {
            _cg.alpha = t / fadeInSeconds;
            yield return null;
        }
        _cg.alpha = 1f;

        yield return new WaitForSeconds(waitTime);

        // 淡出
        for (float t = 0f; t < fadeOutSeconds; t += Time.deltaTime)
        {
            _cg.alpha = 1f - (t / fadeOutSeconds);
            yield return null;
        }
        _cg.alpha = 0f;

        // 播完再遞增並保存（同一回合內有效）
        PlayerPrefs.SetInt("IntroStageIndex", stageIndex + 1);
        PlayerPrefs.Save();

        if (SceneFlowManager.instance != null)
            SceneFlowManager.instance.LoadNextScene();
        else if (!string.IsNullOrEmpty(nextScene))
            SceneManager.LoadScene(nextScene);
    }
}

/*
【若你在 Editor 開了「Enter Play Mode Options」且關閉了 Domain Reload】
建議再加一個 Editor 專用腳本（放在 Assets/Editor/ 任一 .cs 檔）：

using UnityEditor;
using UnityEditor.Callbacks;
using UnityEngine;

[InitializeOnLoad]
public static class IntroStageEditorReset
{
    static IntroStageEditorReset()
    {
        EditorApplication.playModeStateChanged += s =>
        {
            if (s == PlayModeStateChange.EnteredPlayMode)
            {
                PlayerPrefs.SetInt("IntroStageIndex", 0);
                PlayerPrefs.Save();
            }
        };
    }
}

這樣就算沒有 Domain Reload，每次進入 Play 也會把 IntroStageIndex 清成 0。
*/
