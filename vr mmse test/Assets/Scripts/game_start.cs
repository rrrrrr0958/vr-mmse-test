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
    // public float timeBetweenQuestions = 3f; // 移除此變數，因為你不需要點擊後的延遲

    [Header("攝影機目標點")]
    // 這些 Transform 物件需要你在 Unity Inspector 中拖曳設定
    public Transform cameraTarget_FishStall;
    public Transform cameraTarget_MeatStall;
    public Transform cameraTarget_WeaponStall;
    public Transform cameraTarget_BakeryStall;
    public Transform cameraTarget_FruitStall;

    [Header("攝影機移動設定")]
    public float cameraMoveSpeed = 50.0f;    // 攝影機移動的速度，數值越大越快

    [Header("UI 連結")]
    // 這個 TextMeshPro 物件需要你在 Unity Inspector 中拖曳設定
    public TMPro.TextMeshPro questionBroadcastTextMeshPro; // 用於顯示問題的 TextMeshPro 組件

    // =========================================================================
    // 私有變數 (腳本內部使用)
    // =========================================================================

    // 用於儲存所有可點擊根物件的列表 (帶有 "StallNameText" 標籤)
    private GameObject[] stallRootObjects;
    // 儲存所有攤位的文字名稱 (例如 "魚攤", "肉攤")
    private List<string> stallNames = new List<string>();
    // 當前需要玩家點擊的攤位名稱
    private string currentQuestionStallName;

    // 儲存所有攝影機目標點，方便按索引訪問
    private List<Transform> allCameraTargets = new List<Transform>();
    private int currentTargetIndex = 0; // 用於追蹤當前攝影機應該移動到的目標索引

    // =========================================================================
    // Unity 生命周期方法
    // =========================================================================

    void Awake()
    {
        // Awake 在 Start 之前被調用，確保所有引用被設置
        // 找到所有帶有 "StallNameText" 標籤的根物件
        stallRootObjects = GameObject.FindGameObjectsWithTag("StallNameText");
        Debug.Log($"Awake: Found {stallRootObjects.Length} stall clickable root objects by tag.");

        // 收集所有攤位的名稱並禁用它們 (直到需要顯示)
        foreach (GameObject stallRoot in stallRootObjects)
        {
            stallRoot.SetActive(false); // 初始禁用物件，以便後面 ShowTextsAfterDelay 啟用

            TMPro.TextMeshPro textMeshPro = stallRoot.GetComponentInChildren<TMPro.TextMeshPro>();
            if (textMeshPro != null)
            {
                string stallName = textMeshPro.text;
                stallNames.Add(stallName);
                Debug.Log($"Awake: Collected stall name: {stallName} from {stallRoot.name}.");
            }
            else
            {
                Debug.LogWarning($"Awake: Stall root '{stallRoot.name}' has 'StallNameText' tag but no TextMeshPro component found in children.");
            }
        }
        Debug.Log($"Awake: Total stall names collected: {stallNames.Count}");

        // 初始化攝影機目標點列表
        // 確保這些目標點在 Inspector 中被指定，否則會出現 NullReferenceException
        if (cameraTarget_FishStall != null) allCameraTargets.Add(cameraTarget_FishStall);
        if (cameraTarget_MeatStall != null) allCameraTargets.Add(cameraTarget_MeatStall);
        if (cameraTarget_WeaponStall != null) allCameraTargets.Add(cameraTarget_WeaponStall);
        if (cameraTarget_BakeryStall != null) allCameraTargets.Add(cameraTarget_BakeryStall);
        if (cameraTarget_FruitStall != null) allCameraTargets.Add(cameraTarget_FruitStall);

        // 可選：隨機化攝影機目標點的順序，或者按照你希望的固定順序排列它們
        // For demonstration, let's just make sure we have at least one target for the loop.
        if (allCameraTargets.Count == 0)
        {
            Debug.LogError("Error: No camera target transforms are assigned in the Inspector! Please assign at least one camera target.");
        }
    }

    void Start()
    {
        Debug.Log("GameManager Start() called.");

        // 確保 questionBroadcastTextMeshPro 引用已設置
        if (questionBroadcastTextMeshPro != null)
        {
            questionBroadcastTextMeshPro.gameObject.SetActive(false); // 初始禁用廣播文字物件
            Debug.Log("Start: QuestionBroadcastText (TextMeshPro) set to inactive.");
        }
        else
        {
            Debug.LogError("Error: questionBroadcastTextMeshPro is not assigned in the Inspector!");
        }

        // 遊戲開始時，不移動攝影機。直接開始顯示文字和廣播第一個問題。
        // 攝影機的初始位置將是你在編輯器中放置 Main Camera 的位置。
        StartCoroutine(ShowTextsAfterDelay());
        StartCoroutine(BroadcastQuestionAfterDelay());
    }

    void Update()
    {
        if (Input.GetMouseButtonDown(0)) // 檢查滑鼠左鍵是否按下
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition); // 從滑鼠位置發射射線
            RaycastHit hit;

            // 創建只包含 "StallLayer" 的 LayerMask
            int stallLayerMask = 1 << LayerMask.NameToLayer("StallLayer");

            // Raycast 只會擊中 StallLayer 中的物件
            if (Physics.Raycast(ray, out hit, Mathf.Infinity, stallLayerMask))
            {
                Debug.Log($"Raycast hit object: {hit.collider.name} (Tag: {hit.collider.tag})");

                // 檢查被點擊的物體是否有 "StallNameText" 標籤
                if (hit.collider.CompareTag("StallNameText"))
                {
                    Debug.Log($"Hit object '{hit.collider.name}' has 'StallNameText' tag. Attempting to get TextMeshPro component from its children...");

                    // 從被點擊的 Collider 所屬的 GameObject (現在是根物件) 的子物件中獲取 TextMeshPro 組件
                    TMPro.TextMeshPro clickedTextMeshPro3D = hit.collider.gameObject.GetComponentInChildren<TMPro.TextMeshPro>();

                    string clickedStallName = null;

                    if (clickedTextMeshPro3D != null)
                    {
                        clickedStallName = clickedTextMeshPro3D.text;
                        Debug.Log($"Successfully got TextMeshPro component from child of {hit.collider.name}. Text: {clickedStallName}");
                    }
                    else
                    {
                        Debug.LogWarning($"點擊的物件 '{hit.collider.name}' 有 'StallNameText' 標籤，但**沒有在其自身或子物件中找到 TextMeshPro (3D) 組件**。請檢查其層級結構或確認 TextMeshPro 組件類型是否正確。");
                    }

                    if (clickedStallName != null)
                    {
                        if (clickedStallName == currentQuestionStallName)
                        {
                            Debug.Log("恭喜！點擊正確！");
                            if (questionBroadcastTextMeshPro != null)
                            {
                                questionBroadcastTextMeshPro.text = "恭喜！點擊正確！";
                            }
                            // 處理點擊正確的邏輯，例如計分
                            // ...

                            // 無論對錯，都啟動下一個遊戲回合流程
                            StartNextRoundFlow();
                        }
                        else
                        {
                            Debug.Log("點擊錯誤，請再試一次。");
                            if (questionBroadcastTextMeshPro != null)
                            {
                                questionBroadcastTextMeshPro.text = "點擊錯誤，請再試一次。";
                            }
                            // 處理點擊錯誤的邏輯
                            // ...

                            // 無論對錯，都啟動下一個遊戲回合流程
                            StartNextRoundFlow();
                        }
                    }
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
    void HideAllStallNames()
    {
        Debug.Log("HideAllStallNames is called.");
        // 確保 stallRootObjects 已經被找到
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
            questionBroadcastTextMeshPro.gameObject.SetActive(false);
        }
    }

    // 啟動下一回合的遊戲流程 (點擊後觸發)
    void StartNextRoundFlow()
    {
        // 隱藏所有攤位文字和當前問題文字 (立即隱藏)
        HideAllStallNames();

        // 直接啟動 MoveCameraAndBroadcastNextQuestion 協程，不再有延遲
        StartCoroutine(MoveCameraAndBroadcastNextQuestion());
    }

    // 協程：處理攝影機移動和廣播下一個問題
    IEnumerator MoveCameraAndBroadcastNextQuestion()
    {
        // 移除原有的 yield return new WaitForSeconds(timeBetweenQuestions);
        // 現在點擊後會立即移動攝影機

        // 確定下一個攝影機目標點
        if (allCameraTargets.Count > 0)
        {
            currentTargetIndex = (currentTargetIndex + 1) % allCameraTargets.Count;
            Transform nextCameraTarget = allCameraTargets[currentTargetIndex];
            Debug.Log($"Moving camera to next target: {nextCameraTarget.name}");

            // 開始平滑移動攝影機到下一個目標點
            yield return StartCoroutine(SmoothCameraMove(nextCameraTarget.position, nextCameraTarget.rotation));

            // 攝影機移動完成後，廣播新的問題（這會顯示新的攤位文字和問題文字）
            StartCoroutine(BroadcastQuestionAfterDelay());
        }
        else
        {
            Debug.LogWarning("No camera targets assigned for rotation. Game might not proceed to next round correctly.");
        }
    }

    // 協程：顯示所有攤位名稱文字 (在遊戲開始或新問題廣播前)
    IEnumerator ShowTextsAfterDelay()
    {
        // 這裡的延遲用於遊戲開始時的首次文字顯示
        yield return new WaitForSeconds(initialTextDelay);

        foreach (GameObject stallRoot in stallRootObjects)
        {
            stallRoot.SetActive(true); // 啟用所有攤位根物件，文字顯示
            Debug.Log($"Enabled stall root: {stallRoot.name}.");
        }
    }

    // 協程：廣播問題 (在攝影機到位且文字顯示後)
    IEnumerator BroadcastQuestionAfterDelay()
    {
        // 在廣播新問題前等待一小段時間 (確保攝影機移動和文字顯示完成)
        yield return new WaitForSeconds(questionBroadcastDelay);

        // 隨機選擇一個攤位作為問題
        if (stallNames.Count > 0)
        {
            int randomIndex = Random.Range(0, stallNames.Count);
            currentQuestionStallName = stallNames[randomIndex];
            Debug.Log($"Broadcasting question: 請點選{currentQuestionStallName}攤位！");

            if (questionBroadcastTextMeshPro != null)
            {
                questionBroadcastTextMeshPro.text = $"請點選{currentQuestionStallName}攤位！";
                questionBroadcastTextMeshPro.gameObject.SetActive(true); // 啟用廣播文字物件
                Debug.Log("Broadcast text (TextMeshPro) enabled and set.");
            }
        }
        else
        {
            Debug.LogWarning("Stall names list is empty. Cannot broadcast question.");
        }
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
        if (duration < 0.05f) duration = 0.05f; // 至少移動0.05秒

        while (elapsedTime < duration)
        {
            // 使用 Lerp 和 Slerp 平滑過渡位置和旋轉
            mainCameraTransform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / duration);
            mainCameraTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime / duration);
            elapsedTime += Time.deltaTime;
            yield return null; // 等待下一幀
        }

        // 確保最終位置和旋轉精確到位
        mainCameraTransform.position = targetPosition;
        mainCameraTransform.rotation = targetRotation;

        Debug.Log("攝影機已移動到新位置。");
        Debug.Log("攝影機應該朝向的角度: " + targetRotation.eulerAngles);
        Debug.Log("實際攝影機角度: " + Camera.main.transform.rotation.eulerAngles);
        //Debug.Log(GameObject.Find("CameraTarget_FishStall").transform.rotation.eulerAngles);

    }
}