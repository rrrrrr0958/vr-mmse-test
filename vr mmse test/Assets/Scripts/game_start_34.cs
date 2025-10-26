using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase;
using Firebase.Database;
using Firebase.Extensions;
using UnityEngine.Networking;
using System.Text.RegularExpressions;
using System.Text;
using System.IO;
using System;


#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;
#endif


public class game_start_34 : MonoBehaviour
{
    // =========================================================================
    // 公開變數 (在 Unity Inspector 中設定)
    // =========================================================================
    
    private FirebaseManager_Firestore FirebaseManager;
    [Header("遊戲開始設定")]
    public float initialTextDelay = 0.5f;
    public float questionBroadcastDelay = 1f;
    public float timeBetweenQuestions = 1f;
    [Tooltip("語音問題結束後，到開始錄音前的緩衝時間。調低這個值可以更快開始錄音。")]
    public float voiceQuestionBufferTime = 0.0f;

    [Header("點擊題設定")]
    public float clickResponseDuration = 5.0f;

    // 【新增】點擊音效設定
    [Header("點擊音效")]
    public AudioSource clickAudioSource; // 播放點擊音效的 AudioSource
    public AudioClip clickSoundEffect;   // 實際的點擊音效檔案

    [Header("攝影機移動設定")]
    public float cameraMoveSpeed = 5.0f;

    [Header("UI 連結")]
    public TMPro.TextMeshPro questionBroadcastTextMeshPro;
    public Image highlightCircleImage;

    [Header("語音問題設定")]
    public AudioSource voiceAudioSource;
    public AudioClip fishStallAudioClip;
    public AudioClip fruitStallAudioClip;
    public AudioClip weaponStallAudioClip;
    public AudioClip breadStallAudioClip;
    public AudioClip meatStallAudioClip;

    // 【新增】轉場提示音檔
    [Header("轉場提示音檔")]
    public AudioClip transitToFishStallAudioClip; // 前往魚攤
    public AudioClip transitToBananaAudioClip; // 前往香蕉

    [Header("魚攤問題語音設定")]
    public AudioClip whatIsSellingAudioClip;
    public AudioClip fishColorAudioClip; // <-- 這是被移除的語音，但程式碼中暫時保留以避免 Inspector 報錯
    public AudioClip whatIsThatAudioClip; // <-- 新的第二題語音 (原來的 Q3)
    public AudioClip bananaColorAudioClip; // <-- 【新增】新的第三題語音（請在 Unity Inspector 中設定此 AudioClip！）

    // >>> 【新增】第二輪點擊音檔 (接下來，請點選 XX 攤)
    [Header("第二輪點擊音檔 (接下來...)")]
    public AudioClip nextFruitStallAudioClip;
    public AudioClip nextWeaponStallAudioClip;
    public AudioClip nextBreadStallAudioClip;
    public AudioClip nextMeatStallAudioClip;


    [Header("語音辨識設定")]
    public string serverUrl = "http://localhost:5000/recognize_speech";
    public float recordingDuration = 4f;

    // ===== 新增：VR 控制器 & Ray 設定 =====
    [Header("VR 控制器設定（方案A）")]
    public Transform rightController;
    // 【新增】
    public Transform leftController;
    // 【新增】
    public bool useOVRInput = true;
    public bool useNewInputSystem = false;

    [Header("點擊圖層")]
    public LayerMask stallLayerMask;

    // 在 GameManager 類別開頭
    [Header("VR 攝影機設定")]
    public Transform xrOriginTransform;

    [Header("攝影機目標點")]
    public Transform cameraTarget_FishStall; // 傳統模式用
    public Transform vrCameraTarget_FishStall; // VR 模式用
    public Transform cameraTarget_banana_3; // 第三題燈光目標點

    [Header("第三題物件與文字板")]
    [Tooltip("要取代 Highlight Circle 的新物件 (banana_bg_4)")]
    public GameObject banana_bg_4;
    [Tooltip("第三題語音提示文字板 (請說出答案/語音處理中)")]
    public TMPro.TextMeshPro question3_VoiceText;

#if ENABLE_INPUT_SYSTEM
    [Header("XR Input Actions")]
    public InputActionProperty rightSelectAction;
#endif

    [Header("Ray Origin（可選，沒設就用 Right Controller）")]
    public Transform rayOriginOverride;

    private GameObject[] clickableStallObjects;
    private List<string> stallNames = new List<string>();
    private List<string> nonFishStallNames = new List<string>();
    private bool hasClickedStall = false;
    private Coroutine initialQuestionCoroutine;
    private string currentTargetStallName = "";
    private int correctAnswersCount = 0;
    private int voiceCorrectAnswersCount = 0;
    private bool isWaitingForClickInput = false;

    private DatabaseReference dbReference;
    private FirebaseApp app;
    private AudioClip recordingClip;
    private List<string> currentQuestionAnswers = new List<string>();

    private GameObject currentHoveredObject = null;
    private Color originalHoverColor;
    private GameObject clickedStallObject = null;
    private Dictionary<GameObject, Color> originalColors = new Dictionary<GameObject, Color>();

    private HashSet<GameObject> lockedClickedObjects = new HashSet<GameObject>();

    private int currentVoiceQuestionIndex = 0; // 新增：用於追蹤目前的語音問題編號

    private Coroutine clickHandlingCoroutine = null;

#if ENABLE_INPUT_SYSTEM
    private bool lastTriggerPressed = false;
#endif

