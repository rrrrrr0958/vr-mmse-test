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

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Controls;
#endif


public class GameManager : MonoBehaviour
{
    // =========================================================================
    // 公開變數 (在 Unity Inspector 中設定)
    // =========================================================================

    [Header("遊戲開始設定")]
    public float initialTextDelay = 3f;
    public float questionBroadcastDelay = 2f;
    public float timeBetweenQuestions = 2f;
    [Tooltip("語音問題結束後，到開始錄音前的緩衝時間。調低這個值可以更快開始錄音。")]
    public float voiceQuestionBufferTime = 0;

    [Header("點擊題設定")]
    public float clickResponseDuration = 3.0f;

    [Header("攝影機目標點")]
    public Transform cameraTarget_FishStall;

    [Header("攝影機移動設定")]
    public float cameraMoveSpeed = 50.0f;

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

    [Header("魚攤問題語音設定")]
    public AudioClip whatIsSellingAudioClip;
    public AudioClip fishColorAudioClip;
    public AudioClip whatIsThatAudioClip;

    [Header("語音辨識設定")]
    public string serverUrl = "http://localhost:5000/recognize_speech";
    public float recordingDuration = 5.0f;

    // ===== 新增：VR 控制器 & Ray 設定 =====
    [Header("VR 控制器設定（方案A）")]
    public Transform rightController;
    public float vrRayLength = 50f;
    public bool useOVRInput = true;
    public bool useNewInputSystem = false;

    [Header("點擊圖層")]
    public LayerMask stallLayerMask;

#if ENABLE_INPUT_SYSTEM
    [Header("XR Input Actions")]
    public InputActionProperty rightSelectAction;
#endif

    [Header("Ray Origin（可選，沒設就用 Right Controller）")]
    public Transform rayOriginOverride;


    // =========================================================================
    // 私有變數 (腳本內部使用)
    // =========================================================================

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

#if ENABLE_INPUT_SYSTEM
    private bool lastTriggerPressed = false;
#endif


    // =========================================================================
    // Unity 生命周期方法
    // =========================================================================

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
        if (questionBroadcastTextMeshPro != null)
        {
            questionBroadcastTextMeshPro.gameObject.SetActive(false);
        }
        StartCoroutine(ShowTextsAfterDelay());
        initialQuestionCoroutine = StartCoroutine(MainClickSequence());
    }

    void Update()
    {
        Ray hoverRay;
        Transform originT = rayOriginOverride != null ? rayOriginOverride : rightController;

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null && UnityEngine.InputSystem.Mouse.current.position != null)
        {
            Vector2 pos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            hoverRay = Camera.main.ScreenPointToRay(pos);
        }
        else if (originT != null)
        {
            hoverRay = new Ray(originT.position, originT.forward);
        }
        else
        {
            return;
        }
#else
        hoverRay = Camera.main.ScreenPointToRay(Input.mousePosition);
#endif

        RaycastHit hoverHit;
        if (Physics.Raycast(hoverRay.origin, hoverRay.direction, out hoverHit, vrRayLength, stallLayerMask.value))
        {
            if (hoverHit.collider.gameObject == clickedStallObject)
            {
                return;
            }

            if (hoverHit.collider.gameObject != currentHoveredObject)
            {
                if (currentHoveredObject != null)
                {
                    currentHoveredObject.GetComponent<Renderer>().material.color = originalHoverColor;
                }

                currentHoveredObject = hoverHit.collider.gameObject;
                originalHoverColor = currentHoveredObject.GetComponent<Renderer>().material.color;

                Color darkColor = originalHoverColor * 0.7f;
                currentHoveredObject.GetComponent<Renderer>().material.color = darkColor;
            }
        }
        else
        {
            if (currentHoveredObject != null)
            {
                currentHoveredObject.GetComponent<Renderer>().material.color = originalHoverColor;
                currentHoveredObject = null;
            }
        }

