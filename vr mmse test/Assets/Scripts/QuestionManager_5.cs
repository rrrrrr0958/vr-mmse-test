using UnityEngine;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Networking;
using System.Text.RegularExpressions;
using Firebase;
using Firebase.Database;
using System.Threading.Tasks;
using System.IO; // 引入 System.IO 以處理檔案儲存
using System;    // 引入 System.DateTime 以處理時間戳記

public class QuestionManager : MonoBehaviour
{
    private FirebaseManager_Firestore FirebaseManager;

    public TextMeshPro questionText;
    public GameObject panelBackground;
    public float delayBetweenQuestions = 3.5f;

    public AudioSource questionAudioSource;

    private string initialMoneyQuestion = "現在你有100元";
    public AudioClip initialMoneyAudio;

    [System.Serializable]
    public class QuestionData
    {
        public string questionText;
        public AudioClip audioClip;
        public AudioClip nextAudioClip;
        public GameObject cameraTarget;
        public GameObject vrCameraTarget;
        public GameObject numberObject;
        public GameObject bgObject;
        public GameObject recordingObject;

        [Header("倒數計時 UI (GameObject)")]
        public GameObject countdownGameObject;
    }

    [Header("所有題目資料")]
    public List<QuestionData> allQuestions;

    private List<QuestionData> currentQuestionSequence = new List<QuestionData>();

    private int currentMoney = 100;

    private int correctAnswerCount = 0;

    [Header("伺服器設定")]
    public string serverUrl = "http://localhost:5000/recognize_speech";
    public float recordingDuration = 3.5f; // 錄音長度

    private AudioClip recordingClip;

    // 用於確保倒數計時的整數更新
    private int lastTimeLeft = 0;

    [System.Serializable]
    public class RecognitionResponse
    {
        public string transcription;
    }

    [Header("攝影機設定")]
    public Camera mainCamera;
    public Transform initialCameraPosition;
    public GameObject moneyNumber5;
    public GameObject moneyBg5;

    [Header("攝影機設定")]
    public float cameraMoveDuration = 7.0f;

    // VR 相關修正：新增 XR Origin 的引用
    [Header("VR 攝影機設定")]
    public Transform xrOriginTransform;
    public Camera hmdCamera;
    public Transform initialSpawnPoint;


    void Start()
    {
        if (hmdCamera == null || xrOriginTransform == null || initialSpawnPoint == null)
        {
            Debug.LogError("請確認 hmdCamera、xrOriginTransform、initialSpawnPoint 都已經設定！");
            return;
        }

        // 初始化 VR 攝影機位置和方向
        Vector3 offset = initialSpawnPoint.position - hmdCamera.transform.position;
        xrOriginTransform.position += offset;

        Vector3 camForward = Vector3.ProjectOnPlane(hmdCamera.transform.forward, Vector3.up).normalized;
        Vector3 tgtForward = Vector3.ProjectOnPlane(initialSpawnPoint.forward, Vector3.up).normalized;
        float yawDelta = Vector3.SignedAngle(camForward, tgtForward, Vector3.up);
        xrOriginTransform.Rotate(Vector3.up, yawDelta, Space.World);


        if (questionText == null || panelBackground == null || questionAudioSource == null ||
            initialMoneyAudio == null || mainCamera == null || initialCameraPosition == null ||
            moneyNumber5 == null || moneyBg5 == null || allQuestions.Count < 3 ||
            xrOriginTransform == null)
        {
            Debug.LogError("請確保所有公開變數都已在 Unity Inspector 中設定！");
            return;
        }


        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task =>
        {
            Firebase.DependencyStatus dependencyStatus = task.Result;
            if (dependencyStatus == Firebase.DependencyStatus.Available)
            {
                Debug.Log("Firebase 依賴關係檢查成功。");
            }
            else
            {
                Debug.LogError(string.Format(
                    "無法解決 Firebase 依賴關係: {0}", dependencyStatus));
            }
        });

        panelBackground.SetActive(false);
        moneyNumber5.SetActive(false);
        moneyBg5.SetActive(false);
        HideAllQuestionObjects();
        HideAllRecordingObjects();
        // 初始化所有倒數 UI 為隱藏
        HideAllCountdownObjects();

