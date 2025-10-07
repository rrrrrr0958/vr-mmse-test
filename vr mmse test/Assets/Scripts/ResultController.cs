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

    [Header("Star Settings")]
    public GameObject[] stars;

    [Header("Animation Settings")]
    public float starSpawnDelay = 0.22f; // 每顆星星之間的間隔
    public float delayAfterStars = 0.3f; // 星星全部出現後到文字出現的間隔
    public float delayAfterMessage = 0.5f; // 文字出現後到按鈕出現的間隔

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
        // 1. 逐一顯示星星
        if (stars != null)
        {
            foreach (GameObject star in stars)
            {
                if (star != null)
                {
                    star.SetActive(true);
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