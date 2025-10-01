using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;

public class ResultController : MonoBehaviour
{
    [Header("UI Elements")]
    public Button submitButton;
    public GameObject blackPanel;
    public TMP_Text messageText;
    public Button continueButton;

    [Header("Star Settings")]
    public GameObject[] stars; // 直接引用 Hierarchy 中的星星

    [Header("Animation Settings")]
    public float blackPanelFadeDuration = 0.25f;
    [Range(0f, 1f)] public float blackPanelAlpha = 0.6f;
    public float starSpawnDelay = 0.22f;
    public float delayBeforeMessage = 0.12f;

    [Header("Scene Settings")]
    public string nextSceneName = "NextScene";

    private CanvasGroup blackPanelCanvasGroup;

    void Start()
    {
        // 初始化 BlackPanel 的 CanvasGroup
        if (blackPanel != null)
        {
            blackPanelCanvasGroup = blackPanel.GetComponent<CanvasGroup>();
            if (blackPanelCanvasGroup == null)
                blackPanelCanvasGroup = blackPanel.AddComponent<CanvasGroup>();
            
            blackPanelCanvasGroup.alpha = 0f;
            blackPanel.SetActive(false);
        }

        // 初始化 UI 狀態
        if (messageText != null)
        {
            messageText.text = "";
            messageText.gameObject.SetActive(false);
        }

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

    public void OnSubmitButtonClicked()
    {
        if (submitButton != null)
            submitButton.gameObject.SetActive(false);

        StopAllCoroutines();
        StartCoroutine(ShowResultSequence());
    }

    IEnumerator ShowResultSequence()
    {
        // 1. 顯示並淡入 BlackPanel
        if (blackPanel != null)
        {
            blackPanel.SetActive(true);
            yield return StartCoroutine(FadeCanvasGroup(blackPanelCanvasGroup, 0f, blackPanelAlpha, blackPanelFadeDuration));
        }

        // 2. 逐一顯示星星
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

        // 3. 等待一小段時間後顯示訊息
        yield return new WaitForSeconds(delayBeforeMessage);

        if (messageText != null)
        {
            messageText.gameObject.SetActive(true);
            messageText.text = "太棒了!";
        }

        // 4. 顯示繼續按鈕
        if (continueButton != null)
            continueButton.gameObject.SetActive(true);
    }

    IEnumerator FadeCanvasGroup(CanvasGroup cg, float from, float to, float duration)
    {
        if (cg == null) yield break;

        float elapsed = 0f;
        cg.alpha = from;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Lerp(from, to, elapsed / duration);
            yield return null;
        }

        cg.alpha = to;
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