        StartCoroutine(StartGameSequence());
    }

    // 隱藏所有倒數物件的函式
    void HideAllCountdownObjects()
    {
        foreach (var q in allQuestions)
        {
            if (q.countdownGameObject != null) q.countdownGameObject.SetActive(false);
        }
    }


    void HideAllQuestionObjects()
    {
        foreach (var q in allQuestions)
        {
            if (q.numberObject != null) q.numberObject.SetActive(false);
            if (q.bgObject != null) q.bgObject.SetActive(false);
        }
    }

    void HideAllRecordingObjects()
    {
        foreach (var q in allQuestions)
        {
            if (q.recordingObject != null) q.recordingObject.SetActive(false);
        }
    }

    IEnumerator StartGameSequence()
    {
        GenerateRandomQuestions();
        currentMoney = 100;
        correctAnswerCount = 0;
        panelBackground.SetActive(true);


        questionText.text = initialMoneyQuestion;
        Debug.Log("顯示題目: " + initialMoneyQuestion);

        if (initialMoneyAudio != null)
        {
            questionAudioSource.clip = initialMoneyAudio;
            questionAudioSource.Play();

            moneyNumber5.SetActive(true);
            moneyBg5.SetActive(true);

            yield return new WaitForSeconds(initialMoneyAudio.length);

            moneyNumber5.SetActive(false);
            moneyBg5.SetActive(false);
            yield return new WaitForSeconds(delayBetweenQuestions);
        }
        else
        {
            yield return new WaitForSeconds(delayBetweenQuestions);
        }

        for (int i = 0; i < currentQuestionSequence.Count; i++)
        {
            QuestionData currentQuestionData = currentQuestionSequence[i];
            string currentQuestionText = currentQuestionData.questionText;
            AudioClip currentAudioClip = null;

            Transform targetTransform = (xrOriginTransform != null && currentQuestionData.vrCameraTarget != null) ?
                currentQuestionData.vrCameraTarget.transform :
                currentQuestionData.cameraTarget.transform;

            if (targetTransform != null)
            {
                yield return StartCoroutine(MoveCameraToTarget(targetTransform));
            }

            // 根據題號 i 來決定要使用哪種版本的語音
            if (i == 0)
            {
                currentAudioClip = currentQuestionData.audioClip;
            }
            else
            {
                currentAudioClip = currentQuestionData.nextAudioClip;
                currentQuestionText = "接下來再" + currentQuestionText;
            }

            questionText.text = currentQuestionText;
            Debug.Log("顯示題目: " + currentQuestionText);

            if (currentAudioClip != null)
            {
                questionAudioSource.clip = currentAudioClip;
                questionAudioSource.Play();
                currentQuestionData.numberObject.SetActive(true);
                currentQuestionData.bgObject.SetActive(true);

                yield return new WaitForSeconds(currentAudioClip.length);
                currentQuestionData.numberObject.SetActive(false);
                currentQuestionData.bgObject.SetActive(false);
            }
            else
            {
                Debug.LogWarning($"第 {i + 1} 題沒有設定 {(i == 0 ? "audioClip" : "nextAudioClip")}，將等待 {delayBetweenQuestions} 秒。");
                yield return new WaitForSeconds(delayBetweenQuestions);
            }

            // 將當前題目的索引 i 傳入 WaitForAnswer
            yield return StartCoroutine(WaitForAnswer(currentQuestionData, i));
        }

        Debug.Log("所有題目已顯示完畢！");
        questionText.text = "商品購買完畢！";

        StartCoroutine(SaveCorrectAnswersToFirebaseCoroutine());
        // 假設 SceneFlowManager.instance.LoadNextScene() 存在且運作正常
        SceneFlowManager.instance.LoadNextScene(); 
        Debug.Log("✅ 流程結束，準備載入下一個場景。");
    }

    IEnumerator MoveCameraToTarget(Transform target)
    {
        if (xrOriginTransform == null)
        {
            Debug.LogError("XR Origin Transform is not assigned!");
            yield break;
        }

        float startTime = Time.time;
        Vector3 startPosition = xrOriginTransform.position;
        Quaternion startRotation = xrOriginTransform.rotation;
        float elapsedTime = 0f;

        while (elapsedTime < cameraMoveDuration)
        {
            float fractionOfJourney = elapsedTime / cameraMoveDuration;

            xrOriginTransform.position = Vector3.Lerp(startPosition, target.position, fractionOfJourney);
            xrOriginTransform.rotation = Quaternion.Lerp(startRotation, target.rotation, fractionOfJourney);

            elapsedTime += Time.deltaTime;

            yield return null;
        }

        xrOriginTransform.position = target.position;
        xrOriginTransform.rotation = target.rotation;
    }

    void GenerateRandomQuestions()
    {
        currentQuestionSequence = allQuestions.OrderBy(x => System.Guid.NewGuid()).ToList();
    }

    // ===============================================
    // 倒數計時協程 (只負責文字更新)
    // ===============================================
    IEnumerator CountdownCoroutine(TextMeshPro countdownDisplay, float duration)
    {
        lastTimeLeft = (int)Mathf.Ceil(duration);
        float startTime = Time.time;

        // 初始化顯示為整數
        if (countdownDisplay != null) countdownDisplay.text = lastTimeLeft.ToString();

        while (Time.time < startTime + duration)
        {
            // 使用無條件進位確保倒數為整數 (5 -> 4 -> 3...)
            int timeLeft = (int)Mathf.Ceil((startTime + duration) - Time.time);

            if (timeLeft != lastTimeLeft && timeLeft >= 0)
            {
                if (countdownDisplay != null) countdownDisplay.text = timeLeft.ToString();
                lastTimeLeft = timeLeft;
            }
            yield return null;
        }

        // 倒數結束，確保最後顯示 0 
        if (countdownDisplay != null)
        {
            countdownDisplay.text = "0";
            yield return new WaitForSeconds(0.1f);
        }
    }

    // ===============================================
    // WaitForAnswer 整合倒數計時、子物件尋找和檔案儲存
    // ===============================================
    IEnumerator WaitForAnswer(QuestionData currentQuestionData, int questionIndex) // 接收 questionIndex
    {
        if (currentQuestionData.recordingObject != null)
        {
            currentQuestionData.recordingObject.SetActive(true);
        }

        Debug.Log("請說出你的答案...");
        questionText.text = "請說出你的答案...";

        // 1. 嘗試從 GameObject 及其子物件中獲取 TextMeshPro
        TextMeshPro countdownUI = null;
        if (currentQuestionData.countdownGameObject != null)
        {
            countdownUI = currentQuestionData.countdownGameObject.GetComponentInChildren<TextMeshPro>();

            if (countdownUI == null)
            {
                Debug.LogError($"⚠️ 題目 {currentQuestionData.questionText} 的倒數物件 {currentQuestionData.countdownGameObject.name} 及其子物件中缺少 TextMeshPro 元件！請檢查設定。");
            }
            else
            {
                // 啟用父物件，讓倒數 UI 顯示出來
                currentQuestionData.countdownGameObject.SetActive(true);
            }
        }

        // 2. 開始錄音和倒數
        if (Microphone.devices.Length > 0)
        {
            Debug.Log("開始錄音...");

            // 啟動倒數計時協程
            Coroutine countdownJob = null;
            if (countdownUI != null)
            {
                countdownJob = StartCoroutine(CountdownCoroutine(countdownUI, recordingDuration));
            }

            // 開始錄音
            recordingClip = Microphone.Start(null, false, (int)recordingDuration, 44100);

            // 等待錄音時間結束
            yield return new WaitForSeconds(recordingDuration);

            // 停止倒數協程
            if (countdownJob != null)
            {
                StopCoroutine(countdownJob);
            }

            Microphone.End(null);
            Debug.Log("錄音結束。");

            // 隱藏錄音提示物件
            if (currentQuestionData.recordingObject != null)
            {
                currentQuestionData.recordingObject.SetActive(false);
            }
            // 隱藏倒數的父物件
            if (currentQuestionData.countdownGameObject != null)
            {
                currentQuestionData.countdownGameObject.SetActive(false);
            }

            // 3. 語音處理 (儲存檔案和送去辨識)
            byte[] wavData = ConvertAudioClipToWav(recordingClip);

            string testId = FirebaseManager_Firestore.Instance.testId;
            string levelIndex = "7";
            
            string fileName = $"減法運算_Q{questionIndex + 1}_wavData.wav";
            var files = new Dictionary<string, byte[]> { { fileName, wavData } };
            FirebaseManager_Firestore.Instance.UploadFilesAndSaveUrls(testId, levelIndex, files);

            // ⭐ 呼叫新的存檔函式，使用相對路徑
            SaveWavFile(wavData, questionIndex + 1); // 題號從 1 開始

            // 繼續語音辨識流程
            yield return StartCoroutine(SendAudioToServer(wavData, currentQuestionSequence.IndexOf(currentQuestionData)));
        }
        else
        {
            // 沒有麥克風設備的錯誤處理
            Debug.LogError("沒有找到麥克風設備！");
            questionText.text = "沒有找到麥克風設備！";

            // 隱藏相關提示物件
            if (currentQuestionData.recordingObject != null)
            {
                currentQuestionData.recordingObject.SetActive(false);
            }

            // 確保倒數 UI 被隱藏
            if (currentQuestionData.countdownGameObject != null)
            {
                currentQuestionData.countdownGameObject.SetActive(false);
            }

            UpdateMoneyAndCheckAnswer(string.Empty, currentQuestionSequence.IndexOf(currentQuestionData));
            yield return new WaitForSeconds(2.0f);
        }
    }

    // 修正：WAV 檔案儲存函式 (使用相對路徑)
    private void SaveWavFile(byte[] wavData, int questionNumber)
    {
        // 1. 構建目標路徑
        string relativePath = "Scripts/game_5";
        string directoryPath = Path.Combine(Application.dataPath, relativePath);

        // 2. 建立資料夾
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
            Debug.Log($"已建立資料夾: {directoryPath}");
        }

        // 3. 檔案命名 (時間戳記 + 題號)
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmm");
        string fileName = $"{timestamp}_game5_Q{questionNumber}.wav";
        string filePath = Path.Combine(directoryPath, fileName);

        // 4. 寫入檔案
        try
        {
            File.WriteAllBytes(filePath, wavData);

            // 僅在 Editor 環境下，強制 Unity 刷新 Asset Database，讓新檔案即時出現在 Project 面板
#if UNITY_EDITOR
        UnityEditor.AssetDatabase.Refresh();
#endif

            Debug.Log($"✅ 語音檔案儲存成功 (Assets/Scripts/game_5): {filePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"❌ 語音檔案儲存失敗。請檢查 {directoryPath} 是否存在且有寫入權限。錯誤訊息: {e.Message}");
        }
    }


    IEnumerator SendAudioToServer(byte[] audioData, int questionSequenceIndex)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "recording.wav", "audio/wav");

        UnityWebRequest request = UnityWebRequest.Post(serverUrl, form);
        yield return request.SendWebRequest();

        string userResponse = string.Empty;

        if (request.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = request.downloadHandler.text;
            Debug.Log("伺服器回應: " + jsonResponse);

            try
            {
                RecognitionResponse response = JsonUtility.FromJson<RecognitionResponse>(jsonResponse);
                userResponse = response.transcription;
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

        UpdateMoneyAndCheckAnswer(userResponse, questionSequenceIndex);
    }

    int currentQuestionIndex = 0;
    Dictionary<string, string> correctOptions = new Dictionary<string, string>();
    Dictionary<string, string> playerOptions = new Dictionary<string, string>();

    void UpdateMoneyAndCheckAnswer(string userResponse, int questionSequenceIndex)
    {
        string question = currentQuestionSequence[questionSequenceIndex].questionText;
        Match match = Regex.Match(question, @"花費(\d+)元");
        int spentMoney = 0;
        if (match.Success)
        {
            spentMoney = int.Parse(match.Groups[1].Value);
        }

        int remainingMoney = currentMoney - spentMoney;
        currentMoney = remainingMoney;

        if (string.IsNullOrEmpty(userResponse))
        {
            Debug.Log("沒有聽到或辨識到回答，但金額已扣除。");
            return;
        }

        string remainingMoneyStr = remainingMoney.ToString();

        // ✅ 只保留數字（移除所有非數字字元）
        // 例如：「還剩下 80 元。」-> "80"
        string normalizedResponse = Regex.Replace(userResponse, @"\D", "");

        Debug.Log($"你說了(數字抽取後): \"{normalizedResponse}\"，正確答案應該是: \"{remainingMoneyStr}\""); //這邊錯誤&正確都要存到database

        if (normalizedResponse == remainingMoneyStr)
        {
            Debug.Log("答案正確！");
            correctAnswerCount++;
        }
        else
        {
            Debug.Log("答案錯誤！");
        }
        string qKey = $"Q{currentQuestionIndex + 1}";
        correctOptions[qKey] = string.Join("/", remainingMoneyStr);
        playerOptions[qKey] = normalizedResponse;

        currentQuestionIndex++;

        string testId = FirebaseManager_Firestore.Instance.testId;
        string levelIndex = "7"; // 這一關的代號，請依場景改
        FirebaseManager_Firestore.Instance.SaveLevelOptions(testId, levelIndex, correctOptions, playerOptions);
    }


    private IEnumerator SaveCorrectAnswersToFirebaseCoroutine()
    {
        Debug.Log("開始儲存答對題數到 Firebase...");
        string testId = FirebaseManager_Firestore.Instance.testId;
        string levelIndex = "7";
        FirebaseManager_Firestore.Instance.totalScore = FirebaseManager_Firestore.Instance.totalScore + correctAnswerCount;

        FirebaseManager_Firestore.Instance.SaveLevelData(testId, levelIndex, correctAnswerCount);
        Debug.Log("✅ 答對題數已送出至 Firebase。");
        yield break;
        // DatabaseReference reference = FirebaseDatabase.DefaultInstance.RootReference;
        // string json = JsonUtility.ToJson(new CorrectAnswerData(correctAnswerCount));

        // var task = reference.Child("caculate_5").SetRawJsonValueAsync(json);

        // while (!task.IsCompleted)
        // {
        //     yield return null;
        // }

        // if (task.IsCompleted)
        // {
        //     Debug.Log("答對題數已成功儲存到 Firebase。");
        // }
        // else if (task.IsFaulted)
        // {
        //     Debug.LogError("儲存資料到 Firebase 時發生錯誤: " + task.Exception);
        // }
    }

    [System.Serializable]
    public class CorrectAnswerData
    {
        public int count;
        public CorrectAnswerData(int count)
        {
            this.count = count;
        }
    }

    byte[] ConvertAudioClipToWav(AudioClip clip)
    {
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
}