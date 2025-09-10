using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class SceneFlowManager : MonoBehaviour
{
    public static SceneFlowManager instance;

    // 場景切換順序
    private readonly List<string> sceneOrder = new List<string>
    {
        "SampleScene_11_1",
        "SampleScene_11",
        "SampleScene_2",
        "SampleScene_11"
    };

    private int currentIndex = 0;

    [Header("Fade UI")]
    public Image fadeImage;             // 指到一個黑色全螢幕 Image
    public float fadeDuration = 3f;   // 淡入/淡出時間

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadNextScene()
    {
        currentIndex++;
        if (currentIndex >= sceneOrder.Count)
        {
            Debug.Log("流程結束，回到第一個場景");
            currentIndex = 0; // 如果你要循環
        }

        string nextScene = sceneOrder[currentIndex];
        StartCoroutine(LoadSceneRoutine(nextScene));
    }

    private IEnumerator LoadSceneRoutine(string nextScene)
    {
        // 1. 黑幕淡入
        yield return StartCoroutine(Fade(0f, 1f));

        // 2. 開始非阻塞載入
        AsyncOperation op = SceneManager.LoadSceneAsync(nextScene, LoadSceneMode.Single);
        op.allowSceneActivation = false;

        while (op.progress < 0.9f) yield return null;
        op.allowSceneActivation = true;

        // 3. 等一幀，讓場景物件初始化
        yield return null;

        // 3.5 額外等 XR Origin 初始化（避免視角卡住）
        yield return new WaitForSeconds(3f);

        // 4. 黑幕淡出
        yield return StartCoroutine(Fade(1f, 0f));
    }


    private IEnumerator Fade(float from, float to)
    {
        if (fadeImage == null) yield break;

        float t = 0f;
        Color c = fadeImage.color;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float a = Mathf.Lerp(from, to, t / fadeDuration);
            fadeImage.color = new Color(c.r, c.g, c.b, a);
            yield return null;
        }

        fadeImage.color = new Color(c.r, c.g, c.b, to);
    }
}
