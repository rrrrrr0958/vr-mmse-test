// Assets/Scripts/LevelIntro/IntroSceneDirector.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using System.Collections;
using System.Collections.Generic;

public class IntroSceneDirector : MonoBehaviour
{
    [Header("UI 與語音")]
    public TextMeshProUGUI messageText;
    public AudioSource audioSource;
    [Tooltip("第一～第十關語音 (index = 關卡 - 1)")]
    public AudioClip[] stageVoiceClips;

    [Header("每關顯示文字")]
    public List<string> stageMessages = new List<string>
    {
        "第一關",
        "第二關",
        "第三關",
        "第四關",
        "第五關",
        "第六關",
        "第七關",
        "第八關",
        "第九關",
        "第十關"
    };

    [Header("顯示設定")]
    public float fadeInSeconds = 0.5f;
    public float fadeOutSeconds = 0.8f;

    private CanvasGroup cg;
    private int stageNumber;
    private string targetScene;

    void Start()
    {
        cg = messageText.GetComponentInParent<CanvasGroup>();
        if (!cg) cg = messageText.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 0;

        // 取得下一個目標場景
        targetScene = PlayerPrefs.GetString("NextTargetScene", "");
        stageNumber = GetStageNumber(targetScene);

        // 顯示第 X 關
        messageText.text = stageMessages[Mathf.Clamp(stageNumber - 1, 0, stageMessages.Count - 1)];

        // 播語音
        if (stageNumber - 1 < stageVoiceClips.Length && stageVoiceClips[stageNumber - 1])
        {
            audioSource.clip = stageVoiceClips[stageNumber - 1];
            audioSource.Play();
            StartCoroutine(PlayThenLoad(targetScene, audioSource.clip.length));
        }
        else
        {
            StartCoroutine(PlayThenLoad(targetScene, 2f)); // 沒語音時等兩秒
        }
    }

    IEnumerator PlayThenLoad(string nextScene, float waitTime)
    {
        // 淡入
        for (float t = 0; t < fadeInSeconds; t += Time.deltaTime)
        {
            cg.alpha = t / fadeInSeconds;
            yield return null;
        }
        cg.alpha = 1;

        yield return new WaitForSeconds(waitTime);

        // 淡出
        for (float t = 0; t < fadeOutSeconds; t += Time.deltaTime)
        {
            cg.alpha = 1 - t / fadeOutSeconds;
            yield return null;
        }

        SceneManager.LoadScene(nextScene);
    }

    int GetStageNumber(string sceneName)
    {
        // 你原本的關卡順序：
        var sceneMap = new Dictionary<string, int>()
        {
            {"SampleScene_7", 1},
            {"SampleScene_14", 2},
            {"SentenceGame_13", 3},
            {"SampleScene_3", 4},
            {"SampleScene_2", 5},
            {"SampleScene_5", 6},
            {"SampleScene_11_1", 7}, {"SampleScene_11", 7},
            {"f1_8", 8},
            {"SampleScene_11", 9},
            {"SampleScene_6", 10}
        };

        if (sceneMap.ContainsKey(sceneName))
            return sceneMap[sceneName];
        return 1;
    }
}
