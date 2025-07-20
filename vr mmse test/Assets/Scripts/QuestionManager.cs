using UnityEngine;
using TMPro; // 引入TextMeshPro命名空間
using System.Collections; // 引入用於協程的命名空間
using System.Collections.Generic; // 引入用於List的命名空間
using System.Linq; // 引入用於LINQ，方便隨機選擇

public class QuestionManager : MonoBehaviour
{
    public TextMeshPro questionText; // 用於顯示文字的 TextMeshPro (3D)
    public GameObject panelBackground; // 問題背景面板的 GameObject (3D Quad 或 Plane)
    public float delayBetweenQuestions = 3.0f; // 每題之間的延遲時間

    public AudioSource questionAudioSource; // 用於播放題目語音的 AudioSource

    // 所有的題目文字都直接在 Script 中定義
    private string initialMoneyQuestion = "現在你有100元";
    private List<string> answerOptions = new List<string>
    {
        "花費25元買了魚之後剩多少?",
        "花費7元買了麵包之後剩多少?",
        "花費35元買了水果之後剩多少?",
        "花費15元買了武器之後剩多少?",
        "花費30元買了肉之後剩多少?"
    };

    // 所有題目對應的音頻片段，需要在 Inspector 中連接
    public AudioClip initialMoneyAudio; // "現在你有100元" 的音頻
    public List<AudioClip> answerOptionAudios; // a-e 題目的音頻列表

    private List<int> currentQuestionSequenceIndices = new List<int>(); // 儲存隨機選擇的題目在 answerOptions中的索引
    private int currentQuestionIndexInSequence = 0; // 當前在隨機序列中的題目索引

    void Start()
    {
        // 檢查必要的組件是否已連接
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

        // 檢查音頻文件是否已連接
        if (initialMoneyAudio == null)
        {
            Debug.LogError("請為 '現在你有100元' 提供音頻文件 (Initial Money Audio)！");
            return;
        }
        // 確保音頻列表的數量與題目文字列表的數量一致
        if (answerOptionAudios == null || answerOptionAudios.Count != answerOptions.Count)
        {
            Debug.LogError("請確保 Answer Option Audios 列表中有 " + answerOptions.Count + " 個音頻文件，且與題目順序一致！");
            return;
        }

        panelBackground.SetActive(false); // 初始時隱藏面板和文字

        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        GenerateRandomQuestions();
        yield return StartCoroutine(DisplayAndPlayQuestion()); // 啟動顯示和播放語音的協程
    }

    void GenerateRandomQuestions()
    {
        // 從 0 到 answerOptions.Count-1 的索引中隨機選擇3個
        // 這樣可以確保隨機選擇的題目對應到正確的音頻索引
        currentQuestionSequenceIndices = Enumerable.Range(0, answerOptions.Count)
                                         .OrderBy(x => System.Guid.NewGuid())
                                         .Take(3)
                                         .ToList();

        // 輸出隨機順序，用於調試
        Debug.Log("隨機題目順序 (索引)：");
        foreach (var index in currentQuestionSequenceIndices)
        {
            Debug.Log($"題目: {answerOptions[index]}, 音頻: {answerOptionAudios[index]?.name}");
        }
    }

    IEnumerator DisplayAndPlayQuestion()
    {
        panelBackground.SetActive(true); // 顯示面板和文字

        // 處理固定題目 "現在你有100元"
        if (currentQuestionIndexInSequence == 0)
        {
            questionText.text = initialMoneyQuestion; // 顯示固定文字
            Debug.Log("顯示題目: " + initialMoneyQuestion);

            if (initialMoneyAudio != null)
            {
                questionAudioSource.clip = initialMoneyAudio;
                questionAudioSource.Play();
                // 等待音頻播放完畢，或至少 delayBetweenQuestions 時間
                yield return new WaitForSeconds(Mathf.Max(initialMoneyAudio.length, delayBetweenQuestions));
            }
            else
            {
                yield return new WaitForSeconds(delayBetweenQuestions); // 如果沒有音頻，只延遲
            }
        }

        // 處理隨機選擇的題目 (a-e)
        // 注意這裡的判斷條件，currentQuestionIndexInSequence 已經包含初始題目，所以要檢查是否小於隨機序列的總長度 + 1 (因為初始題目佔用一個階段)
        if (currentQuestionIndexInSequence < currentQuestionSequenceIndices.Count + 1)
        {
            // 如果當前索引是 0，表示已經顯示過初始題目，現在要開始顯示第一個隨機題目
            // 如果當前索引大於 0，表示要顯示第 currentQuestionIndexInSequence 個隨機題目
            int actualRandomQuestionIndex = currentQuestionIndexInSequence - 1; // 減1因為隨機題目從索引0開始，而我們的sequence索引0已經給了固定題目

            if (actualRandomQuestionIndex >= 0 && actualRandomQuestionIndex < currentQuestionSequenceIndices.Count)
            {
                int questionListIndex = currentQuestionSequenceIndices[actualRandomQuestionIndex]; // 獲取隨機選中題目的原始列表索引
                string currentQuestionText = answerOptions[questionListIndex]; // 獲取對應的文字
                AudioClip currentQuestionAudio = answerOptionAudios[questionListIndex]; // 獲取對應的音頻

                questionText.text = currentQuestionText;
                Debug.Log("顯示題目: " + currentQuestionText);

                if (currentQuestionAudio != null)
                {
                    questionAudioSource.clip = currentQuestionAudio;
                    questionAudioSource.Play();
                    // 等待音頻播放完畢，或至少 delayBetweenQuestions 時間
                    yield return new WaitForSeconds(Mathf.Max(currentQuestionAudio.length, delayBetweenQuestions));
                }
                else
                {
                    yield return new WaitForSeconds(delayBetweenQuestions); // 如果沒有音頻，只延遲
                }
            }

            currentQuestionIndexInSequence++; // 前進到下一個題目（無論是固定還是隨機）

            // 如果還有題目需要顯示（包括隨機題目和初始題目），則繼續遞迴呼叫
            // 總共會顯示 1 (固定題目) + 3 (隨機題目) = 4 個階段
            if (currentQuestionIndexInSequence <= currentQuestionSequenceIndices.Count) // <= 表示還沒顯示完所有隨機題目
            {
                yield return StartCoroutine(DisplayAndPlayQuestion());
            }
            else
            {
                Debug.Log("所有題目已顯示完畢！");
                questionText.text = "所有題目已顯示完畢！";
                // panelBackground.SetActive(false); // 可以選擇最後隱藏
            }
        }
        else // 所有題目都顯示完畢了
        {
            Debug.Log("所有題目已顯示完畢！");
            questionText.text = "所有題目已顯示完畢！";
            // panelBackground.SetActive(false); // 可以選擇最後隱藏
        }
    }
}