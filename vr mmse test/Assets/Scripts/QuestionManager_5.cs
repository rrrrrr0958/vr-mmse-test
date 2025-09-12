using UnityEngine;
using TMPro; // 引入TextMeshPro命名空間
using System.Collections; // 引入用於協程的命名空間
using System.Collections.Generic; // 引入用於List的命名空間
using System.Linq; // 引入用於LINQ，方便隨機選擇
using UnityEngine.Networking; // 新增這行
using System.Text.RegularExpressions; // 用於正規表達式，提取數字
using Firebase; // 新增 Firebase 命名空間
using Firebase.Database; // 新增 Firebase Realtime Database 命名空間
using System.Threading.Tasks; // 用於非同步操作

public class QuestionManager : MonoBehaviour
{
    public TextMeshPro questionText; // 用於顯示文字的 TextMeshPro (3D)
    public GameObject panelBackground; // 問題背景面板的 GameObject (3D Quad 或 Plane)
    public float delayBetweenQuestions = 3.0f; // 每題之間的延遲時間

    public AudioSource questionAudioSource; // 用於播放題目語音的 AudioSource

    private string initialMoneyQuestion = "現在你有100元";
    public AudioClip initialMoneyAudio;

    // 新增：用於儲存所有題目相關資訊的結構
    [System.Serializable]
    public class QuestionData
    {
        public string questionText;
        public AudioClip audioClip;
        public GameObject cameraTarget;
        public GameObject numberObject;
        public GameObject bgObject;
    }

    [Header("所有題目資料")]
    public List<QuestionData> allQuestions;

    // 儲存隨機選擇的三個題目序列
    private List<QuestionData> currentQuestionSequence = new List<QuestionData>();

    // 追蹤目前金額
    private int currentMoney = 100;

    // 新增：追蹤答對題數的變數
    private int correctAnswerCount = 0;

    [Header("伺服器設定")]
    public string serverUrl = "http://localhost:5000/recognize_speech";
    public float recordingDuration = 3.0f;

    private AudioClip recordingClip;

    [System.Serializable]
    public class RecognitionResponse
    {
        public string transcription;
    }

    // 新增：主攝影機和初始攝影機位置
    [Header("攝影機設定")]
    public Camera mainCamera;
    public Transform initialCameraPosition;
    public GameObject moneyNumber5;
    public GameObject moneyBg5;

    // 攝影機轉向參數
    public float cameraMoveSpeed = 2.0f;

    void Start()
    {
        // 檢查所有必要的物件是否已設定
        if (questionText == null || panelBackground == null || questionAudioSource == null ||
      initialMoneyAudio == null || mainCamera == null || initialCameraPosition == null ||
      moneyNumber5 == null || moneyBg5 == null || allQuestions.Count < 3)
        {
            Debug.LogError("請確保所有公開變數都已在 Unity Inspector 中設定！");
            return;
        }

        // Firebase 初始化 (保持不變)
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWith(task => {
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

        // 初始隱藏所有相關物件
        panelBackground.SetActive(false);
        moneyNumber5.SetActive(false);
        moneyBg5.SetActive(false);
        HideAllQuestionObjects();

        StartCoroutine(StartGameSequence());
    }

    // 隱藏所有題目相關的數字和背景物件
    void HideAllQuestionObjects()
    {
        foreach (var q in allQuestions)
        {
            q.numberObject.SetActive(false);
            q.bgObject.SetActive(false);
        }
    }

    IEnumerator StartGameSequence()
    {
        GenerateRandomQuestions();
        currentMoney = 100;
        correctAnswerCount = 0; // 重設答對題數
        panelBackground.SetActive(true);

        // 1. 處理固定題目（100元）
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

        // 2. 依序處理每個隨機題目
        for (int i = 0; i < currentQuestionSequence.Count; i++)
        {
            QuestionData currentQuestionData = currentQuestionSequence[i];
            string currentQuestionText = currentQuestionData.questionText;

            if (i > 0)
            {
                currentQuestionText = "再" + currentQuestionText;
            }

            // 攝影機轉向目標攤位
            if (currentQuestionData.cameraTarget != null)
            {
                yield return StartCoroutine(MoveCameraToTarget(currentQuestionData.cameraTarget.transform));
            }

            // 播放題目語音和顯示物件
            questionText.text = currentQuestionText;
            Debug.Log("顯示題目: " + currentQuestionText);

            if (currentQuestionData.audioClip != null)
            {
                questionAudioSource.clip = currentQuestionData.audioClip;
                questionAudioSource.Play();
                currentQuestionData.numberObject.SetActive(true);
                currentQuestionData.bgObject.SetActive(true);

                yield return new WaitForSeconds(currentQuestionData.audioClip.length);
                currentQuestionData.numberObject.SetActive(false);
                currentQuestionData.bgObject.SetActive(false);
                yield return new WaitForSeconds(delayBetweenQuestions);
            }
            else
            {
                yield return new WaitForSeconds(delayBetweenQuestions);
            }

            yield return StartCoroutine(WaitForAnswer(i));
        }

        Debug.Log("所有題目已顯示完畢！");
        questionText.text = "商品購買完畢！";
        yield return StartCoroutine(MoveCameraToTarget(initialCameraPosition));

        StartCoroutine(SaveCorrectAnswersToFirebaseCoroutine());
    }

    // 新增：平滑移動攝影機的協程
    IEnumerator MoveCameraToTarget(Transform target)
    {
        float startTime = Time.time;
        Vector3 startPosition = mainCamera.transform.position;
        Quaternion startRotation = mainCamera.transform.rotation;
        float journeyLength = Vector3.Distance(startPosition, target.position);

        while (mainCamera.transform.position != target.position || mainCamera.transform.rotation != target.rotation)
        {
            float distCovered = (Time.time - startTime) * cameraMoveSpeed;
            float fractionOfJourney = journeyLength > 0 ? distCovered / journeyLength : 1f;

            mainCamera.transform.position = Vector3.Lerp(startPosition, target.position, fractionOfJourney);
            mainCamera.transform.rotation = Quaternion.Lerp(startRotation, target.rotation, fractionOfJourney);

            if (fractionOfJourney >= 1.0f) break;

            yield return null;
        }
    }

    void GenerateRandomQuestions()
    {
        // 隨機選取三個題目
        currentQuestionSequence = allQuestions.OrderBy(x => System.Guid.NewGuid()).Take(3).ToList();
    }

    IEnumerator WaitForAnswer(int questionSequenceIndex)
    {
        // 後續程式碼保持不變...
        Debug.Log("請說出你的答案...");
        questionText.text = "請說出你的答案...";

        if (Microphone.devices.Length > 0)
        {
            Debug.Log("開始錄音...");
            recordingClip = Microphone.Start(null, false, (int)recordingDuration, 44100);
            yield return new WaitForSeconds(recordingDuration);
            Microphone.End(null);
            Debug.Log("錄音結束。");

            byte[] wavData = ConvertAudioClipToWav(recordingClip);
            yield return StartCoroutine(SendAudioToServer(wavData, questionSequenceIndex));
        }
        else
        {
            Debug.LogError("沒有找到麥克風設備！");
            questionText.text = "沒有找到麥克風設備！";
            UpdateMoneyAndCheckAnswer(string.Empty, questionSequenceIndex);
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