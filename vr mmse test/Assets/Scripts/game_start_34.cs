using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    // =========================================================================
    // 公開變數 (在 Unity Inspector 中設定)
    // =========================================================================

    [Header("遊戲開始設定")]
    public float initialTextDelay = 3f;      // 遊戲開始後，攤位文字顯示前的延遲時間
    public float questionBroadcastDelay = 2f; // 攤位文字顯示後，問題廣播前的延遲時間
    public float timeBetweenQuestions = 2f;  // 問題之間的延遲時間 (用於等待語音回答)
    public float voiceQuestionBufferTime = 0.5f; // 語音播放完成後的額外緩衝時間

    // 【新增】點擊題目的等待時間
    [Header("點擊題設定")]
    public float clickResponseDuration = 3.0f; // 每個點擊題目等待使用者點擊的秒數 (此時間段內會監測點擊)


    [Header("攝影機目標點")]
    public Transform cameraTarget_FishStall; // 魚攤攝影機目標

    [Header("攝影機移動設定")]
    public float cameraMoveSpeed = 50.0f;    // 攝影機移動的速度，數值越大越快

    [Header("UI 連結")]
    public TMPro.TextMeshPro questionBroadcastTextMeshPro; // 用於顯示 "請點選 XX 攤位" 的 TextMeshPro 組件 (可選，如果不在螢幕顯示則不需)
    public Image highlightCircleImage; // 新增：用於高亮顯示的圈圈 UI Image

    // 語音問題相關變數
    [Header("語音問題設定")]
    public AudioSource voiceAudioSource; // 用於播放語音的 AudioSource，綁定 Main Camera 上的
    public AudioClip fishStallAudioClip;    // "魚攤" 的語音檔案
    public AudioClip fruitStallAudioClip;   // "水果攤" 的語音檔案
    public AudioClip weaponStallAudioClip;  // "武器攤" 的語音檔案
    public AudioClip breadStallAudioClip;   // "麵包攤" 的語音檔案
    public AudioClip meatStallAudioClip;    // "肉攤" 的語音檔案

    // 魚攤專屬問題語音檔案
    [Header("魚攤問題語音設定")]
    public AudioClip whatIsSellingAudioClip; // "這個攤位在賣什麼？" 的語音檔案
    public AudioClip fishColorAudioClip;     // "魚的顏色是什麼？" 的語音檔案
    public AudioClip whatIsThatAudioClip;    // "那個是什麼？" 的語音檔案


    // =========================================================================
    // 私有變數 (腳本內部使用)
    // =========================================================================

    private GameObject[] stallRootObjects;
    private List<string> stallNames = new List<string>();
    private List<string> nonFishStallNames = new List<string>(); // 不包含魚攤的名稱列表

    private bool hasClickedStall = false; // 此變數將用於控制是否已進入魚攤流程
    private Coroutine initialQuestionCoroutine;

    private string currentTargetStallName = ""; // 儲存當前被要求的目標攤位名稱

    // 【新增】追蹤正確點擊的次數
    private int correctAnswersCount = 0;

    // 【新增】標誌，表示當前是否處於等待玩家點擊的階段（針對初期三題）
    private bool isWaitingForClickInput = false;

    // =========================================================================
    // Unity 生命周期方法
    // =========================================================================

    void Awake()
    {
        stallRootObjects = GameObject.FindGameObjectsWithTag("StallNameText");
        Debug.Log($"Awake: Found {stallRootObjects.Length} stall clickable root objects by tag.");

        foreach (GameObject stallRoot in stallRootObjects)
        {
            stallRoot.SetActive(false);
            TMPro.TextMeshPro textMeshPro = stallRoot.GetComponentInChildren<TMPro.TextMeshPro>();
            if (textMeshPro != null)
            {
                string stallName = textMeshPro.text;
                stallNames.Add(stallName); // 加入所有攤位名稱

                // 將非魚攤的名稱加入到另一個列表
                if (stallName != "魚攤") // 確保 "魚攤" 這個字串和你的 TextMeshPro 完全一致
                {
                    nonFishStallNames.Add(stallName);
                }
            }
            else
            {
                Debug.LogWarning($"Awake: Stall root '{stallRoot.name}' has 'StallNameText' tag but no TextMeshPro component found in children. This stall name will not be used for initial question.");
            }
        }
        Debug.Log($"Awake: Total stall names collected for initial question: {stallNames.Count}");
        Debug.Log($"Awake: Non-fish stall names collected: {nonFishStallNames.Count}");


        if (cameraTarget_FishStall == null)
        {
            Debug.LogError("Error: cameraTarget_FishStall is not assigned in the Inspector! Please assign it.");
        }
        if (questionBroadcastTextMeshPro == null)
        {
            Debug.LogWarning("Warning: questionBroadcastTextMeshPro is not assigned in the Inspector. Initial question will only appear in Console.");
        }
        if (highlightCircleImage == null)
        {
            Debug.LogError("Error: highlightCircleImage is not assigned in the Inspector! Please assign it for question 3.");
        }

        // 檢查語音相關變數是否設定
        if (voiceAudioSource == null)
        {
            Debug.LogError("Error: voiceAudioSource is not assigned in the Inspector! Please assign the AudioSource from Main Camera.");
        }
        if (fishStallAudioClip == null || fruitStallAudioClip == null || weaponStallAudioClip == null ||
            breadStallAudioClip == null || meatStallAudioClip == null ||
            whatIsSellingAudioClip == null || fishColorAudioClip == null || whatIsThatAudioClip == null)
        {
            Debug.LogWarning("Warning: Some AudioClips are not assigned. Voice questions may not play.");
        }

        // 初始禁用圈圈
        if (highlightCircleImage != null)
        {
            highlightCircleImage.gameObject.SetActive(false);
        }
    }

    void Start()
    {
        Debug.Log("GameManager Start() called.");

        if (questionBroadcastTextMeshPro != null)
        {
            questionBroadcastTextMeshPro.gameObject.SetActive(false);
        }

        StartCoroutine(ShowTextsAfterDelay());
        initialQuestionCoroutine = StartCoroutine(MainClickSequence());
    }

    void Update()
    {
        // 【修改】只在等待玩家點擊輸入的階段，並且當 currentTargetStallName 有值時才監測點擊
        // 且只檢查滑鼠左鍵按下一次 (GetMouseButtonDown(0))
        if (isWaitingForClickInput && !hasClickedStall && !string.IsNullOrEmpty(currentTargetStallName) && Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            int stallLayerMask = 1 << LayerMask.NameToLayer("StallLayer");

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, stallLayerMask))
            {
                if (hit.collider.CompareTag("StallNameText"))
                {
                    TMPro.TextMeshPro clickedTextMeshPro = hit.collider.GetComponentInChildren<TMPro.TextMeshPro>();
                    if (clickedTextMeshPro != null && clickedTextMeshPro.text == currentTargetStallName)
                    {
                        Debug.Log($"正確偵測到點擊目標攤位: {currentTargetStallName}。此題正確！");
                        correctAnswersCount++; // 點擊正確，增加分數
                    }
                    else
                    {
                        Debug.LogWarning($"點擊了錯誤的攤位: {clickedTextMeshPro?.text ?? "未知攤位"}。正確答案是 {currentTargetStallName}。此題錯誤！");
                    }
                    // 無論點擊正確與否，只要有一次點擊被處理，就將 currentTargetStallName 清空。
                    // 這樣可以確保在一個 `clickResponseDuration` 期間只處理一次有效點擊，
                    // 避免重複計分或重複日誌。
                    currentTargetStallName = "";
                }
            }
        }
    }

    // =========================================================================
    // 遊戲流程控制方法
    // =========================================================================

    void HideAllStallNamesAndQuestion()
    {
        foreach (GameObject stallRoot in stallRootObjects)
        {
            stallRoot.SetActive(false);
        }
        if (questionBroadcastTextMeshPro != null)
        {
            questionBroadcastTextMeshPro.gameObject.SetActive(false);
        }
    }

    IEnumerator ShowTextsAfterDelay()
    {
        yield return new WaitForSeconds(initialTextDelay);
        foreach (GameObject stallRoot in stallRootObjects)
        {
            stallRoot.SetActive(true);
        }
        Debug.Log("所有攤位名稱已顯示。");
    }

    // 【主要修改】
    IEnumerator MainClickSequence()
    {
        yield return new WaitForSeconds(initialTextDelay + questionBroadcastDelay); // 等待攤位名稱顯示

        List<string> tempNonFishStallNames = new List<string>(nonFishStallNames);

        correctAnswersCount = 0; // 在開始新一輪題目時重置計數

        // 第一、二次隨機選擇（不包含魚攤，且不重複）
        for (int i = 0; i < 2; i++)
        {
            if (tempNonFishStallNames.Count == 0) // 如果沒有足夠的非魚攤選項了
            {
                Debug.LogWarning("沒有足夠的非魚攤名稱可供隨機選擇！請確保至少有兩個非魚攤。");
                yield break; // 無法繼續，終止協程
            }

            int randomIndex = Random.Range(0, tempNonFishStallNames.Count);
            currentTargetStallName = tempNonFishStallNames[randomIndex]; // 設定目標攤位，Update 會監聽這個目標

            string initialQuestion = $"請點選 {currentTargetStallName} 攤位！";
            Debug.Log($"Console 問題 (第 {i + 1} 次): {initialQuestion}");
            PlayInitialVoiceQuestion(currentTargetStallName); // 播放語音

            tempNonFishStallNames.RemoveAt(randomIndex); // 從臨時列表中移除已被選中的攤位

            // 【新增】設置旗標，通知 Update 可以開始監測點擊
            isWaitingForClickInput = true;

            // 計算總等待時間：語音長度 + 語音播放後的緩衝 + 玩家點擊反應時間
            AudioClip currentClip = GetAudioClipForStall(currentTargetStallName);
            float totalWaitTime = (currentClip != null ? currentClip.length : 0f) + voiceQuestionBufferTime + clickResponseDuration;

            yield return new WaitForSeconds(totalWaitTime); // 等待指定時間，無論是否點擊

            // 【重置】清空 currentTargetStallName 並關閉監聽旗標，表示該題目的點擊響應時間已過
            currentTargetStallName = "";
            isWaitingForClickInput = false;

            // 確保題與題之間有間隔，這可以在 totalWaitTime 中涵蓋，也可以額外加一個小延遲
            yield return new WaitForSeconds(timeBetweenQuestions);
        }

        // 第三次固定點擊魚攤
        currentTargetStallName = "魚攤"; // 固定目標為魚攤，Update 會監聽這個目標
        string finalQuestion = $"請點選 {currentTargetStallName} 攤位！";
        Debug.Log($"Console 問題 (第 3 次，固定魚攤): {finalQuestion}");
        PlayInitialVoiceQuestion(currentTargetStallName); // 播放魚攤語音

        // 【新增】設置旗標，通知 Update 可以開始監測點擊
        isWaitingForClickInput = true;

        // 計算總等待時間
        AudioClip fishStallClip = GetAudioClipForStall("魚攤");
        float fishStallTotalWaitTime = (fishStallClip != null ? fishStallClip.length : 0f) + voiceQuestionBufferTime + clickResponseDuration;
        yield return new WaitForSeconds(fishStallTotalWaitTime); // 等待指定時間

        // 【重置】清空 currentTargetStallName 並關閉監聽旗標
        currentTargetStallName = "";
        isWaitingForClickInput = false;

        // 【新增】記錄總分到 Console
        Debug.Log($"正確題目數: {correctAnswersCount}/3");

        Debug.Log("所有點擊任務完成，準備進入魚攤流程。");
        hasClickedStall = true;
        HideAllStallNamesAndQuestion();
        StartCoroutine(MoveCameraToFishStallAndStartFishStallQuestions());
    }

    IEnumerator MoveCameraToFishStallAndStartFishStallQuestions()
    {
        Debug.Log("準備將攝影機轉向魚攤...");

        if (cameraTarget_FishStall == null)
        {
            Debug.LogError("cameraTarget_FishStall is not assigned! Cannot move camera.");
            yield break;
        }

        yield return StartCoroutine(SmoothCameraMove(cameraTarget_FishStall.position, cameraTarget_FishStall.rotation));

        Debug.Log("攝影機已成功轉向魚攤。");
        StartCoroutine(FishStallQuestionSequence());
    }

    IEnumerator FishStallQuestionSequence()
    {
        // 等待進入魚攤的緩衝時間 (如果你希望在魚攤問題開始前有一段靜默)
        yield return new WaitForSeconds(timeBetweenQuestions); // 這行可以保留，作為問題開始前的緩衝

        // 第一個問題
        Debug.Log("Console 問題: 這個攤位在賣什麼？");
        PlayVoiceClip(whatIsSellingAudioClip, "這個攤位在賣什麼？");
        yield return StartCoroutine(WaitForVoiceToFinish(whatIsSellingAudioClip)); // 等待語音播放完畢

        // 第二個問題
        Debug.Log("Console 問題: 魚的顏色是什麼？");
        PlayVoiceClip(fishColorAudioClip, "魚的顏色是什麼？");
        yield return StartCoroutine(WaitForVoiceToFinish(fishColorAudioClip)); // 等待語音播放完畢

        // 第三個問題 (帶圈圈)
        Debug.Log("Console 問題: 那個是什麼？");
        PlayVoiceClip(whatIsThatAudioClip, "那個是什麼？");
        yield return StartCoroutine(WaitForVoiceToFinish(whatIsThatAudioClip)); // 等待語音播放完畢

        ShowHighlightCircle();
        yield return new WaitForSeconds(timeBetweenQuestions); // 這裡可以根據需要調整等待時間，因為語音已播完
        HideHighlightCircle();

        Debug.Log("Console: 所有魚攤問題已完成！");
    }

    void ShowHighlightCircle()
    {
        if (highlightCircleImage != null)
        {
            highlightCircleImage.gameObject.SetActive(true);
            Debug.Log("HighlightCircle 已啟用並顯示。其位置和大小由 Editor 設定。");
        }
        else
        {
            Debug.LogError("HighlightCircleImage 未賦值，無法顯示圈圈！");
        }
    }

    void HideHighlightCircle()
    {
        if (highlightCircleImage != null)
        {
            highlightCircleImage.gameObject.SetActive(false);
            Debug.Log("HighlightCircle 已禁用。");
        }
    }

    // 通用的語音播放方法，用於初始問題
    void PlayInitialVoiceQuestion(string stallName)
    {
        Debug.Log($"嘗試播放語音給攤位: '{stallName}' (長度: {stallName.Length})");

        AudioClip clipToPlay = GetAudioClipForStall(stallName); // 使用輔助方法獲取 AudioClip

        PlayVoiceClip(clipToPlay, stallName); // 使用通用的播放方法
    }

    // 【新增】輔助方法：根據攤位名稱獲取對應的 AudioClip
    private AudioClip GetAudioClipForStall(string stallName)
    {
        switch (stallName)
        {
            case "魚攤": return fishStallAudioClip;
            case "蔬果": return fruitStallAudioClip; // 確保這裡的字串與你的實際攤位名稱和 AudioClip 匹配
            case "武器": return weaponStallAudioClip;
            case "麵包": return breadStallAudioClip;
            case "肉攤": return meatStallAudioClip;
            default: return null;
        }
    }

    // 通用的語音播放私有方法
    private void PlayVoiceClip(AudioClip clip, string debugMessageContext)
    {
        if (voiceAudioSource != null)
        {
            if (clip != null)
            {
                voiceAudioSource.PlayOneShot(clip);
                Debug.Log($"Playing voice clip: '{debugMessageContext}'");
            }
            else
            {
                Debug.LogWarning($"無法播放語音給 '{debugMessageContext}'。原因: AudioClip 未設定。");
            }
        }
        else
        {
            Debug.LogWarning($"無法播放語音給 '{debugMessageContext}'。原因: AudioSource 未設定。");
        }
    }

    // 等待語音播放完成的協程 (用於魚攤問題部分)
    private IEnumerator WaitForVoiceToFinish(AudioClip clip)
    {
        if (voiceAudioSource == null)
        {
            Debug.LogWarning("AudioSource 未設定，無法等待語音播放完成。");
            yield break;
        }

        if (clip == null)
        {
            Debug.LogWarning("要等待的 AudioClip 為空。");
            yield break;
        }

        yield return new WaitForSeconds(clip.length + voiceQuestionBufferTime);
    }


    IEnumerator SmoothCameraMove(Vector3 targetPosition, Quaternion targetRotation)
    {
        Transform mainCameraTransform = Camera.main.transform;
        Vector3 startPosition = mainCameraTransform.position;
        Quaternion startRotation = mainCameraTransform.rotation;

        float elapsedTime = 0;
        float duration = Vector3.Distance(startPosition, targetPosition) / cameraMoveSpeed;

        if (duration < 0.05f) duration = 0.05f;

        while (elapsedTime < duration)
        {
            mainCameraTransform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / duration);
            mainCameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        mainCameraTransform.position = targetPosition;
        mainCameraTransform.rotation = targetRotation;

        Debug.Log("攝影機平滑移動完成。");
    }
}
