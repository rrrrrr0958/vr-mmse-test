using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections.Generic;

public class SimpleTestManager : MonoBehaviour
{
    [Header("UI References")]
    public Text titleText;
    public Text feedbackText;
    public GameObject startPanel;
    public GameObject yearPanel;
    public GameObject seasonPanel;
    public GameObject monthPanel;
    public GameObject dayOfWeekPanel;
    public GameObject resultPanel;
    public Button confirmButton;
    public Text resultScoreText;

    [Header("Dynamic Buttons - 手動拖拽4個按鈕")]
    public Button[] yearButtons = new Button[4];
    public Button[] seasonButtons = new Button[4];
    public Button[] monthButtons = new Button[4];
    public Button[] dayOfWeekButtons = new Button[4];

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip correctSFX;
    public AudioClip incorrectSFX;

    [Header("Scenery")]
    public Material[] seasonMaterials; // 0:春, 1:夏, 2:秋, 3:冬
    public Renderer sceneryRenderer;

    private int currentQuestionIndex = 0;
    private int score = 0;
    private string selectedAnswer = "";
    private Dictionary<string, string> correctAnswers;
    private List<string> questions;
    private Dictionary<string, GameObject> questionPanels;

    void Start()
    {
        InitializeTest();
    }

    void InitializeTest()
    {
        score = 0;
        currentQuestionIndex = 0;

        // 設定正確答案（基於真實時間）
        correctAnswers = new Dictionary<string, string>();
        DateTime now = DateTime.Now;
        correctAnswers["Year"] = now.Year.ToString();
       
        string[] monthNames = { "", "1月", "2月", "3月", "4月", "5月", "6月",
                              "7月", "8月", "9月", "10月", "11月", "12月" };
        correctAnswers["Month"] = monthNames[now.Month];
       
        string[] dayNames = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
        correctAnswers["DayOfWeek"] = dayNames[(int)now.DayOfWeek];

        // 季節改為真實季節
        correctAnswers["Season"] = GetCurrentSeason();
        
        // 場景材質也根據真實季節設定
        SetSceneryBySeason(correctAnswers["Season"]);

        // 設定題目順序
        questions = new List<string> { "Year", "Season", "Month", "DayOfWeek" };
       
        // 設定面板字典
        questionPanels = new Dictionary<string, GameObject>();
        if (yearPanel != null) questionPanels["Year"] = yearPanel;
        if (seasonPanel != null) questionPanels["Season"] = seasonPanel;
        if (monthPanel != null) questionPanels["Month"] = monthPanel;
        if (dayOfWeekPanel != null) questionPanels["DayOfWeek"] = dayOfWeekPanel;

        // 初始化UI
        HideAllPanels();
        if (startPanel != null) startPanel.SetActive(true);
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (feedbackText != null) feedbackText.text = "";
        if (titleText != null) titleText.text = "準備好就開始吧！";
       
        Debug.Log("Simple Test Manager initialized");
        Debug.Log("正確答案: 年=" + correctAnswers["Year"] +
                 ", 季節=" + correctAnswers["Season"] +
                 ", 月=" + correctAnswers["Month"] +
                 ", 星期=" + correctAnswers["DayOfWeek"]);
    }

    // 根據月份判斷真實季節
    string GetCurrentSeason()
    {
        int month = DateTime.Now.Month;
        
        switch(month)
        {
            case 3: case 4: case 5:
                return "春天";
            case 6: case 7: case 8:
                return "夏天";
            case 9: case 10: case 11:
                return "秋天";
            case 12: case 1: case 2:
                return "冬天";
            default:
                return "春天";
        }
    }

    // 根據季節設定場景材質
    void SetSceneryBySeason(string season)
    {
        if (seasonMaterials != null && seasonMaterials.Length >= 4 && sceneryRenderer != null)
        {
            int seasonIndex = 0;
            switch(season)
            {
                case "春天": seasonIndex = 0; break;
                case "夏天": seasonIndex = 1; break;
                case "秋天": seasonIndex = 2; break;
                case "冬天": seasonIndex = 3; break;
            }
            sceneryRenderer.material = seasonMaterials[seasonIndex];
        }
    }