#if ENABLE_INPUT_SYSTEM
        if (UnityEngine.InputSystem.Mouse.current != null &&
            UnityEngine.InputSystem.Mouse.current.leftButton.wasPressedThisFrame)
        {
            Vector2 pos = UnityEngine.InputSystem.Mouse.current.position.ReadValue();
            Ray ray = Camera.main.ScreenPointToRay(pos);
            HandleClickRaycast(ray.origin, ray.direction);
        }
#else
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            HandleClickRaycast(ray.origin, ray.direction);
        }
#endif

        if (!isWaitingForClickInput || hasClickedStall || string.IsNullOrEmpty(currentTargetStallName))
            return;

#if ENABLE_INPUT_SYSTEM
        if (originT != null)
        {
            var xrRight = UnityEngine.InputSystem.XR.XRController.rightHand;
            if (xrRight != null)
            {
                var trigger = xrRight.TryGetChildControl<UnityEngine.InputSystem.Controls.AxisControl>("trigger");
                var aButton = xrRight.TryGetChildControl<UnityEngine.InputSystem.Controls.ButtonControl>("primaryButton");

                bool pressed = false;

                if (trigger != null)
                {
                    float v = trigger.ReadValue();
                    bool nowPressed = v > 0.5f;
                    if (nowPressed && !lastTriggerPressed) pressed = true;
                    lastTriggerPressed = nowPressed;
                }

                if (aButton != null && aButton.wasPressedThisFrame)
                    pressed = true;

                if (pressed)
                {
                    TryRaycastHit(originT.position, originT.forward);
                }
            }
        }
