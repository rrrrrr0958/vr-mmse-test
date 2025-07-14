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

    [Header("攝影機目標點")]
    public Transform cameraTarget_FishStall; // 魚攤攝影機目標

    [Header("攝影機移動設定")]
    public float cameraMoveSpeed = 50.0f;    // 攝影機移動的速度，數值越大越快

    [Header("UI 連結")]
    public TMPro.TextMeshPro questionBroadcastTextMeshPro; // 用於顯示 "請點選 XX 攤位" 的 TextMeshPro 組件 (可選，如果不在螢幕顯示則不需)
    public Image highlightCircleImage; // 新增：用於高亮顯示的圈圈 UI Image

    // 【新增】語音問題相關變數
    [Header("語音問題設定")]
    public AudioSource voiceAudioSource; // 用於播放語音的 AudioSource，綁定 Main Camera 上的
    public AudioClip fishStallAudioClip;    // "魚攤" 的語音檔案
    public AudioClip fruitStallAudioClip;   // "水果攤" 的語音檔案
    public AudioClip weaponStallAudioClip;  // "武器攤" 的語音檔案
    public AudioClip breadStallAudioClip;   // "麵包攤" 的語音檔案
    public AudioClip meatStallAudioClip;    // "肉攤" 的語音檔案

    // =========================================================================
    // 私有變數 (腳本內部使用)
    // =========================================================================

    private GameObject[] stallRootObjects;
    private List<string> stallNames = new List<string>();

    private bool hasClickedStall = false;
    private Coroutine initialQuestionCoroutine;

    private string currentTargetStallName = ""; // 儲存當前隨機選擇的目標攤位名稱

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
                stallNames.Add(textMeshPro.text);
            }
            else
            {
                Debug.LogWarning($"Awake: Stall root '{stallRoot.name}' has 'StallNameText' tag but no TextMeshPro component found in children. This stall name will not be used for initial question.");
            }
        }
        Debug.Log($"Awake: Total stall names collected for initial question: {stallNames.Count}");

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

        // 【新增】檢查語音相關變數是否設定
        if (voiceAudioSource == null)
        {
            Debug.LogError("Error: voiceAudioSource is not assigned in the Inspector! Please assign the AudioSource from Main Camera.");
        }
        // 對每個 AudioClip 進行檢查 (雖然在 PlayVoiceQuestionByIdx 裡會再次檢查)
        if (fishStallAudioClip == null || fruitStallAudioClip == null || weaponStallAudioClip == null ||
            breadStallAudioClip == null || meatStallAudioClip == null)
        {
            Debug.LogWarning("Warning: Some initial question AudioClips are not assigned. Voice questions may not play.");
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
        initialQuestionCoroutine = StartCoroutine(BroadcastInitialRandomQuestion());
    }

    void Update()
    {
        if (!hasClickedStall && !string.IsNullOrEmpty(currentTargetStallName) && Input.GetMouseButtonDown(0))
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
                        Debug.Log($"正確偵測到點擊目標攤位: {currentTargetStallName}，準備轉向魚攤。");
                        hasClickedStall = true;

                        if (initialQuestionCoroutine != null)
                        {
                            StopCoroutine(initialQuestionCoroutine);
                        }

                        HideAllStallNamesAndQuestion();
                        StartCoroutine(MoveCameraToFishStallAndStartFishStallQuestions());
                    }
                    else
                    {
                        Debug.LogWarning($"點擊了錯誤的攤位: {clickedTextMeshPro?.text ?? "未知攤位"}。請點擊 {currentTargetStallName} 攤位！");
                        // 如果需要，可以在這裡播放一個錯誤提示音
                    }
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

    IEnumerator BroadcastInitialRandomQuestion()
    {
        yield return new WaitForSeconds(initialTextDelay + questionBroadcastDelay);

        if (stallNames.Count == 0)
        {
            Debug.LogWarning("沒有攤位名稱可供廣播初始問題。");
            yield break;
        }

        int randomIndex = Random.Range(0, stallNames.Count);
        currentTargetStallName = stallNames[randomIndex];

        string initialQuestion = $"請點選 {currentTargetStallName} 攤位！";

        Debug.Log($"Console 問題 (初始階段): {initialQuestion}");

        // 【新增】播放對應的語音檔案
        PlayInitialVoiceQuestion(currentTargetStallName);
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

    // 這個協程目前沒有語音，如果之後需要為魚攤問題也添加語音，會在這裡修改
    IEnumerator FishStallQuestionSequence()
    {
        yield return new WaitForSeconds(timeBetweenQuestions);
        Debug.Log("Console 問題: 這個攤位在賣什麼？");
        yield return new WaitForSeconds(timeBetweenQuestions);

        Debug.Log("Console 問題: 魚的顏色是什麼？");
        yield return new WaitForSeconds(timeBetweenQuestions);

        Debug.Log("Console 問題: 那個是什麼？");
        ShowHighlightCircle();
        yield return new WaitForSeconds(timeBetweenQuestions);
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

    // 【新增】根據攤位名稱播放語音的方法
    void PlayInitialVoiceQuestion(string stallName)
    {
        // 這行會告訴你實際傳入的 stallName 是什麼
        Debug.Log($"嘗試播放語音給攤位: '{stallName}' (長度: {stallName.Length})");

        AudioClip clipToPlay = null;
        switch (stallName)
        {
            case "魚攤":
                clipToPlay = fishStallAudioClip;
                break;
            case "蔬果":
                clipToPlay = fruitStallAudioClip;
                break;
            case "武器":
                clipToPlay = weaponStallAudioClip;
                break;
            case "麵包":
                clipToPlay = breadStallAudioClip;
                break;
            case "肉攤":
                clipToPlay = meatStallAudioClip;
                break;
            default:
                Debug.LogWarning($"No audio clip found for stall: {stallName}");
                break;
        }

        if (voiceAudioSource != null && clipToPlay != null)
        {
            voiceAudioSource.PlayOneShot(clipToPlay); // 使用 PlayOneShot 不會打斷其他正在播放的聲音 (如果有的話)
            Debug.Log($"Playing voice question for: {stallName}");
        }
        else
        {
            Debug.LogWarning($"無法播放語音給攤位: '{stallName}'。原因: AudioSource 或 AudioClip 未設定。");
        }
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