    // 生成年份選項（1個正確+3個錯誤）
    List<string> GenerateYearOptions()
    {
        int currentYear = DateTime.Now.Year;
        List<string> options = new List<string>();
        
        // 正確答案
        options.Add(currentYear.ToString());
        
        // 3個錯誤選項
        options.Add((currentYear - 1).ToString());
        options.Add((currentYear + 1).ToString());
        options.Add((currentYear - 2).ToString());
        
        // 打亂順序
        ShuffleList(options);
        return options;
    }

    // 生成季節選項
    List<string> GenerateSeasonOptions()
    {
        string[] allSeasons = { "春天", "夏天", "秋天", "冬天" };
        string correctSeason = GetCurrentSeason();
        
        List<string> options = new List<string>();
        options.Add(correctSeason);
        
        // 添加其他3個季節作為錯誤選項
        foreach(string season in allSeasons)
        {
            if(season != correctSeason)
                options.Add(season);
        }
        
        ShuffleList(options);
        return options;
    }

    // 生成月份選項
    List<string> GenerateMonthOptions()
    {
        string[] monthNames = { "", "1月", "2月", "3月", "4月", "5月", "6月",
                              "7月", "8月", "9月", "10月", "11月", "12月" };
        int currentMonth = DateTime.Now.Month;
        string correctMonth = monthNames[currentMonth];
        
        List<string> options = new List<string>();
        options.Add(correctMonth);
        
        // 隨機選3個其他月份
        List<string> otherMonths = new List<string>();
        for(int i = 1; i <= 12; i++)
        {
            if(i != currentMonth)
                otherMonths.Add(monthNames[i]);
        }
        
        // 隨機選3個
        for(int i = 0; i < 3; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, otherMonths.Count);
            options.Add(otherMonths[randomIndex]);
            otherMonths.RemoveAt(randomIndex);
        }
        
