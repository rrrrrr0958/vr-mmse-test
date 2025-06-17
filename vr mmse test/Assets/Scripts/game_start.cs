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

    [Header("高亮目標")]
    // public GameObject lampQuestionObject; // 這個欄位在新的邏輯下可以不需要了，因為我們不依賴燈的物件來定位圈圈

    // =========================================================================
    // 私有變數 (腳本內部使用)
    // =========================================================================

    private GameObject[] stallRootObjects;
    private List<string> stallNames = new List<string>();

    private bool hasClickedStall = false;
    private int currentInitialTargetIndex = 0;
    private Coroutine initialQuestionCoroutine;

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
        // 由於不再需要程式碼定位，lampQuestionObject 也不再是必須的錯誤檢查
        // if (lampQuestionObject == null)
        // {
        //     Debug.LogError("Error: lampQuestionObject is not assigned in the Inspector! Please assign it for question 3.");
        // }

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
        initialQuestionCoroutine = StartCoroutine(BroadcastInitialQuestionLoop());
    }

    void Update()
    {
        if (!hasClickedStall && Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            int stallLayerMask = 1 << LayerMask.NameToLayer("StallLayer");

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, stallLayerMask))
            {
                if (hit.collider.CompareTag("StallNameText"))
                {
                    Debug.Log("偵測到攤位點擊，準備轉向魚攤。");
                    hasClickedStall = true;

                    if (initialQuestionCoroutine != null)
                    {
                        StopCoroutine(initialQuestionCoroutine);
                    }

                    HideAllStallNamesAndQuestion();
                    StartCoroutine(MoveCameraToFishStallAndStartFishStallQuestions());
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

    IEnumerator BroadcastInitialQuestionLoop()
    {
        yield return new WaitForSeconds(initialTextDelay + questionBroadcastDelay);

        while (!hasClickedStall)
        {
            if (stallNames.Count == 0)
            {
                Debug.LogWarning("沒有攤位名稱可供廣播初始問題。");
                yield break;
            }

            string targetStallName = stallNames[currentInitialTargetIndex];
            string initialQuestion = $"請點選 {targetStallName} 攤位！";

            Debug.Log($"Console 問題 (初始階段): {initialQuestion}");

            yield return new WaitForSeconds(timeBetweenQuestions);

            currentInitialTargetIndex = (currentInitialTargetIndex + 1) % stallNames.Count;
        }
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
        yield return new WaitForSeconds(timeBetweenQuestions);
        Debug.Log("Console 問題: 這個攤位在賣什麼？");
        yield return new WaitForSeconds(timeBetweenQuestions);

        Debug.Log("Console 問題: 魚的顏色是什麼？");
        yield return new WaitForSeconds(timeBetweenQuestions);

        Debug.Log("Console 問題: 那個是什麼？");
        ShowHighlightCircle(); // 不再需要傳入 lampQuestionObject
        yield return new WaitForSeconds(timeBetweenQuestions);
        HideHighlightCircle();

        Debug.Log("Console: 所有魚攤問題已完成！");
    }

    // 顯示高亮圈圈 (現在只負責啟用/禁用)
    void ShowHighlightCircle() // 不再需要 GameObject targetObject 參數
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

    // 隱藏高亮圈圈
    void HideHighlightCircle()
    {
        if (highlightCircleImage != null)
        {
            highlightCircleImage.gameObject.SetActive(false);
            Debug.Log("HighlightCircle 已禁用。");
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