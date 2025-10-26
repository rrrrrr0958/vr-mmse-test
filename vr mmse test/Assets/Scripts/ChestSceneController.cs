using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using UnityEngine.SceneManagement;
using Firebase.Firestore;

public class ChestSceneController : MonoBehaviour
{
    private FirebaseManager_Firestore FirebaseManager;
    
    [Header("UI Elements")]
    public TMP_Text rewardText;              // 顯示獎勵的文字
    public Button continueButton;            // 繼續按鈕
    [SerializeField] TMP_Text score; 
    [SerializeField] TMP_Text hint; 

    [Header("Audio Settings")]
    public AudioSource audioSource;          // 播放音效的 AudioSource
    public AudioClip chestOpenSound;         // 開寶箱音效
    public AudioClip confirmSound;           // 按下按鈕時的音效
    [Range(0f, 1f)] public float chestOpenVolume = 1f;
    [Range(0f, 1f)] public float confirmVolume = 1f;

    [Header("Timing Settings")]
    public float textDelay = 1.5f;           // 文字出現延遲
    public float buttonDelay = 3f;           // 按鈕出現延遲
    public float fadeDuration = 1f;          // 淡入時間

    

    private void Awake()
    {
        // ✅ 確保音效在場景剛載入時就預先初始化
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        // ✅ 預先載入音效到記憶體 (避免播放時延遲)
        if (chestOpenSound != null)
            chestOpenSound.LoadAudioData();

        if (confirmSound != null)
            confirmSound.LoadAudioData();
    }

    void Start()
    {
        InitializeUI();

        // ✅ 不延遲播放音效 — 立即播放開場寶箱音效
        if (chestOpenSound != null && audioSource != null)
            audioSource.PlayOneShot(chestOpenSound, chestOpenVolume);

        score.text = FirebaseManager_Firestore.Instance.totalScore.ToString();

        if (score.text != null)
        {
            score.text = score.text + " / 30";
            if (FirebaseManager_Firestore.Instance.totalScore >= 24)
            {
                hint.text = "太厲害了！";
            }
            else if (FirebaseManager_Firestore.Instance.totalScore >= 16)
            {
                hint.text = "還不錯喔～";
            }
            else
            {
                hint.text = "再試一次吧！";
            }
        }
        else
        {
            hint.text = "遊戲失敗，沒有分數";
        }
        
        // ✅ 開始文字與按鈕的協程
        StartCoroutine(PlayChestSequence());
    }

    void InitializeUI()
    {
        if (rewardText != null)
        {
            rewardText.alpha = 0f;
            hint.alpha = 0f;
            rewardText.gameObject.SetActive(false);
            hint.gameObject.SetActive(false);
        }

        if (continueButton != null)
        {
            CanvasGroup btnGroup = continueButton.GetComponent<CanvasGroup>();
            if (btnGroup == null)
                btnGroup = continueButton.gameObject.AddComponent<CanvasGroup>();

            btnGroup.alpha = 0f;
            continueButton.gameObject.SetActive(false);
            // continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(OnContinueButtonClicked);
        }

  
    }

    IEnumerator PlayChestSequence()
    {
        // ✅ 不再 yield null；直接進入後續流程

        // 延遲顯示文字
        yield return new WaitForSeconds(textDelay);
        if (rewardText != null)
        {
            rewardText.gameObject.SetActive(true);
            hint.gameObject.SetActive(true);
            yield return StartCoroutine(FadeTextIn(rewardText, fadeDuration));
            yield return StartCoroutine(FadeTextIn(hint, fadeDuration));
        }

        // 延遲顯示按鈕
        yield return new WaitForSeconds(buttonDelay);
        if (continueButton != null)
        {
            continueButton.gameObject.SetActive(true);
            yield return StartCoroutine(FadeButtonIn(continueButton, fadeDuration));
        }
    }

    IEnumerator FadeTextIn(TMP_Text text, float duration)
    {
        float elapsed = 0f;
        text.alpha = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            text.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        text.alpha = 1f;
    }

    IEnumerator FadeButtonIn(Button button, float duration)
    {
        CanvasGroup cg = button.GetComponent<CanvasGroup>();
        float elapsed = 0f;
        cg.alpha = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            cg.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }
        cg.alpha = 1f;
    }

    private void OnRecentTestsChecked(bool success, List<DocumentSnapshot> docs)
    {
        if (!success || docs == null || docs.Count < 100)
        {
            Debug.Log("❌ 沒有歷史紀錄，直接關閉遊戲");
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false; // 編輯器中結束播放
#else
        Application.Quit(); // build 後關閉遊戲
#endif
            return;
        }
        Debug.Log("✅ 有歷史紀錄，切換場景");
    }

    void OnContinueButtonClicked()
    {
        FirebaseManager_Firestore.Instance.LoadRecentTests(1, OnRecentTestsChecked);
        StartCoroutine(HandleContinueButton());
        SceneFlowManager.instance.LoadNextScene();
    }

    IEnumerator HandleContinueButton()
    {
        if (confirmSound != null && audioSource != null)
            audioSource.PlayOneShot(confirmSound, confirmVolume);

        yield return new WaitForSeconds(0.5f);


    }
}