        ShuffleList(options);
        return options;
    }

    // 生成星期選項
    List<string> GenerateDayOfWeekOptions()
    {
        string[] dayNames = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
        string correctDay = dayNames[(int)DateTime.Now.DayOfWeek];
        
        List<string> options = new List<string>();
        options.Add(correctDay);
        
        // 隨機選3個其他星期
        List<string> otherDays = new List<string>();
        foreach(string day in dayNames)
        {
            if(day != correctDay)
                otherDays.Add(day);
        }
        
        for(int i = 0; i < 3; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, otherDays.Count);
            options.Add(otherDays[randomIndex]);
            otherDays.RemoveAt(randomIndex);
        }
        
        ShuffleList(options);
        return options;
    }

    // 打亂列表順序的輔助方法
    void ShuffleList<T>(List<T> list)
    {
        for(int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = UnityEngine.Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }

    // 更新按鈕文字和事件的通用方法
    void UpdateButtonsForQuestion(string questionType, List<string> options)
    {
        Button[] buttons = null;
        
        switch(questionType)
        {
            case "Year": buttons = yearButtons; break;
            case "Season": buttons = seasonButtons; break;
            case "Month": buttons = monthButtons; break;
            case "DayOfWeek": buttons = dayOfWeekButtons; break;
        }
        
        if(buttons == null) return;
        
        // 更新按鈕文字和事件
        for(int i = 0; i < buttons.Length && i < options.Count; i++)
        {
            if(buttons[i] != null)
            {
                // 更新按鈕文字
                Text buttonText = buttons[i].GetComponentInChildren<Text>();
                if(buttonText != null)
                {
                    buttonText.text = options[i];
                }
                
                // 移除舊的事件監聽器
                buttons[i].onClick.RemoveAllListeners();
                
                // 添加新的事件監聽器
                string optionValue = options[i]; // 重要：需要捕獲局部變量
                buttons[i].onClick.AddListener(() => RecordSelection(optionValue));
                
                buttons[i].gameObject.SetActive(true);
            }
        }
        
        // 隱藏多餘的按鈕（雖然我們都是4個選項，但保險起見）
        for(int i = options.Count; i < buttons.Length; i++)
        {
            if(buttons[i] != null)
            {
                buttons[i].gameObject.SetActive(false);
            }
        }
    }

    public void StartTest()
    {
        Debug.Log("測驗開始");
        HideAllPanels();
        AskNextQuestion();
    }

    void AskNextQuestion()
    {
        if (currentQuestionIndex >= questions.Count)
        {
            ShowResults();
            return;
        }

        selectedAnswer = "";
        if (feedbackText != null) feedbackText.text = "";
       
        string currentQuestionKey = questions[currentQuestionIndex];
        Debug.Log("詢問問題: " + currentQuestionKey);
       
        // 生成選項並更新按鈕
        List<string> options = null;
        switch(currentQuestionKey)
        {
            case "Year":
                options = GenerateYearOptions();
                if (titleText != null) titleText.text = "請問今年是哪一年？";
                break;
            case "Season":
                options = GenerateSeasonOptions();
                if (titleText != null) titleText.text = "請看看窗外，現在是什麼季節？";
                break;
            case "Month":
                options = GenerateMonthOptions();
                if (titleText != null) titleText.text = "現在是幾月呢？";
                break;
            case "DayOfWeek":
                options = GenerateDayOfWeekOptions();
                if (titleText != null) titleText.text = "那今天是星期幾？";
                break;
        }
        
        // 更新按鈕
        if(options != null)
        {
            UpdateButtonsForQuestion(currentQuestionKey, options);
        }

        // 顯示對應面板
        if (questionPanels.ContainsKey(currentQuestionKey) && questionPanels[currentQuestionKey] != null)
        {
            questionPanels[currentQuestionKey].SetActive(true);
        }
       
        // 顯示確認按鈕但禁用
        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(true);
            confirmButton.interactable = false;
        }
    }

    public void RecordSelection(string selection)
    {
        selectedAnswer = selection;
        if (confirmButton != null)
        {
            confirmButton.interactable = true;
        }
        Debug.Log("選擇: " + selection);
    }

    public void ConfirmAnswer()
    {
        if (string.IsNullOrEmpty(selectedAnswer)) return;

        string currentQuestionKey = questions[currentQuestionIndex];
        bool isCorrect = (selectedAnswer == correctAnswers[currentQuestionKey]);

        Debug.Log("答案: " + selectedAnswer + ", 正確答案: " + correctAnswers[currentQuestionKey] + ", 是否正確: " + isCorrect);

        if (isCorrect)
        {
            score++;
            if (feedbackText != null)
            {
                feedbackText.text = "答對了！";
                feedbackText.color = Color.green;
            }
            if (correctSFX != null && audioSource != null)
            {
                audioSource.PlayOneShot(correctSFX);
            }
        }
        else
        {
            if (feedbackText != null)
            {
                feedbackText.text = "再想想看喔。";
                feedbackText.color = Color.red;
            }
            if (incorrectSFX != null && audioSource != null)
            {
                audioSource.PlayOneShot(incorrectSFX);
            }
        }

        currentQuestionIndex++;
        HideAllPanels();
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
       
        Invoke("AskNextQuestion", 2f);
    }

    void ShowResults()
    {
        HideAllPanels();
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(true);
        if (titleText != null) titleText.text = "測驗結束！";
        if (resultScoreText != null) resultScoreText.text = "您的得分是：" + score + " / 4";
        Debug.Log("測驗完成，得分: " + score + "/4");
    }

    void HideAllPanels()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (yearPanel != null) yearPanel.SetActive(false);
        if (seasonPanel != null) seasonPanel.SetActive(false);
        if (monthPanel != null) monthPanel.SetActive(false);
        if (dayOfWeekPanel != null) dayOfWeekPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
    }
   
    public void RestartTest()
    {
        InitializeTest();
    }

    // 注意：刪除了所有固定的選擇方法，因為現在使用動態事件處理
    // 不再需要 SelectSpring()、Select2024() 等方法
}