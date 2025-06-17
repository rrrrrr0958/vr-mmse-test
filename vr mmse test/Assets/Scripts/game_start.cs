using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    // =========================================================================
    // 公開變數 (在 Unity Inspector 中設定)
    // =========================================================================

    [Header("遊戲開始設定")]
    public float initialTextDelay = 3f;      // 遊戲開始後，攤位文字顯示前的延遲時間
    public float questionBroadcastDelay = 2f; // 攤位文字顯示後，問題廣播前的延遲時間

    [Header("攝影機目標點")]
    // 你只需要設定魚攤的目標點，因為攝影機只會轉到這裡
    public Transform cameraTarget_FishStall;

    [Header("攝影機移動設定")]
    public float cameraMoveSpeed = 500.0f;    // 攝影機移動的速度，數值越大越快

    [Header("UI 連結")]
    public TMPro.TextMeshPro questionBroadcastTextMeshPro; // 用於顯示問題的 TextMeshPro 組件

    // =========================================================================
    // 私有變數 (腳本內部使用)
    // =========================================================================

    private GameObject[] stallRootObjects; // 用於儲存所有可點擊根物件的列表
    private List<string> stallNames = new List<string>(); // 儲存所有攤位的文字名稱
    private string currentQuestionStallName; // 當前廣播的問題攤位名稱

    private bool hasClickedStall = false; // 新增：追蹤是否已經點擊過攤位

    // =========================================================================
    // Unity 生命周期方法
    // =========================================================================

    void Awake()
    {
        // 找到所有帶有 "StallNameText" 標籤的根物件
        stallRootObjects = GameObject.FindGameObjectsWithTag("StallNameText");
        Debug.Log($"Awake: Found {stallRootObjects.Length} stall clickable root objects by tag.");

        // 收集所有攤位的名稱並禁用它們
        foreach (GameObject stallRoot in stallRootObjects)
        {
            stallRoot.SetActive(false); // 初始禁用物件
            TMPro.TextMeshPro textMeshPro = stallRoot.GetComponentInChildren<TMPro.TextMeshPro>();
            if (textMeshPro != null)
            {
                stallNames.Add(textMeshPro.text);
            }
            else
            {
                Debug.LogWarning($"Awake: Stall root '{stallRoot.name}' has 'StallNameText' tag but no TextMeshPro component found in children.");
            }
        }
        Debug.Log($"Awake: Total stall names collected: {stallNames.Count}");

        // 檢查魚攤目標點是否設定
        if (cameraTarget_FishStall == null)
        {
            Debug.LogError("Error: cameraTarget_FishStall is not assigned in the Inspector! Please assign it.");
        }
    }

    void Start()
    {
        Debug.Log("GameManager Start() called.");

        // 確保 questionBroadcastTextMeshPro 引用已設置並初始禁用
        if (questionBroadcastTextMeshPro != null)
        {
            questionBroadcastTextMeshPro.gameObject.SetActive(false);
        }
        else
        {
            Debug.LogError("Error: questionBroadcastTextMeshPro is not assigned in the Inspector!");
        }

        // 遊戲開始時，顯示所有攤位文字，然後廣播問題
        StartCoroutine(ShowTextsAfterDelay());
        StartCoroutine(BroadcastQuestionAfterDelay());
    }

    void Update()
    {
        // 只在尚未點擊過攤位時才處理點擊
        if (!hasClickedStall && Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            int stallLayerMask = 1 << LayerMask.NameToLayer("StallLayer");

            if (Physics.Raycast(ray, out hit, Mathf.Infinity, stallLayerMask))
            {
                Debug.Log($"Raycast hit object: {hit.collider.name} (Tag: {hit.collider.tag})");

                if (hit.collider.CompareTag("StallNameText"))
                {
                    TMPro.TextMeshPro clickedTextMeshPro3D = hit.collider.gameObject.GetComponentInChildren<TMPro.TextMeshPro>();
                    string clickedStallName = null;

                    if (clickedTextMeshPro3D != null)
                    {
                        clickedStallName = clickedTextMeshPro3D.text;
                        Debug.Log($"Clicked stall: {clickedStallName}");
                    }
                    else
                    {
                        Debug.LogWarning($"點擊的物件 '{hit.collider.name}' 有 'StallNameText' 標籤，但沒有在其自身或子物件中找到 TextMeshPro 組件。");
                    }

                    // 無論點擊的是哪個攤位，都觸發流程
                    Debug.Log("攤位已點擊，開始轉向魚攤。");
                    hasClickedStall = true; // 標記為已點擊，避免重複觸發
                    StartCoroutine(MoveCameraToFishStallAndEndRound());
                }
                else
                {
                    Debug.LogWarning($"Hit object '{hit.collider.name}' does NOT have 'StallNameText' tag. **這通常表示你的 Raycast 命中了 StallLayer 中不該被判定的物件。**");
                }
            }
            else
            {
                Debug.Log("Raycast hit nothing on StallLayer.");
            }
        }
    }

    // =========================================================================
    // 遊戲流程控制方法
    // =========================================================================

    // 隱藏所有攤位名稱和廣播文字
    void HideAllStallNamesAndQuestion()
    {
        Debug.Log("HideAllStallNamesAndQuestion is called. All texts will disappear.");
        if (stallRootObjects == null || stallRootObjects.Length == 0)
        {
            stallRootObjects = GameObject.FindGameObjectsWithTag("StallNameText");
        }

        foreach (GameObject stallRoot in stallRootObjects)
        {
            stallRoot.SetActive(false); // 禁用整個根物件，其文字也會隨之消失
        }
        if (questionBroadcastTextMeshPro != null)
        {
            questionBroadcastTextMeshPro.gameObject.SetActive(false); // 隱藏廣播文字
        }
    }

    // 協程：顯示所有攤位名稱文字 (遊戲開始時)
    IEnumerator ShowTextsAfterDelay()
    {
        yield return new WaitForSeconds(initialTextDelay);
        foreach (GameObject stallRoot in stallRootObjects)
        {
            stallRoot.SetActive(true); // 啟用所有攤位根物件，文字顯示
        }
        Debug.Log("All stall names are now visible.");
    }

    // 協程：廣播問題 (遊戲開始時)
    IEnumerator BroadcastQuestionAfterDelay()
    {
        yield return new WaitForSeconds(initialTextDelay + questionBroadcastDelay);

        // 為了簡單起見，隨機選擇一個攤位作為問題（但實際上點擊哪個都導向魚攤）
        if (stallNames.Count > 0 && questionBroadcastTextMeshPro != null)
        {
            int randomIndex = Random.Range(0, stallNames.Count);
            currentQuestionStallName = stallNames[randomIndex];
            questionBroadcastTextMeshPro.text = $"請點選{currentQuestionStallName}攤位！";
            questionBroadcastTextMeshPro.gameObject.SetActive(true);
            Debug.Log($"Initial question broadcasted: 請點選{currentQuestionStallName}攤位！");
        }
        else
        {
            Debug.LogWarning("No stall names collected or questionBroadcastTextMeshPro is not assigned. Cannot broadcast question.");
        }
    }

    // 協程：將攝影機移動到魚攤並結束回合/階段
    IEnumerator MoveCameraToFishStallAndEndRound()
    {
        Debug.Log("準備將攝影機轉向魚攤...");
        HideAllStallNamesAndQuestion(); // 立即隱藏所有文字

        if (cameraTarget_FishStall == null)
        {
            Debug.LogError("cameraTarget_FishStall is not assigned! Cannot move camera.");
            yield break; // 結束協程
        }

        // 開始平滑移動攝影機到魚攤
        yield return StartCoroutine(SmoothCameraMove(cameraTarget_FishStall.position, cameraTarget_FishStall.rotation));

        Debug.Log("攝影機已成功轉向魚攤。遊戲流程結束此階段。");
        // 到此為止，所有文字都已經消失，攝影機也轉向了魚攤。
        // 如果遊戲有下一階段，你可以在這裡觸發它。
        // 例如：LoadNextScene(); 或者 TriggerGameEnd();
    }

    // 協程：平滑移動攝影機到目標位置
    // 這個協程現在只負責移動，不負責後續的遊戲流程啟動
    IEnumerator SmoothCameraMove(Vector3 targetPosition, Quaternion targetRotation)
    {
        Transform mainCameraTransform = Camera.main.transform;
        Vector3 startPosition = mainCameraTransform.position;
        Quaternion startRotation = mainCameraTransform.rotation;

        float elapsedTime = 0;
        float duration = Vector3.Distance(startPosition, targetPosition) / cameraMoveSpeed;

        // 如果移動距離很小或速度過快導致時間接近0，設置一個最小時間，避免瞬移
        if (duration < 0.05f) duration = 0.05f;

        while (elapsedTime < duration)
        {
            mainCameraTransform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / duration);
            mainCameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }

        // 確保最終位置和旋轉精確到位
        mainCameraTransform.position = targetPosition;
        mainCameraTransform.rotation = targetRotation;

        Debug.Log("攝影機平滑移動完成。");
    }
}