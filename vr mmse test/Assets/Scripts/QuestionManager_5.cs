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

public class QuestionManager : MonoBehaviour
{
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
        public AudioClip audioClip; // 🔹 這是第一題/起始的語音 (e.g., "花費XX元...")
        public AudioClip nextAudioClip; // 🔹 【新增】這是第二/三題的語音 (e.g., "接下來再花費XX元...")
        public GameObject cameraTarget;
        public GameObject vrCameraTarget;
        public GameObject numberObject;
        public GameObject bgObject;
        public GameObject recordingObject;
    }

    [Header("所有題目資料")]
    public List<QuestionData> allQuestions;

    private List<QuestionData> currentQuestionSequence = new List<QuestionData>();

    private int currentMoney = 100;

    private int correctAnswerCount = 0;

    [Header("伺服器設定")]
    public string serverUrl = "http://localhost:5000/recognize_speech";
    public float recordingDuration = 3.5f;

    private AudioClip recordingClip;

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

    //public float cameraMoveSpeed = 3.0f;

    // 新增這一行
    [Header("攝影機設定")]
    public float cameraMoveDuration = 7.0f; // 設定移動需花費 1.5 秒

    // VR 相關修正：新增 XR Origin 的引用
    [Header("VR 攝影機設定")]
    public Transform xrOriginTransform;
    public Camera hmdCamera;   // 新增：XR Origin 裡的相機
    public Transform initialSpawnPoint; // 新增：你希望玩家開始站的位置


    void Start()
    {
        if (hmdCamera == null || xrOriginTransform == null || initialSpawnPoint == null)
        {
            Debug.LogError("請確認 hmdCamera、xrOriginTransform、initialSpawnPoint 都已經設定！");
            return;
        }

        // 🔹 方法一：計算 offset，把頭顯拉到指定初始位置
        Vector3 offset = initialSpawnPoint.position - hmdCamera.transform.position;
        xrOriginTransform.position += offset;

        // 🔹 只對齊 Yaw，不硬調 pitch/roll（避免暈）
        Vector3 camForward = Vector3.ProjectOnPlane(hmdCamera.transform.forward, Vector3.up).normalized;
        Vector3 tgtForward = Vector3.ProjectOnPlane(initialSpawnPoint.forward, Vector3.up).normalized;
        float yawDelta = Vector3.SignedAngle(camForward, tgtForward, Vector3.up);
        xrOriginTransform.Rotate(Vector3.up, yawDelta, Space.World);


        if (questionText == null || panelBackground == null || questionAudioSource == null ||
            initialMoneyAudio == null || mainCamera == null || initialCameraPosition == null ||
            moneyNumber5 == null || moneyBg5 == null || allQuestions.Count < 3 ||
            xrOriginTransform == null) // 新增：檢查 xrOriginTransform
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

        StartCoroutine(StartGameSequence());
        SceneFlowManager.instance.StartServerForScene("SampleScene_5");
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
            AudioClip currentAudioClip = null; // 🔹 新增：用於儲存當前要播放的語音

            Transform targetTransform = (xrOriginTransform != null && currentQuestionData.vrCameraTarget != null) ?
                currentQuestionData.vrCameraTarget.transform :
                currentQuestionData.cameraTarget.transform;

            if (targetTransform != null)
            {
                yield return StartCoroutine(MoveCameraToTarget(targetTransform));
            }

            // 🔹 根據題號 i 來決定要使用哪種版本的語音
            if (i == 0)
            {
                // 第一題 (i=0): 使用原版語音和文字 (e.g., "花費XX元...")
                currentAudioClip = currentQuestionData.audioClip;
                // 不需要修改 currentQuestionText，它已經是 "花費XX元..."
            }
            else
            {
                // 第二題 (i=1) 和之後的題目 (i>0):
                // 1. 使用新增的 "接下來/再" 版語音
                currentAudioClip = currentQuestionData.nextAudioClip;

                // 2. 在文字上加上 "再"
                currentQuestionText = "接下來再" + currentQuestionText;
            }

            questionText.text = currentQuestionText;
            Debug.Log("顯示題目: " + currentQuestionText);

            if (currentAudioClip != null) // 🔹 改為判斷 currentAudioClip
            {
                questionAudioSource.clip = currentAudioClip; // 🔹 播放正確的語音
                questionAudioSource.Play();
                currentQuestionData.numberObject.SetActive(true);
                currentQuestionData.bgObject.SetActive(true);

                yield return new WaitForSeconds(currentAudioClip.length); // 🔹 等待正確語音的長度
                currentQuestionData.numberObject.SetActive(false);
                currentQuestionData.bgObject.SetActive(false);
            }
            else
            {
                // 🔹 處理沒有設定語音的情況，並發出警告
                Debug.LogWarning($"第 {i + 1} 題沒有設定 {(i == 0 ? "audioClip" : "nextAudioClip")}，將等待 {delayBetweenQuestions} 秒。");
                yield return new WaitForSeconds(delayBetweenQuestions);
            }

            yield return StartCoroutine(WaitForAnswer(currentQuestionData));
        }

        Debug.Log("所有題目已顯示完畢！");
        questionText.text = "商品購買完畢！";
        // 移除或註解掉這段程式碼
        // Transform endTarget = (xrOriginTransform != null && vrEndPosition != null) ? vrEndPosition : initialCameraPosition;
        // yield return StartCoroutine(MoveCameraToTarget(endTarget));

        StartCoroutine(SaveCorrectAnswersToFirebaseCoroutine());
        SceneFlowManager.instance.LoadNextScene();
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

        // 使用一個計時器來控制時間
        float elapsedTime = 0f;

        // 只要尚未達到預設的移動時間，就持續移動
        while (elapsedTime < cameraMoveDuration)
        {
            // fractionOfJourney 現在代表時間的進度 (0 到 1)
            float fractionOfJourney = elapsedTime / cameraMoveDuration;

            // 【可選平滑化】使用 Mathf.SmoothStep 讓開始和結束時移動更平滑，減少眩暈
            // float smoothStepProgress = Mathf.SmoothStep(0f, 1f, fractionOfJourney);

            xrOriginTransform.position = Vector3.Lerp(startPosition, target.position, fractionOfJourney);
            xrOriginTransform.rotation = Quaternion.Lerp(startRotation, target.rotation, fractionOfJourney);

            elapsedTime += Time.deltaTime; // 累積經過的時間

            yield return null;
        }

        // 確保最終精確到達目標點
        xrOriginTransform.position = target.position;
        xrOriginTransform.rotation = target.rotation;
    }

    void GenerateRandomQuestions()
    {
        currentQuestionSequence = allQuestions.OrderBy(x => System.Guid.NewGuid()).Take(3).ToList();
    }

    IEnumerator WaitForAnswer(QuestionData currentQuestionData)
    {
        if (currentQuestionData.recordingObject != null)
        {
            currentQuestionData.recordingObject.SetActive(true);
        }

        Debug.Log("請說出你的答案...");
        questionText.text = "請說出你的答案...";

        if (Microphone.devices.Length > 0)
        {
            Debug.Log("開始錄音...");
            recordingClip = Microphone.Start(null, false, (int)recordingDuration, 44100);
            yield return new WaitForSeconds(recordingDuration);
            Microphone.End(null);
            Debug.Log("錄音結束。");

            if (currentQuestionData.recordingObject != null)
            {
                currentQuestionData.recordingObject.SetActive(false);
            }

            byte[] wavData = ConvertAudioClipToWav(recordingClip);
            yield return StartCoroutine(SendAudioToServer(wavData, currentQuestionSequence.IndexOf(currentQuestionData)));
        }
        else
        {
            Debug.LogError("沒有找到麥克風設備！");
            questionText.text = "沒有找到麥克風設備！";

            if (currentQuestionData.recordingObject != null)
            {
                currentQuestionData.recordingObject.SetActive(false);
            }

            UpdateMoneyAndCheckAnswer(string.Empty, currentQuestionSequence.IndexOf(currentQuestionData));
            yield return new WaitForSeconds(2.0f);
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
        string normalizedResponse = userResponse.Replace("。", "").Replace("元", "").Trim();

        Debug.Log($"你說了: \"{normalizedResponse}\"，正確答案應該是: \"{remainingMoneyStr}\"");

        if (normalizedResponse == remainingMoneyStr)
        {
            Debug.Log("答案正確！");
            correctAnswerCount++;
        }
        else
        {
            Debug.Log("答案錯誤！");
        }
    }

    private IEnumerator SaveCorrectAnswersToFirebaseCoroutine()
    {
        Debug.Log("開始儲存答對題數到 Firebase...");
        DatabaseReference reference = FirebaseDatabase.DefaultInstance.RootReference;
        string json = JsonUtility.ToJson(new CorrectAnswerData(correctAnswerCount));

        var task = reference.Child("caculate_5").SetRawJsonValueAsync(json);

        while (!task.IsCompleted)
        {
            yield return null;
        }

        if (task.IsCompleted)
        {
            Debug.Log("答對題數已成功儲存到 Firebase。");
        }
        else if (task.IsFaulted)
        {
            Debug.LogError("儲存資料到 Firebase 時發生錯誤: " + task.Exception);
        }
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