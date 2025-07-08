using UnityEngine;
using TMPro; // 引入TextMeshPro命名空間
using System.Collections; // 引入用於協程的命名空間
using System.Collections.Generic; // 引入用於List的命名空間
using System.Linq; // 引入用於LINQ，方便隨機選擇

public class QuestionManager : MonoBehaviour
{
    public TextMeshPro questionText; // 現在是 TextMeshPro (3D)
    public GameObject panelBackground; // 背景可以是 3D Quad 或 Plane
    public float delayBetweenQuestions = 3.0f; // 每題之間的延遲時間

    private string initialMoneyQuestion = "現在你有100元";
    private List<string> answerOptions = new List<string>
    {
        "花費25元買了魚之後剩多少?",
        "花費7元買了麵包之後剩多少?",
        "花費35元買了水果之後剩多少?",
        "花費15元買了武器之後剩多少?",
        "花費30元買了肉之後剩多少?"
    };

    private List<string> currentQuestionSequence = new List<string>(); // 儲存本次遊戲的隨機題目序列
    private int currentQuestionIndex = 0; // 當前題目的索引

    // 這裡需要你的語音輸入系統的參考。
    // 由於Unity內建的語音識別功能有限，你可能需要整合第三方插件，
    // 例如：Windows Speech Recognition (PC), iOS Speech Framework (iOS) 或 Google Cloud Speech-to-Text。
    // 暫時我們用一個模擬的語音輸入來表示。
    // public YourSpeechRecognitionSystem speechRecognizer; // 假設你整合的語音識別系統

    void Start()
    {
        if (questionText == null)
        {
            Debug.LogError("請將TextMeshProUGUI組件拖曳到Question Text欄位！");
            return;
        }
        if (panelBackground == null)
        {
            Debug.LogError("請將Panel背景的GameObject拖曳到Panel Background欄位！");
            return;
        }

        panelBackground.SetActive(false); // 初始時隱藏面板和文字

        // 這裡可以初始化你的語音識別系統
        // if (speechRecognizer != null)
        // {
        //     speechRecognizer.OnSpeechRecognized += HandleSpeechInput;
        // }

        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        GenerateRandomQuestions();
        yield return StartCoroutine(DisplayNextQuestion()); // 先顯示第一段固定文字
    }

    void GenerateRandomQuestions()
    {
        // 從 answerOptions 中隨機選擇3個題目
        // 使用LINQ的OrderBy(Guid.NewGuid())進行隨機排序，然後取前3個
        currentQuestionSequence = answerOptions.OrderBy(x => System.Guid.NewGuid()).Take(3).ToList();

        // 輸出隨機順序，用於調試
        Debug.Log("隨機題目順序：");
        foreach (var q in currentQuestionSequence)
        {
            Debug.Log(q);
        }
    }

    IEnumerator DisplayNextQuestion()
    {
        panelBackground.SetActive(true); // 顯示面板和文字

        if (currentQuestionIndex == 0)
        {
            // 先顯示固定題目
            questionText.text = initialMoneyQuestion;
            Debug.Log("顯示題目: " + initialMoneyQuestion);
            yield return new WaitForSeconds(delayBetweenQuestions); // 停留一段時間
        }

        if (currentQuestionIndex < currentQuestionSequence.Count)
        {
            questionText.text = currentQuestionSequence[currentQuestionIndex];
            Debug.Log("顯示題目: " + currentQuestionSequence[currentQuestionIndex]);

            // 在這裡等待語音輸入
            // 實際情況下，你需要一個機制來接收語音輸入並觸發下一題
            // yield return StartCoroutine(WaitForSpeechInput()); // 假設有這個協程來等待語音輸入

            yield return new WaitForSeconds(delayBetweenQuestions); // 暫時用延遲模擬等待語音輸入時間

            currentQuestionIndex++;

            // 如果還有題目，繼續顯示下一題
            if (currentQuestionIndex <= currentQuestionSequence.Count)
            {
                yield return StartCoroutine(DisplayNextQuestion());
            }
            else
            {
                Debug.Log("所有題目已顯示完畢！");
                questionText.text = "所有題目已顯示完畢！";
                // panelBackground.SetActive(false); // 可以選擇最後隱藏
            }
        }
        else
        {
            Debug.Log("所有題目已顯示完畢！");
            questionText.text = "所有題目已顯示完畢！";
            // panelBackground.SetActive(false); // 可以選擇最後隱藏
        }
    }

    // 模擬語音輸入處理，實際需要與你的語音識別系統整合
    // public void HandleSpeechInput(string recognizedText)
    // {
    //     Debug.Log("識別到的語音: " + recognizedText);
    //     // 在這裡你可以處理語音輸入，例如檢查答案是否正確
    //     // 然後根據答案決定是否進入下一題
    //     // 如果答案正確，可以呼叫 StopCoroutine(WaitForSpeechInput()) 並啟動 DisplayNextQuestion()
    // }
}