#endif
    }

    void HandleClickRaycast(Vector3 origin, Vector3 direction)
    {
        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, vrRayLength, stallLayerMask.value))
        {
            if (hit.collider != null && hit.collider.CompareTag("ClickableStall"))
            {
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
                    if (clickedStallName == currentTargetStallName)
                    {
                        Debug.Log($"✅ 正確！點擊了目標攤位: {currentTargetStallName}。");
                        correctAnswersCount++;

                        clickedStallObject = hit.collider.gameObject;
                        if (clickedStallObject != null)
                        {
                            if (currentHoveredObject != null)
                            {
                                currentHoveredObject.GetComponent<Renderer>().material.color = originalColors[currentHoveredObject];
                                currentHoveredObject = null;
                            }

                            Color newColor = originalColors[clickedStallObject] * 0.5f;
                            clickedStallObject.GetComponent<Renderer>().material.color = newColor;
                        }

                    }
                    else
                    {
                        Debug.LogWarning($"❌ 錯誤！你點擊了 {clickedStallName}，但正確答案是 {currentTargetStallName}。");
                    }
                    currentTargetStallName = "";
                }
                else
                {
                    Debug.Log("目前不在等待點擊的狀態，點擊無效。");
                }
            }
        }
        Debug.DrawRay(origin, direction.normalized * vrRayLength, Color.magenta, 0.25f);
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
        Debug.Log("所有攤位物件的顏色已重置。");
    }

    IEnumerator MainClickSequence()
    {
        yield return new WaitForSeconds(initialTextDelay + questionBroadcastDelay);

        List<string> tempNonFishStallNames = new List<string>(nonFishStallNames);

        correctAnswersCount = 0;

        for (int i = 0; i < 2; i++)
        {
            ResetAllStallColors();

            if (tempNonFishStallNames.Count == 0)
            {
                Debug.LogWarning("沒有足夠的非魚攤名稱可供隨機選擇！請確保至少有兩個非魚攤。");
                yield break;
            }

            int randomIndex = Random.Range(0, tempNonFishStallNames.Count);
            currentTargetStallName = tempNonFishStallNames[randomIndex];

            string initialQuestion = $"請點選 {currentTargetStallName} 攤位！";
            Debug.Log($"Console 問題 (第 {i + 1} 次): {initialQuestion}");
            PlayInitialVoiceQuestion(currentTargetStallName);

            tempNonFishStallNames.RemoveAt(randomIndex);

            isWaitingForClickInput = true;

            AudioClip currentClip = GetAudioClipForStall(currentTargetStallName);
            float totalWaitTime = (currentClip != null ? currentClip.length : 0f) + voiceQuestionBufferTime + clickResponseDuration;

            yield return new WaitForSeconds(totalWaitTime);

            currentTargetStallName = "";
            isWaitingForClickInput = false;

            yield return new WaitForSeconds(timeBetweenQuestions);
        }

        ResetAllStallColors();

        currentTargetStallName = "魚攤";
        string finalQuestion = $"請點選 {currentTargetStallName} 攤位！";
        Debug.Log($"Console 問題 (第 3 次，固定魚攤): {finalQuestion}");
        PlayInitialVoiceQuestion(currentTargetStallName);

        isWaitingForClickInput = true;

        AudioClip fishStallClip = GetAudioClipForStall("魚攤");
        float fishStallTotalWaitTime = (fishStallClip != null ? fishStallClip.length : 0f) + voiceQuestionBufferTime + clickResponseDuration;
        yield return new WaitForSeconds(fishStallTotalWaitTime);

        currentTargetStallName = "";
        isWaitingForClickInput = false;

        Debug.Log($"點擊題目正確數: {correctAnswersCount}/3");

        if (dbReference != null)
        {
            string userId = SystemInfo.deviceUniqueIdentifier;
            string timestamp = System.DateTime.Now.ToString("yyyyMMddHHmmss");
            string recordKey = $"{userId}_{timestamp}";

            Dictionary<string, object> scoreData = new Dictionary<string, object>();
            scoreData["Command_score"] = correctAnswersCount;
            scoreData["totalQuestions"] = 3;
            scoreData["timestamp"] = ServerValue.Timestamp;
            scoreData["userName"] = "PlayerName";

            dbReference.Child("scores").Child(recordKey).SetValueAsync(scoreData).ContinueWithOnMainThread(task =>
            {
                if (task.IsCompleted)
                {
                    Debug.Log($"成功將點擊分數寫入 Firebase: 正確 {correctAnswersCount}/3");
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
        yield return new WaitForSeconds(timeBetweenQuestions);

        voiceCorrectAnswersCount = 0;

        Debug.Log("Console 問題: 這個攤位在賣什麼？");
        yield return StartCoroutine(PlayAudioClipAndThenWait(whatIsSellingAudioClip));
        yield return StartCoroutine(WaitForAnswer(new List<string> { "魚", "魚肉", "魚攤", "肉", "海鮮", "魚肉攤", "一", "一肉", "一攤", "一肉攤", "露", "魚露", "一露", "魚露攤", "一露攤" }));

        Debug.Log("Console 問題: 魚的顏色是什麼？");
        yield return StartCoroutine(PlayAudioClipAndThenWait(fishColorAudioClip));
        yield return StartCoroutine(WaitForAnswer(new List<string> { "藍色", "藍", "藍白", "藍白色", "白藍", "白藍色", "淺藍", "淺藍色" }));

        Debug.Log("Console 問題: 那個是什麼？");
        yield return StartCoroutine(PlayAudioClipAndThenWait(whatIsThatAudioClip));
        ShowHighlightCircle();
        yield return StartCoroutine(WaitForAnswer(new List<string> { "燈", "路燈", "跟", "路跟", "膯", "路膯", "入燈", "入膯", "入跟" }));
        HideHighlightCircle();

        Debug.Log("Console: 所有魚攤問題已完成！");
        Debug.Log($"語音題目正確數: {voiceCorrectAnswersCount}/3");

        UploadVoiceScoreToFirebase(voiceCorrectAnswersCount);
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
                    Debug.Log($"成功將語音分數寫入 Firebase: 正確 {score}/3");
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

    void PlayInitialVoiceQuestion(string stallName)
    {
        Debug.Log($"嘗試播放語音給攤位: '{stallName}' (長度: {stallName.Length})");
        AudioClip clipToPlay = GetAudioClipForStall(stallName);
        PlayVoiceClip(clipToPlay, stallName);
    }

    private AudioClip GetAudioClipForStall(string stallName)
    {
        switch (stallName)
        {
            case "魚攤": return fishStallAudioClip;
            case "蔬果": return fruitStallAudioClip;
            case "武器": return weaponStallAudioClip;
            case "麵包": return breadStallAudioClip;
            case "肉攤": return meatStallAudioClip;
            default: return null;
        }
    }

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

    // =========================================================================
    // 語音辨識相關的核心函式
    // =========================================================================

    IEnumerator WaitForAnswer(List<string> correctAnswers)
    {
        if (questionBroadcastTextMeshPro != null)
        {
            questionBroadcastTextMeshPro.gameObject.SetActive(true);
        }

        if (Microphone.devices.Length > 0)
        {
            Debug.Log("開始錄音...");
            if (questionBroadcastTextMeshPro != null)
            {
                questionBroadcastTextMeshPro.text = "開始錄音";
            }

            recordingClip = Microphone.Start(null, false, (int)recordingDuration, 44100);
            yield return new WaitForSeconds(recordingDuration);
            Microphone.End(null);

            Debug.Log("錄音結束。");

            if (questionBroadcastTextMeshPro != null)
            {
                questionBroadcastTextMeshPro.text = "處理中";
            }

            byte[] wavData = ConvertAudioClipToWav(recordingClip);
            yield return StartCoroutine(SendAudioToServer(wavData, correctAnswers));
        }
        else
        {
            Debug.LogError("沒有找到麥克風設備！");
            if (questionBroadcastTextMeshPro != null)
            {
                questionBroadcastTextMeshPro.gameObject.SetActive(false);
            }
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

        StartCoroutine(ShowResultAndContinue(isCorrect));
    }

    IEnumerator ShowResultAndContinue(bool isCorrect)
    {
        if (isCorrect)
        {
            voiceCorrectAnswersCount++;
        }

        yield return new WaitForSeconds(timeBetweenQuestions);

        if (questionBroadcastTextMeshPro != null)
        {
            questionBroadcastTextMeshPro.gameObject.SetActive(false);
        }
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

    // =========================================================================
    // 方案A：共用 Raycast 判定（桌機/VR 都呼叫）
    // =========================================================================
    void TryRaycastHit(Vector3 origin, Vector3 direction)
    {
        RaycastHit hit;
        if (Physics.Raycast(origin, direction, out hit, vrRayLength, stallLayerMask.value))
        {
            if (hit.collider != null && hit.collider.CompareTag("ClickableStall"))
            {
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

                if (clickedStallName == currentTargetStallName)
                {
                    Debug.Log($"✅ 正確！點擊了目標攤位: {currentTargetStallName}。");
                    correctAnswersCount++;

                    clickedStallObject = hit.collider.gameObject;
                    if (clickedStallObject != null)
                    {
                        if (currentHoveredObject != null)
                        {
                            currentHoveredObject.GetComponent<Renderer>().material.color = originalColors[currentHoveredObject];
                            currentHoveredObject = null;
                        }

                        Color newColor = originalColors[clickedStallObject] * 0.5f;
                        clickedStallObject.GetComponent<Renderer>().material.color = newColor;
                    }
                }
                else
                {
                    Debug.LogWarning($"❌ 錯誤！你點擊了 {clickedStallName}，但正確答案是 {currentTargetStallName}。");
                }
                currentTargetStallName = "";
            }
        }
        Debug.DrawRay(origin, direction.normalized * vrRayLength, Color.cyan, 0.25f);
    }

    [System.Serializable]
    public class RecognitionResponse
    {
        public string transcription;
        public float confidence;
    }
}