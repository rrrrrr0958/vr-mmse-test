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

    [Header("Animation Settings")]
    public float initialTextDelay = 0.5f;
    [Tooltip("文字出現後到按鈕出現的間隔時間")]
    public float delayAfterMessage = 0.5f;


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
    }

    IEnumerator ShowResultSequence()
    {
        // ✅ 在第一顆星星出現前等待指定延遲
        yield return new WaitForSeconds(initialTextDelay);

        // 3. 顯示訊息文字
        if (messageText != null)
        {
            messageText.gameObject.SetActive(true);
            messageText.text = "按下粉紅按鈕\n繼續挑戰下一關";
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
            //StartCoroutine(LoadNextSceneAfterDelay(audioController.buttonClickSound.length));

            
        }
        SceneFlowManager.instance.LoadNextScene();
    }
}