    void Awake()
    {
        clickableStallObjects = GameObject.FindGameObjectsWithTag("ClickableStall");
        Debug.Log($"Awake: Found {clickableStallObjects.Length} stall clickable objects by tag.");

        Dictionary<string, string> nameMapping = new Dictionary<string, string>
        {
            { "Breadbg_34", "麵包" },
            { "Meatonbg_34", "肉攤" },
            { "fishbg_34", "魚攤" },
            { "fruitbg_34", "蔬果" },
            { "Weaponbg_34", "武器" }
        };

        foreach (GameObject stallObject in clickableStallObjects)
        {
            stallObject.SetActive(true);
            if (stallObject.GetComponent<Renderer>() != null)
            {
                originalColors[stallObject] = stallObject.GetComponent<Renderer>().material.color;
            }

            if (nameMapping.TryGetValue(stallObject.name, out string stallName))
            {
                stallNames.Add(stallName);
                if (stallName != "魚攤")
                {
                    nonFishStallNames.Add(stallName);
                }
            }
            else
            {
                Debug.LogWarning($"Awake: Object '{stallObject.name}' has 'ClickableStall' tag but no name mapping found. This stall name will not be used for initial question.");
            }
        }
        Debug.Log($"Awake: Total stall names collected for initial question: {stallNames.Count}");
        Debug.Log($"Awake: Non-fish stall names collected: {nonFishStallNames.Count}");

        if (cameraTarget_FishStall == null) Debug.LogError("Error: cameraTarget_FishStall is not assigned in the Inspector! Please assign it.");
        if (questionBroadcastTextMeshPro == null) Debug.LogWarning("Warning: questionBroadcastTextMeshPro is not assigned in the Inspector. Initial question will only appear in Console.");
        if (highlightCircleImage == null) Debug.LogError("Error: highlightCircleImage is not assigned in the Inspector! Please assign it for question 3.");
        if (voiceAudioSource == null) Debug.LogError("Error: voiceAudioSource is not assigned in the Inspector! Please assign the AudioSource from Main Camera.");
        if (fishStallAudioClip == null || fruitStallAudioClip == null || weaponStallAudioClip == null ||
            breadStallAudioClip == null || meatStallAudioClip == null ||
            whatIsSellingAudioClip == null || fishColorAudioClip == null || whatIsThatAudioClip == null)
        {
            Debug.LogWarning("Warning: Some AudioClips are not assigned. Voice questions may not play.");
        }

        if (highlightCircleImage != null) highlightCircleImage.gameObject.SetActive(false);

        // >>> [修改點 2] 初始化時隱藏新增的第三題物件和文字板
        if (banana_bg_4 != null) banana_bg_4.SetActive(false);
        if (question3_VoiceText != null) question3_VoiceText.gameObject.SetActive(false);
        // <<< [修改點 2]

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            var dependencyStatus = task.Result;
            if (dependencyStatus == DependencyStatus.Available)
            {
                app = FirebaseApp.DefaultInstance;
                dbReference = FirebaseDatabase.DefaultInstance.RootReference;
                Debug.Log("Firebase 已成功初始化！");
            }
            else
            {
                Debug.LogError($"無法解決 Firebase 依賴問題: {dependencyStatus}");
            }
        });
    }

    void Start()
    {
        Debug.Log("GameManager Start() called.");
        SetupCameraMode();
        if (questionBroadcastTextMeshPro != null)
        {
            questionBroadcastTextMeshPro.gameObject.SetActive(false);
        }
        StartCoroutine(ShowTextsAfterDelay());
        initialQuestionCoroutine = StartCoroutine(MainClickSequence());
    }

    void Update()
    {
        // --- 1. 取得 Ray 的來源 (VR 模式優先，否則退回滑鼠) ---

        RaycastHit hoverHit = default;
        Ray hoverRay = default;
        Transform activeController = null;
        bool didRaycastHit = false;

        // 新版輸入系統 & VR/OVR 模式
        if (useNewInputSystem || useOVRInput)
        {
            // 優先檢查右手
            if (rightController != null)
            {
                hoverRay = new Ray(rightController.position, rightController.forward);
                if (Physics.Raycast(hoverRay.origin, hoverRay.direction, out hoverHit, Mathf.Infinity, stallLayerMask))
                {
                    didRaycastHit = true;
                    activeController = rightController;
                }
            }

            // 如果右手沒有命中，則檢查左手
            if (!didRaycastHit && leftController != null)
            {
                hoverRay = new Ray(leftController.position, leftController.forward);
                if (Physics.Raycast(hoverRay.origin, hoverRay.direction, out hoverHit, Mathf.Infinity, stallLayerMask))
                {
                    didRaycastHit = true;
                    activeController = leftController;
                }
            }
        }

        // 如果目前 VR Raycast 尚未命中，則退回使用滑鼠 (無論是否啟用 VR)
        if (!didRaycastHit)
        {
            // 【修正點 1】：使用新版 Input System 讀取滑鼠位置
            if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
            {
                Vector2 pos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
                Ray mouseRay = Camera.main.ScreenPointToRay(pos);
                // 原始呼叫：HandleClickRaycast(mouseRay.origin, mouseRay.direction);

                // 如果您想使用新的 Coroutine 方式：
                // RaycastHit hit;
                // if (Physics.Raycast(mouseRay.origin, mouseRay.direction, out hit, Mathf.Infinity, stallLayerMask.value))
                // {
                //    if (hit.collider != null && hit.collider.CompareTag("ClickableStall"))
                //    {
                //        if (clickHandlingCoroutine == null)
                //        {
                //             clickHandlingCoroutine = StartCoroutine(HandleClickRaycastCoroutine(mouseRay.origin, mouseRay.direction));
                //        }
                //    }
                // }
            }
        }


        // --- 2. 處理 Hover 偵測 (邏輯不變) ---
        if (didRaycastHit)
        {
            // 此時 hoverHit 已經確定被賦值
            GameObject hoverObj = hoverHit.collider.gameObject;
            if (lockedClickedObjects.Contains(hoverObj))
                return;

            if (hoverObj != currentHoveredObject)
            {
                // 重置上一個 Hover 物件的顏色
                if (currentHoveredObject != null && !lockedClickedObjects.Contains(currentHoveredObject) && originalColors.ContainsKey(currentHoveredObject))
                {
                    currentHoveredObject.GetComponent<Renderer>().material.color = originalColors[currentHoveredObject];
                }
                // 設置新的 Hover 物件的顏色
                currentHoveredObject = hoverObj;
                Renderer rend = currentHoveredObject.GetComponent<Renderer>();

                // 確保材質顏色屬性存在 (避免錯誤)
                Color originalColor = rend.material.HasProperty("_BaseColor") ? rend.material.GetColor("_BaseColor") : rend.material.color;
                Color darkColor = originalColor * 0.7f;

                if (rend.material.HasProperty("_BaseColor"))
                    rend.material.SetColor("_BaseColor", darkColor);
                else
                    rend.material.color = darkColor;
            }
        }
        else
        {
            // 如果沒有命中任何物件，則重置目前的 Hover 物件
            if (currentHoveredObject != null && !lockedClickedObjects.Contains(currentHoveredObject) && originalColors.ContainsKey(currentHoveredObject))
            {
                currentHoveredObject.GetComponent<Renderer>().material.color = originalColors[currentHoveredObject];
                currentHoveredObject = null;
            }
        }

        // --- 3. 處理點擊邏輯 ---
        if (!isWaitingForClickInput || hasClickedStall || string.IsNullOrEmpty(currentTargetStallName))
            return;

        // 新版輸入系統：VR 控制器點擊 (檢查左右手)
        if (useNewInputSystem || useOVRInput)
        {
            bool pressed = false;

            // ... (VR 控制器點擊邏輯不變) ...
            var xrRight = UnityEngine.InputSystem.XR.XRController.rightHand;
            if (xrRight != null)
            {
                var trigger = xrRight.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("trigger");
                var aButton = xrRight.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primaryButton");

                bool nowPressed = false;
                if (trigger != null)
                {
                    nowPressed = trigger.ReadValue() > 0.5f;
                    // 使用原有的邏輯追蹤單次按下
                    if (nowPressed && !lastTriggerPressed) pressed = true;
                    lastTriggerPressed = nowPressed;
                }
                if (aButton != null && aButton.wasPressedThisFrame)
                    pressed = true;
            }

            var xrLeft = UnityEngine.InputSystem.XR.XRController.leftHand;
            if (xrLeft != null)
            {
                var trigger = xrLeft.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("trigger");
                var xButton = xrLeft.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primaryButton");

                if (trigger != null && trigger.ReadValue() > 0.5f)
                {
                    pressed = true;
                }
                if (xButton != null && xButton.wasPressedThisFrame)
                {
                    pressed = true;
                }
            }

            if (pressed)
            {
                // 找到發出 Ray 的來源。
                Transform triggerOrigin = activeController; // 優先使用 Hover 命中的控制器

                if (triggerOrigin == null)
                {
                    // 如果沒有 Hover 命中，但按下了按鈕，則嘗試用右手或左手的 Ray 點擊
                    if (rightController != null) triggerOrigin = rightController;
                    else if (leftController != null) triggerOrigin = leftController;
                }

                if (triggerOrigin != null)
                {
                    // 使用控制器的 Ray 執行點擊判定
                    HandleClickRaycast(triggerOrigin.position, triggerOrigin.forward);
                }
            }
        }

        // 【修正點 2】統一使用新版輸入系統：滑鼠點擊
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 pos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            Ray mouseRay = Camera.main.ScreenPointToRay(pos);
            HandleClickRaycast(mouseRay.origin, mouseRay.direction);
        }
    }

    int currentQuestionIndex = 0;
    Dictionary<string, string> correctOptions = new Dictionary<string, string>();
    Dictionary<string, string> playerOptions = new Dictionary<string, string>();

    void HandleClickRaycast(Vector3 origin, Vector3 direction)
    {
        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, Mathf.Infinity, stallLayerMask.value))
        {
            if (hit.collider != null && hit.collider.CompareTag("ClickableStall"))
            {
                // >>> 【新增】點擊音效播放邏輯
                if (clickAudioSource != null && clickSoundEffect != null)
                {
                    clickAudioSource.PlayOneShot(clickSoundEffect);
                }
                // <<<

                string clickedObjectName = hit.collider.gameObject.name;
                string clickedStallName = "";

                if (clickedObjectName.Contains("Breadbg_34")) clickedStallName = "麵包";
                else if (clickedObjectName.Contains("Meatonbg_34")) clickedStallName = "肉攤";
                else if (clickedObjectName.Contains("fishbg_34")) clickedStallName = "魚攤";
                else if (clickedObjectName.Contains("fruitbg_34")) clickedStallName = "蔬果";
                else if (clickedObjectName.Contains("Weaponbg_34")) clickedStallName = "武器";
                else
                {
                    Debug.LogWarning($"點擊了未知物件：{clickedObjectName}，無法判斷攤位名稱。");
                    return;
                }

                Debug.Log($"你點擊了：{clickedStallName}");

                if (isWaitingForClickInput && !string.IsNullOrEmpty(currentTargetStallName))
                {
                    clickedStallObject = hit.collider.gameObject;
                    
                    // 優化：無論點擊的是不是 Hover 物件，都先將 Hover 顏色復原
                    if (currentHoveredObject != null && originalColors.ContainsKey(currentHoveredObject))
                    {
                        currentHoveredObject.GetComponent<Renderer>().material.color = originalColors[currentHoveredObject];
                        currentHoveredObject = null;
                    }

                    if (clickedStallObject != null)
                    {
                        Renderer rend = clickedStallObject.GetComponent<Renderer>();
                        if (rend.material.HasProperty("_BaseColor"))
                            rend.material.SetColor("_BaseColor", Color.yellow);
                        else
                            rend.material.color = Color.yellow;

                        lockedClickedObjects.Add(clickedStallObject);

                        // >>> 【新增】隱藏所有其他攤位物件
                        foreach (GameObject stallObject in clickableStallObjects)
                        {
                            // 判斷：如果這個攤位不是當前點擊的攤位，就隱藏它
                            if (stallObject != clickedStallObject)
                            {
                                stallObject.SetActive(false);
                            }
                        }
                        // <<< 【新增結束】
                    }

                    if (clickedStallName == currentTargetStallName)
                    {
                        Debug.Log($"✅ 正確！點擊了目標攤位: {currentTargetStallName}。");
                        correctAnswersCount++;
                    }
                    else
                    {
                        Debug.LogWarning($"❌ 錯誤！你點擊了 {clickedStallName}，但正確答案是 {currentTargetStallName}。");
                    }

                    // 儲存這一題的紀錄
                    correctOptions[$"Q{currentQuestionIndex + 1}"] = currentTargetStallName;
                    playerOptions[$"Q{currentQuestionIndex + 1}"] = clickedStallName;

                    string testId = FirebaseManager_Firestore.Instance.testId;
                    string levelIndex = "4";
                    FirebaseManager_Firestore.Instance.SaveLevelOptions(testId, levelIndex, correctOptions, playerOptions);
                    currentQuestionIndex++;

                    currentTargetStallName = "";
                    isWaitingForClickInput = false;
                }
                else
                {
                    Debug.Log("目前不在等待點擊的狀態，點擊無效。");
                }
            }
        }
        Debug.DrawRay(origin, direction.normalized * 100f, Color.magenta, 0.25f);
    }

    void HideAllStallNamesAndQuestion()
    {
        foreach (GameObject stallRoot in clickableStallObjects)
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
        foreach (GameObject stallRoot in clickableStallObjects)
        {
            stallRoot.SetActive(true);
        }
        Debug.Log("所有攤位物件已顯示。");
    }

    void ResetAllStallColors()
    {
        foreach (var entry in originalColors)
        {
            if (entry.Key != null && entry.Key.GetComponent<Renderer>() != null)
            {
                entry.Key.GetComponent<Renderer>().material.color = entry.Value;
            }
        }
        clickedStallObject = null;
        currentHoveredObject = null;
        lockedClickedObjects.Clear();
        Debug.Log("所有攤位物件的顏色已重置。");
    }

    //來確保在下一回合開始時，所有攤位物件都會被重新顯示。
    void ShowAllStallObjects()
    {
        foreach (GameObject stallObject in clickableStallObjects)
        {
            if (stallObject != null && !stallObject.activeSelf)
            {
                stallObject.SetActive(true);
            }
        }
        Debug.Log("所有攤位物件已重新啟用。");
    }


    IEnumerator MainClickSequence()
    {
        yield return new WaitForSeconds(initialTextDelay + questionBroadcastDelay);

        List<string> tempNonFishStallNames = new List<string>(nonFishStallNames);

        correctAnswersCount = 0;

        // --- 第一、二輪 (隨機攤位) ---
        for (int i = 0; i < 2; i++)
        {
            ShowAllStallObjects(); // 確保所有物件在回合開始時顯示
            ResetAllStallColors();

            if (tempNonFishStallNames.Count == 0)
            {
                Debug.LogWarning("沒有足夠的非魚攤名稱可供隨機選擇！請確保至少有兩個非魚攤。");
                yield break;
            }

            int randomIndex = UnityEngine.Random.Range(0, tempNonFishStallNames.Count);
            currentTargetStallName = tempNonFishStallNames[randomIndex];

            // --- 傳遞輪次資訊給語音播放函式 ---
            string initialQuestion = $"請點選 {currentTargetStallName} 攤位！";
            Debug.Log($"Console 問題 (第 {i + 1} 次): {initialQuestion}");

            int currentRound = i + 1; // 當前輪次 (1 或 2)
            PlayInitialVoiceQuestion(currentTargetStallName, currentRound);

            tempNonFishStallNames.RemoveAt(randomIndex);

            // 啟動點擊等待狀態
            isWaitingForClickInput = true;

            // 1. 等待語音播放結束 + 緩衝時間
            AudioClip currentClip = GetAudioClipForStall(currentTargetStallName, currentRound);
            float voiceAndBufferTime = (currentClip != null ? currentClip.length : 0f) + voiceQuestionBufferTime;

            // 【修改點 A】移除 clickResponseDuration 的固定等待，只等待語音緩衝時間
            yield return new WaitForSeconds(voiceAndBufferTime);

            // 2. 【核心修改點 B】無限期等待玩家點擊 (直到 HandleClickRaycast 將 isWaitingForClickInput 設為 false)
            // 玩家點擊後，HandleClickRaycast 會將 isWaitingForClickInput 設為 false
            Debug.Log("等待玩家點擊 (無限期)...");
            yield return new WaitUntil(() => isWaitingForClickInput == false);
            // 玩家已點擊

            // currentTargetStallName 在 HandleClickRaycast 中被清空
            // isWaitingForClickInput 在 HandleClickRaycast 中被設為 false

            yield return new WaitForSeconds(timeBetweenQuestions);
        }

        // --- 第三輪 (固定魚攤) ---
        ShowAllStallObjects();
        ResetAllStallColors();

        currentTargetStallName = "魚攤";
        string finalQuestion = $"請點選 {currentTargetStallName} 攤位！";
        Debug.Log($"Console 問題 (第 3 次，固定魚攤): {finalQuestion}");

        // 傳遞輪次資訊 (第 3 輪)
        int fishStallRound = 3;
        PlayInitialVoiceQuestion(currentTargetStallName, fishStallRound);

        // 啟動點擊等待狀態
        isWaitingForClickInput = true;

        // 1. 等待語音播放結束 + 緩衝時間
        AudioClip fishStallClip = GetAudioClipForStall("魚攤", fishStallRound);
        float fishStallVoiceAndBufferTime = (fishStallClip != null ? fishStallClip.length : 0f) + voiceQuestionBufferTime;

        // 【修改點 C】移除 clickResponseDuration 的固定等待，只等待語音緩衝時間
        yield return new WaitForSeconds(fishStallVoiceAndBufferTime);

        // 2. 【核心修改點 D】無限期等待玩家點擊
        Debug.Log("等待玩家點擊魚攤 (無限期)...");
        yield return new WaitUntil(() => isWaitingForClickInput == false);
        // 玩家已點擊

        // currentTargetStallName 在 HandleClickRaycast 中被清空
        // isWaitingForClickInput 在 HandleClickRaycast 中被設為 false


        Debug.Log($"點擊題目正確數: {correctAnswersCount}/3");

        // ... (Firebase 儲存邏輯不變) ...
        string testId = FirebaseManager_Firestore.Instance.testId;
        if (testId != null)
        {
            Dictionary<string, object> scoreData = new Dictionary<string, object>();
            scoreData["Command_score"] = correctAnswersCount;

            string levelIndex = "4";
            FirebaseManager_Firestore.Instance.totalScore = FirebaseManager_Firestore.Instance.totalScore + correctAnswersCount;

            FirebaseManager_Firestore.Instance.SaveLevelData(testId, levelIndex, correctAnswersCount);
        }
        else
        {
            Debug.LogWarning("Firebase Database 未初始化，無法寫入分數。");
        }

        Debug.Log("所有點擊任務完成，準備進入魚攤流程。");

        // 只有在完成所有點擊題後才將 hasClickedStall 設為 true，用於後續流程標記
        hasClickedStall = true;

        HideAllStallNamesAndQuestion();
        StartCoroutine(MoveCameraToFishStallAndStartFishStallQuestions());
    }

    IEnumerator MoveCameraToFishStallAndStartFishStallQuestions()
    {
        Debug.Log("準備將攝影機轉向魚攤...");
        Transform targetTransform;

        if (xrOriginTransform != null && vrCameraTarget_FishStall != null)
        {
            targetTransform = vrCameraTarget_FishStall;
        }
        else
        {
            targetTransform = cameraTarget_FishStall;
        }

        if (targetTransform == null)
        {
            Debug.LogError("目標點未賦值！無法移動攝影機。");
            yield break;
        }

        // ⭐ 需求 2: 撥放前往魚攤語音
        if (transitToFishStallAudioClip != null)
        {
            voiceAudioSource.PlayOneShot(transitToFishStallAudioClip);
            // 等待語音播放完畢 (加上一個小緩衝)
            yield return new WaitForSeconds(transitToFishStallAudioClip.length + 0.5f);
        }
        // ⭐ 轉場語音結束

        yield return StartCoroutine(SmoothCameraMove(targetTransform.position, targetTransform.rotation));
        Debug.Log("攝影機已成功轉向魚攤。");
        StartCoroutine(FishStallQuestionSequence());
    }

    IEnumerator FishStallQuestionSequence()
    {
        currentVoiceQuestionIndex = 0;
        yield return new WaitForSeconds(timeBetweenQuestions);

        // --- 第一個問題 (Q1) ---
        currentVoiceQuestionIndex = 1;
        yield return StartCoroutine(PlayAudioClipAndThenWait(whatIsSellingAudioClip));
        yield return StartCoroutine(WaitForAnswer(new List<string> { "魚", "魚肉", "與", "雨", "雨肉", "雨攤", "魚攤", "雨肉攤", "與肉攤", "肉", "海鮮", "魚肉攤", "一", "一肉", "一攤", "一肉攤", "露", "魚露", "一露", "魚露攤", "一露攤", "旗魚攤", "旗魚", "旗一", "旗一攤", "及一", "及一攤", "及魚", "及魚攤", "祈雨", "祈雨攤" }));

        yield return new WaitForSeconds(timeBetweenQuestions);

        // ⭐ 需求 3: Q1 到 Q2 的轉場語音與攝影機移動
        if (cameraTarget_banana_3 != null)
        {
            Debug.Log("攝影機開始轉向第二題目標（香蕉）...");

            // 撥放轉場語音：我們要換到水果攤了！
            if (transitToBananaAudioClip != null)
            {
                voiceAudioSource.PlayOneShot(transitToBananaAudioClip);
                // 等待語音播放完畢 (加上一個小緩衝)
                yield return new WaitForSeconds(transitToBananaAudioClip.length + 0.5f);
            }

            yield return StartCoroutine(SmoothCameraMove(cameraTarget_banana_3.position, cameraTarget_banana_3.rotation));
            if (banana_bg_4 != null)
            {
                banana_bg_4.SetActive(true);
                Debug.Log("banana_bg_4 (香蕉物件) 已在視角轉換後啟用。");
            }
        }
        yield return new WaitForSeconds(timeBetweenQuestions);
        // ⭐ 轉場語音與移動結束

        // --- 第二個問題 (Q2: 問香蕉是什麼) ---
        currentVoiceQuestionIndex = 2;
        Debug.Log("Console 問題 2: 那個水果是什麼？");
        ShowHighlightCircle();
        yield return StartCoroutine(PlayAudioClipAndThenWait(whatIsThatAudioClip));
        yield return StartCoroutine(WaitForAnswer(new List<string> { "香蕉", "芭蕉" }));
        HideHighlightCircle();

        Debug.Log("Console: 所有魚攤問題已完成！");
        Debug.Log($"語音題目正確數: {voiceCorrectAnswersCount}/2"); // game_4 總題數 = 2

        currentVoiceQuestionIndex = 0;

        string testId = FirebaseManager_Firestore.Instance.testId;
        string levelIndex = "5";
        FirebaseManager_Firestore.Instance.totalScore = FirebaseManager_Firestore.Instance.totalScore + voiceCorrectAnswersCount;
        FirebaseManager_Firestore.Instance.SaveLevelData(testId, levelIndex, voiceCorrectAnswersCount);

        // UploadVoiceScoreToFirebase(voiceCorrectAnswersCount);
        SceneFlowManager.instance.LoadNextScene();
    }


    IEnumerator PlayAudioClipAndThenWait(AudioClip clip)
    {
        if (voiceAudioSource == null || clip == null)
        {
            Debug.LogWarning("無法播放音訊，AudioSource 或 AudioClip 為空。");
            yield break;
        }

        voiceAudioSource.clip = clip;
        voiceAudioSource.Play();
        Debug.Log($"正在播放語音，長度: {clip.length} 秒");

        yield return new WaitForSeconds(clip.length + voiceQuestionBufferTime);
    }

    //資料庫改這邊(記得命名只有兩題)
    void UploadVoiceScoreToFirebase(int score)
    {
        if (dbReference != null)
        {
            string userId = SystemInfo.deviceUniqueIdentifier;
            string timestamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");
            string recordKey = $"{userId}_{timestamp}";

            Dictionary<string, object> scoreData = new Dictionary<string, object>();
            scoreData["AnswerName_score"] = score;
            scoreData["totalQuestions"] = 3;
            scoreData["timestamp"] = ServerValue.Timestamp;
            scoreData["userName"] = "PlayerName";

            dbReference.Child("voiceScores").Child(recordKey).SetValueAsync(scoreData).ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted)
                {
                    Debug.Log($"成功將語音分數寫入 Firebase: {score}");
                }
                else if (task.IsFaulted)
                {
                    Debug.LogError($"寫入 Firebase 失敗: {task.Exception}");
                }
            });
        }
        else
        {
            Debug.LogWarning("Firebase Database 未初始化，無法寫入分數。");
        }
    }

    void ShowHighlightCircle()
    {
        // >>> [修改點 3.1] 移除 highlightCircleImage 的啟用，改用 banana_bg_4
        if (banana_bg_4 != null)
        {
            banana_bg_4.SetActive(true);
            Debug.Log("banana_bg_4 (新物件) 已啟用並顯示。");
        }
        else
        {
            Debug.LogError("banana_bg_4 未賦值，無法顯示新物件！");
        }
        // <<< [修改點 3.1]
    }

    void HideHighlightCircle()
    {
        // >>> [修改點 3.2] 隱藏 banana_bg_4
        if (banana_bg_4 != null)
        {
            banana_bg_4.SetActive(false);
            Debug.Log("banana_bg_4 已禁用。");
        }
        else if (highlightCircleImage != null)
        {
            // 如果 banana_bg_4 沒設，則退回隱藏舊的 highlightCircleImage
            highlightCircleImage.gameObject.SetActive(false);
            Debug.Log("HighlightCircle 已禁用 (banana_bg_4 未設定)。");
        }
        // <<< [修改點 3.2]
    }

    // >>> 【修改】函式定義，新增 int questionRound 參數
    void PlayInitialVoiceQuestion(string stallName, int questionRound)
    {
        Debug.Log($"嘗試播放語音給攤位: '{stallName}' (第 {questionRound} 輪)");

        // 傳遞 questionRound 參數給 GetAudioClipForStall
        AudioClip clipToPlay = GetAudioClipForStall(stallName, questionRound);

        PlayVoiceClip(clipToPlay, stallName);
    }
    // <<<

    // >>> 【修改】函式定義，新增 int questionRound 參數
    private AudioClip GetAudioClipForStall(string stallName, int questionRound)
    {
        // 對於 Q3（魚攤），使用您固定的魚攤音效
        if (questionRound == 3)
        {
            // 由於您要自己替換魚攤音檔，我們假設 fishStallAudioClip 會是您想替換的那個音檔。
            // 如果您希望 Q3 有專屬變數，建議在 public 變數中新增。
            // 目前先用原有的 fishStallAudioClip 
            return fishStallAudioClip;
        }

        // 處理 Q1 和 Q2 的邏輯
        switch (stallName)
        {
            case "蔬果":
                return (questionRound == 1) ? fruitStallAudioClip : nextFruitStallAudioClip;
            case "武器":
                return (questionRound == 1) ? weaponStallAudioClip : nextWeaponStallAudioClip;
            case "麵包":
                return (questionRound == 1) ? breadStallAudioClip : nextBreadStallAudioClip;
            case "肉攤":
                return (questionRound == 1) ? meatStallAudioClip : nextMeatStallAudioClip;
            // 如果 Q1/Q2 誤傳魚攤，則返回魚攤預設音效
            case "魚攤":
                return fishStallAudioClip;
            default: return null;
        }
    }
    // <<<

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

    IEnumerator SmoothCameraMove(Vector3 targetPosition, Quaternion targetRotation)
    {
        if (xrOriginTransform != null)
        {
            Vector3 cameraOffset = Camera.main.transform.position - xrOriginTransform.position;
            Quaternion rotationOffset = Quaternion.Inverse(xrOriginTransform.rotation) * Camera.main.transform.rotation;

            Vector3 xrTargetPosition = targetPosition - cameraOffset;
            Quaternion xrTargetRotation = targetRotation * Quaternion.Inverse(rotationOffset);

            Vector3 startPosition = xrOriginTransform.position;
            Quaternion startRotation = xrOriginTransform.rotation;
            float elapsedTime = 0;
            //float duration = Vector3.Distance(startPosition, xrTargetPosition) / cameraMoveSpeed;
            //if (duration < 3f) duration = 0.05f;
            float distance = Vector3.Distance(startPosition, xrTargetPosition);
            float angle = Quaternion.Angle(startRotation, xrTargetRotation);

            // 同時考慮距離與角度，給轉場自然緩衝
            float duration = (distance / cameraMoveSpeed) + (angle / 180f) * 0.8f;
            if (duration < 2.5f) duration = 6.5f;


            while (elapsedTime < duration)
            {
                xrOriginTransform.position = Vector3.Lerp(startPosition, xrTargetPosition, elapsedTime / duration);
                xrOriginTransform.rotation = Quaternion.Slerp(startRotation, xrTargetRotation, elapsedTime / duration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            xrOriginTransform.position = xrTargetPosition;
            xrOriginTransform.rotation = xrTargetRotation;
        }
        else
        {
            Transform movingTransform = Camera.main.transform;
            Vector3 startPosition = movingTransform.position;
            Quaternion startRotation = movingTransform.rotation;
            float elapsedTime = 0;
            float duration = Vector3.Distance(startPosition, targetPosition) / cameraMoveSpeed;
            // 確保移動至少持續 1.0 秒，以提供平滑感
            if (duration < 0.05f) duration = 10.0f;

            while (elapsedTime < duration)
            {
                movingTransform.position = Vector3.Lerp(startPosition, targetPosition, elapsedTime / duration);
                movingTransform.rotation = Quaternion.Slerp(startRotation, targetRotation, elapsedTime / duration);
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            movingTransform.position = targetPosition;
            movingTransform.rotation = targetRotation;
        }
        Debug.Log("攝影機平滑移動完成。");
    }

    IEnumerator WaitForAnswer(List<string> correctAnswers)
    {
        TMPro.TextMeshPro textDisplay = null;

        // >>> 【核心修正】檢查問題編號是否大於等於 2（即 Q2 和 Q3）
        if (currentVoiceQuestionIndex >= 2 && question3_VoiceText != null)
        {
            textDisplay = question3_VoiceText;
            // 確保主文字板在 Q2/Q3 是關閉的
            if (questionBroadcastTextMeshPro != null && questionBroadcastTextMeshPro.gameObject.activeSelf)
            {
                questionBroadcastTextMeshPro.gameObject.SetActive(false);
            }
        }
        else
        {
            // Q1 (currentVoiceQuestionIndex == 1) 使用主文字板
            textDisplay = questionBroadcastTextMeshPro;
        }

        if (textDisplay != null)
        {
            // 確保選定的文字板是啟用的
            textDisplay.gameObject.SetActive(true);
        }
        // <<< 核心修正區域結束

        if (Microphone.devices.Length > 0)
        {
            Debug.Log("開始錄音...");
            if (textDisplay != null)
            {
                // 此時文字板應該已經啟用，顯示 "請說出答案"
                textDisplay.text = "請說出答案";
            }

            recordingClip = Microphone.Start(null, false, (int)recordingDuration, 44100);
            yield return new WaitForSeconds(recordingDuration);
            Microphone.End(null);

            Debug.Log("錄音結束。");

            if (textDisplay != null)
            {
                textDisplay.text = "錄音已結束"; // 顯示 "錄音已結束"
            }

            byte[] wavData = ConvertAudioClipToWav(recordingClip);

            string testId = FirebaseManager_Firestore.Instance.testId;
            string levelIndex = "5";
            var files = new Dictionary<string, byte[]>();
            string key = $"voice_{currentVoiceQuestionIndex}";
            files[key] = wavData;
            FirebaseManager_Firestore.Instance.UploadFilesAndSaveUrls(testId, levelIndex, files);

            SaveWavFile(wavData, currentVoiceQuestionIndex);
            yield return StartCoroutine(SendAudioToServer(wavData, correctAnswers));
        }
        else
        {
            Debug.LogError("沒有找到麥克風設備！流程將跳過本次語音辨識。");
            if (textDisplay != null)
            {
                textDisplay.gameObject.SetActive(false);
            }
            yield return StartCoroutine(ShowResultAndContinue(false));
        }
    }

    IEnumerator SendAudioToServer(byte[] audioData, List<string> correctAnswers)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "recording.wav", "audio/wav");

        UnityWebRequest request = UnityWebRequest.Post(serverUrl, form);
        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = request.downloadHandler.text;
            Debug.Log("伺服器回應: " + jsonResponse);
            try
            {
                RecognitionResponse response = JsonUtility.FromJson<RecognitionResponse>(jsonResponse);
                CheckAnswer(response.transcription, correctAnswers);
            }
            catch (System.Exception ex)
            {
                Debug.LogError("解析 JSON 失敗: " + ex.Message);
            }
        }
        else
        {
            Debug.LogError("語音辨識請求失敗: " + request.error);
        }
    }

    int level5QuestionIndex = 0;
    Dictionary<string, string> level5correctOptions = new Dictionary<string, string>();
    Dictionary<string, string> level5playerOptions = new Dictionary<string, string>();

    void CheckAnswer(string userResponse, List<string> correctAnswers)
    {
        if (string.IsNullOrEmpty(userResponse))
        {
            Debug.Log("沒有聽到回答。");
            StartCoroutine(ShowResultAndContinue(false));
            return;
        }

        bool isCorrect = false;
        string normalizedResponse = userResponse.Trim().ToLower();

        foreach (string correctAnswer in correctAnswers)
        {
            string normalizedCorrectAnswer = correctAnswer.Trim().ToLower();
            if (normalizedResponse.Contains(normalizedCorrectAnswer))
            {
                isCorrect = true;
                break;
            }
        }

        if (isCorrect)
        {
            Debug.Log($"答案正確！你說了: \"{userResponse}\"");
        }
        else
        {
            Debug.Log($"答案錯誤。你說了: \"{userResponse}\"，正確答案是: \"{string.Join("/", correctAnswers)}\"");
        }

        string qKey = $"Q{level5QuestionIndex + 1}";
        level5correctOptions[qKey] = string.Join("/", correctAnswers);
        level5playerOptions[qKey] = userResponse;

        level5QuestionIndex++;

        string testId = FirebaseManager_Firestore.Instance.testId;
        string levelIndex = "5"; // 這一關的代號，請依場景改
        FirebaseManager_Firestore.Instance.SaveLevelOptions(testId, levelIndex, level5correctOptions, level5playerOptions);

        StartCoroutine(ShowResultAndContinue(isCorrect));
    }


    // ===============================================
    // ⭐ 新增：WAV 檔案儲存函式 (寫入 Assets/Scripts/game_4)
    // ===============================================
    private void SaveWavFile(byte[] wavData, int questionNumber)
    {
        string relativePath = "Scripts/game_4";
        string directoryPath = Path.Combine(Application.dataPath, relativePath);

        // 建立資料夾
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
            Debug.Log($"已建立資料夾: {directoryPath}");
        }

        // 檔案命名：時間戳記 + Q1/Q2/Q3... (取自 currentVoiceQuestionIndex)
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        string fileName = $"{timestamp}_game4_Q{questionNumber}.wav"; 
        string filePath = Path.Combine(directoryPath, fileName);

        try
        {
            File.WriteAllBytes(filePath, wavData);

#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif

            Debug.Log($"✅ 語音檔案儲存成功 (Assets/Scripts/game_4): {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 語音檔案儲存失敗。錯誤訊息: {e.Message}");
        }
    }

    IEnumerator ShowResultAndContinue(bool isCorrect)
    {
        if (isCorrect)
        {
            voiceCorrectAnswersCount++;
        }


        yield return new WaitForSeconds(timeBetweenQuestions);

        // >>> 【修正】如果不是在處理最後一題（Q3），則不要自動隱藏文字板，讓主流程控制。
        // 隱藏對應的文字板 (只有在 Q3 結束時，或是 Q1 結束時才需要隱藏)
        if (currentVoiceQuestionIndex == 3 && question3_VoiceText != null) // Q3 結束
        {
            question3_VoiceText.gameObject.SetActive(false);
        }
        else if (currentVoiceQuestionIndex == 1 && questionBroadcastTextMeshPro != null) // Q1 結束
        {
            questionBroadcastTextMeshPro.gameObject.SetActive(false);
        }
        // <<<
    }

    byte[] ConvertAudioClipToWav(AudioClip clip)
    {
        if (clip == null) return new byte[0];

        const int headerSize = 44;
        byte[] bytes = new byte[clip.samples * 2 * clip.channels + headerSize];
        int format = 1;
        int channels = clip.channels;
        int sampleRate = clip.frequency;
        int bitDepth = 16;
        int byteRate = sampleRate * channels * (bitDepth / 8);
        int blockAlign = channels * (bitDepth / 8);

        System.Text.Encoding.UTF8.GetBytes("RIFF").CopyTo(bytes, 0);
        System.BitConverter.GetBytes(bytes.Length - 8).CopyTo(bytes, 4);
        System.Text.Encoding.UTF8.GetBytes("WAVE").CopyTo(bytes, 8);
        System.Text.Encoding.UTF8.GetBytes("fmt ").CopyTo(bytes, 12);
        System.BitConverter.GetBytes(16).CopyTo(bytes, 16);
        System.BitConverter.GetBytes((short)format).CopyTo(bytes, 20);
        System.BitConverter.GetBytes((short)channels).CopyTo(bytes, 22);
        System.BitConverter.GetBytes(sampleRate).CopyTo(bytes, 24);
        System.BitConverter.GetBytes(byteRate).CopyTo(bytes, 28);
        System.BitConverter.GetBytes((short)blockAlign).CopyTo(bytes, 32);
        System.BitConverter.GetBytes((short)bitDepth).CopyTo(bytes, 34);
        System.Text.Encoding.UTF8.GetBytes("data").CopyTo(bytes, 36);
        System.BitConverter.GetBytes(clip.samples * blockAlign).CopyTo(bytes, 40);

        float[] samples = new float[clip.samples * clip.channels];
        clip.GetData(samples, 0);

        for (int i = 0; i < samples.Length; i++)
        {
            short pcmValue = (short)(samples[i] * short.MaxValue);
            System.BitConverter.GetBytes(pcmValue).CopyTo(bytes, headerSize + i * 2);
        }

        return bytes;
    }

    private void SetupCameraMode()
    {
        bool isVRMode = (xrOriginTransform != null);

        if (!isVRMode && vrCameraTarget_FishStall != null)
        {
            Camera.main.transform.position = vrCameraTarget_FishStall.position;
            Camera.main.transform.rotation = vrCameraTarget_FishStall.rotation;
            Debug.Log("已自動將 Main Camera 的位置設為 VR 模式的目標點。");
        }
    }

    [System.Serializable]
    public class RecognitionResponse
    {
        public string transcription;
        public float confidence;
    }
}