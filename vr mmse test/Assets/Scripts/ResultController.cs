using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class ResultController : MonoBehaviour
{
    [Header("UI Elements")]
    public TMP_Text messageText;
    public Button continueButton;

    [Header("Audio")]
    public AudioController audioController;

    [Header("Star Settings")]
    public GameObject[] stars;

    [Header("Animation Settings")]
    [Tooltip("第一顆星星出現前的延遲時間")]
    public float initialStarDelay = 0.5f; // ✅ 新增：第一顆星星出現前延遲
    [Tooltip("每顆星星之間的間隔時間")]
    public float starSpawnDelay = 0.22f;
    [Tooltip("星星全部出現後到文字出現的間隔時間")]
    public float delayAfterStars = 0.3f;
    [Tooltip("文字出現後到按鈕出現的間隔時間")]
    public float delayAfterMessage = 0.5f;

    [Header("Scene Settings")]
    public string nextSceneName = "NextScene";

    void Start()
    {
        InitializeUI();
        StartCoroutine(ShowResultSequence());
    }

    void InitializeUI()
    {
        // 隱藏訊息文字
        if (messageText != null)
        {
            messageText.text = "";
            messageText.gameObject.SetActive(false);
        }

        // 隱藏繼續按鈕
        if (continueButton != null)
            continueButton.gameObject.SetActive(false);

        // 隱藏所有星星
        if (stars != null)
        {
            foreach (GameObject star in stars)
            {
                if (star != null)
                    star.SetActive(false);
            }
        }
    }

    IEnumerator ShowResultSequence()
    {
        // ✅ 在第一顆星星出現前等待指定延遲
        yield return new WaitForSeconds(initialStarDelay);

        // 1. 逐一顯示星星
        if (stars != null)
        {
            bool isFirstStar = true;

            foreach (GameObject star in stars)
            {
                if (star != null)
                {
                    star.SetActive(true);

                    // 只在第一顆星星出現時播放音效
                    if (isFirstStar && audioController != null)
                    {
                        audioController.PlayStarSpawnSound();
                        isFirstStar = false;
                    }

                    yield return new WaitForSeconds(starSpawnDelay);
                }
            }
        }

        // 2. 星星全部出現後等待
        yield return new WaitForSeconds(delayAfterStars);

        // 3. 顯示訊息文字
        if (messageText != null)
        {
            messageText.gameObject.SetActive(true);
            messageText.text = "太棒了!";
        }

        // 4. 文字出現後等待
        yield return new WaitForSeconds(delayAfterMessage);

        // 5. 顯示繼續按鈕
        if (continueButton != null)
            continueButton.gameObject.SetActive(true);
    }

    public void OnContinueButtonClicked()
    {
        // 防止重複點擊
        if (continueButton != null)
            continueButton.interactable = false;

        // 播放按鈕點擊音效並延遲載入場景
        if (audioController != null && audioController.buttonClickSound != null)
        {
            audioController.PlayButtonClickSound();
            StartCoroutine(LoadNextSceneAfterDelay(audioController.buttonClickSound.length));
        }
        else
        {
            // 若沒設定音效就直接切換
            LoadNextScene();
        }
    }

    private IEnumerator LoadNextSceneAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        LoadNextScene();
    }

    private void LoadNextScene()
    {
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        else
        {
            Debug.LogWarning("ResultController: nextSceneName 未設定!");
        }
    }
}
