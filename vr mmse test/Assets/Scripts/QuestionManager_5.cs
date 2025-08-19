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
    private List<string> answerOptions = new List<string>
    {
        "花費25元買了魚之後剩多少?",
        "花費7元買了麵包之後剩多少?",
        "花費35元買了水果之後剩多少?",
        "花費15元買了武器之後剩多少?",
        "花費30元買了肉之後剩多少?"
    };

    public AudioClip initialMoneyAudio;
    public List<AudioClip> answerOptionAudios;

    private List<int> currentQuestionSequenceIndices = new List<int>();

    // 追蹤目前金額
    private int currentMoney = 100;

    // 新增：追蹤答對題數的變數
    private int correctAnswerCount = 0;

    [Header("伺服器設定")]
    public string serverUrl = "http://localhost:5000/recognize_speech";
    public float recordingDuration = 3.0f;

    private AudioClip recordingClip;

    // 將 RecognitionResponse 定義在類別層級，方便所有方法使用
    [System.Serializable]
    public class RecognitionResponse
    {
        public string transcription;
    }


    void Start()
    {
        if (questionText == null)
        {
            Debug.LogError("請將 TextMeshPro (3D) 組件拖曳到 Question Text 欄位！");
            return;
        }
        if (panelBackground == null)
        {
            Debug.LogError("請將 Panel 背景的 GameObject 拖曳到 Panel Background 欄位！");
            return;
        }
        if (questionAudioSource == null)
        {
            Debug.LogError("請將 AudioSource 組件拖曳到 Question Audio Source 欄位！");
            return;
        }
        if (initialMoneyAudio == null)
        {
            Debug.LogError("請為 '現在你有100元' 提供音頻文件 (Initial Money Audio)！");
            return;
        }
        if (answerOptionAudios == null || answerOptionAudios.Count != answerOptions.Count)
        {
            Debug.LogError("請確保 Answer Option Audios 列表中有 " + answerOptions.Count + " 個音頻文件，且與題目順序一致！");
            return;
        }

        // 新增：Firebase 初始化
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

        panelBackground.SetActive(false);
        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        GenerateRandomQuestions();
        currentMoney = 100;
        correctAnswerCount = 0; // 重設答對題數
        panelBackground.SetActive(true);

        // 1. 處理固定題目
        questionText.text = initialMoneyQuestion;
        Debug.Log("顯示題目: " + initialMoneyQuestion);

        if (initialMoneyAudio != null)
        {
            questionAudioSource.clip = initialMoneyAudio;
            questionAudioSource.Play();
            yield return new WaitForSeconds(Mathf.Max(initialMoneyAudio.length, delayBetweenQuestions));
        }
        else
        {
            yield return new WaitForSeconds(delayBetweenQuestions);
        }

        // 2. 依序處理每個隨機題目
        for (int i = 0; i < currentQuestionSequenceIndices.Count; i++)
        {
            int questionListIndex = currentQuestionSequenceIndices[i];
            string currentQuestionText = answerOptions[questionListIndex];
            AudioClip currentQuestionAudio = answerOptionAudios[questionListIndex];

            // 邏輯：檢查是否是第二題或之後的題目 (i > 0)
            if (i > 0)
            {
                currentQuestionText = "再" + currentQuestionText;
            }

            questionText.text = currentQuestionText;
            Debug.Log("顯示題目: " + currentQuestionText);

            if (currentQuestionAudio != null)
            {
                questionAudioSource.clip = currentQuestionAudio;
                questionAudioSource.Play();
                yield return new WaitForSeconds(Mathf.Max(currentQuestionAudio.length, delayBetweenQuestions));
            }
            else
            {
                yield return new WaitForSeconds(delayBetweenQuestions);
            }

            yield return StartCoroutine(WaitForAnswer(i));
        }

        Debug.Log("所有題目已顯示完畢！");
        questionText.text = "商品購買完畢！";

        StartCoroutine(SaveCorrectAnswersToFirebaseCoroutine());
    }

    void GenerateRandomQuestions()
    {
        currentQuestionSequenceIndices = Enumerable.Range(0, answerOptions.Count)
                                         .OrderBy(x => System.Guid.NewGuid())
                                         .Take(3)
                                         .ToList();
    }

    IEnumerator WaitForAnswer(int questionSequenceIndex)
    {
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
            // 關鍵修改：在沒有麥克風時，也應該執行金額扣除邏輯
            // 但因為 SendAudioToServer 不會被呼叫，所以我們必須在這裡處理
            // 我們將金額處理邏輯從 CheckAnswer 移到 WaitForAnswer 的結束點
            // 這樣可以確保不論何種情況，金額都會被扣除
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

        // 核心修改：在協程結束時呼叫處理方法，保證無論成功與否都執行
        UpdateMoneyAndCheckAnswer(userResponse, questionSequenceIndex);
    }

    // 新增：將金額更新和答案檢查的邏輯合併成一個方法
    void UpdateMoneyAndCheckAnswer(string userResponse, int questionSequenceIndex)
    {
        int questionListIndex = currentQuestionSequenceIndices[questionSequenceIndex];
        string question = answerOptions[questionListIndex];

        Match match = Regex.Match(question, @"花費(\d+)元");
        int spentMoney = 0;
        if (match.Success)
        {
            spentMoney = int.Parse(match.Groups[1].Value);
        }

        int remainingMoney = currentMoney - spentMoney;
        currentMoney = remainingMoney;

        // 檢查回答是否為空，如果是，則只記錄金額已扣除
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