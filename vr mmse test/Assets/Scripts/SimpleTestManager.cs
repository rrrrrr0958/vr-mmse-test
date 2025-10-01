using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class SimpleTestManager : MonoBehaviour
{
    [Header("UI References")]
    public Text titleText;
    public Text feedbackText;
    public GameObject startPanel;
    public GameObject yearPanel;
    public GameObject dayPanel;
    public GameObject hourPanel;
    public GameObject monthPanel;
    public GameObject dayOfWeekPanel;
    public GameObject resultPanel;
    public Button confirmButton;
    public Text resultScoreText;

    [Header("Dynamic Buttons - 手動拖拽4個按鈕")]
    public Button[] yearButtons = new Button[4];
    public Button[] dayButtons = new Button[4];
    public Button[] hourButtons = new Button[4];
    public Button[] monthButtons = new Button[4];
    public Button[] dayOfWeekButtons = new Button[4];

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip correctSFX;
    public AudioClip incorrectSFX;
    [Header("Season Ambience")]
    public AudioClip springAmbience;
    public AudioClip summerAmbience;
    public AudioClip autumnAmbience;
    public AudioClip winterAmbience;

    [Header("Scenery")]
    public Material[] seasonMaterials;
    public Renderer sceneryRenderer;
    
    [Header("Season Visual Effects")]
    public float seasonTransitionDuration = 1.0f;
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("DEBUG - 測試用")]
    public bool forceSeasonChange = false;
    [Range(0, 3)]
    public int testSeasonIndex = 0;

    private int currentQuestionIndex = 0;
    private int score = 0;
    private string selectedAnswer = "";
    private Dictionary<string, string> correctAnswers;
    private List<string> questions;
    private Dictionary<string, GameObject> questionPanels;
    private const int TOTAL_QUESTIONS = 5;

    void Start()
    {
        InitializeTest();
        if (forceSeasonChange)
        {
            StartCoroutine(CycleAllSeasons());
        }
    }
    
    void InitializeTest()
    {
        score = 0;
        currentQuestionIndex = 0;
        correctAnswers = new Dictionary<string, string>();
        
        DateTime now = DateTime.Now;
        correctAnswers["Year"] = now.Year.ToString();
        correctAnswers["Day"] = now.Day.ToString();
        
        string[] monthNames = { "", "1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月", "9月", "10月", "11月", "12月" };
        correctAnswers["Month"] = monthNames[now.Month];
        
        string[] dayNames = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
        correctAnswers["DayOfWeek"] = dayNames[(int)now.DayOfWeek];

        SetSceneryBySeason(GetCurrentSeason(), false);

        questions = new List<string> { "Year", "Month", "Day", "DayOfWeek", "Hour" };
        
        questionPanels = new Dictionary<string, GameObject>();
        if (yearPanel != null) questionPanels["Year"] = yearPanel;
        if (monthPanel != null) questionPanels["Month"] = monthPanel;
        if (dayPanel != null) questionPanels["Day"] = dayPanel;
        if (dayOfWeekPanel != null) questionPanels["DayOfWeek"] = dayOfWeekPanel;
        if (hourPanel != null) questionPanels["Hour"] = hourPanel;

        HideAllPanels();
        if (startPanel != null) startPanel.SetActive(true);
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (feedbackText != null) feedbackText.text = "";
        if (titleText != null) titleText.text = "準備好就開始吧！";
        
        Debug.Log("Simple Test Manager initialized");
    }

    #region Options Generation & Helpers
    
    // ----- 新增：輔助函式，將 24 小時制轉為 12 小時制文字 -----
    private string FormatHourTo12(int hour)
    {
        if (hour == 0) return "午夜 12:00";
        if (hour < 12) return $"上午 {hour}:00";
        if (hour == 12) return "中午 12:00";
        return $"下午 {hour - 12}:00";
    }

    // ----- 新增：輔助函式，將 12 小時制文字解析回 24 小時制數字 -----
    private int Parse12HourFormat(string timeString)
    {
        try
        {
            string[] parts = timeString.Split(' ');
            string period = parts[0];
            int hour = int.Parse(parts[1].Split(':')[0]);

            if (period == "午夜" && hour == 12) return 0;
            if (period == "中午" && hour == 12) return 12;
            if (period == "上午") return hour;
            if (period == "下午") return hour + 12;
            
            return hour; // Fallback
        }
        catch (Exception e)
        {
            Debug.LogError($"解析時間字串 '{timeString}' 失敗: {e.Message}");
            return -1; // 回傳一個錯誤值
        }
    }
    
    // ----- 修改：GenerateHourOptions 方法以符合新的優化建議 -----
    List<string> GenerateHourOptions()
    {
        int currentHour = DateTime.Now.Hour;
        List<string> options = new List<string>();

        // 1. 固定將「當前整點」作為選項之一，並使用12小時制格式化
        options.Add(FormatHourTo12(currentHour));

        // 2. 產生三個確定在 ±2 小時範圍外的「錯誤」答案
        List<int> forbiddenHours = new List<int>();
        for (int i = -2; i <= 2; i++)
        {
            forbiddenHours.Add((currentHour + i + 24) % 24);
        }

        // 找出所有可用的錯誤小時選項
        List<int> availableHours = Enumerable.Range(0, 24).Where(h => !forbiddenHours.Contains(h)).ToList();

        for (int i = 0; i < 3; i++)
        {
            if (availableHours.Count == 0) break;
            int randomIndex = UnityEngine.Random.Range(0, availableHours.Count);
            int randomHour = availableHours[randomIndex];
            options.Add(FormatHourTo12(randomHour));
            availableHours.RemoveAt(randomIndex);
        }

        ShuffleList(options);
        return options;
    }

    // --- 其他選項生成方法保持不變 ---
    List<string> GenerateYearOptions()
    {
        int currentYear = DateTime.Now.Year;
        List<string> options = new List<string> { currentYear.ToString(), (currentYear - 1).ToString(), (currentYear + 1).ToString(), (currentYear - 2).ToString() };
        ShuffleList(options);
        return options;
    }

    List<string> GenerateDayOptions()
    {
        DateTime now = DateTime.Now;
        int correctDay = now.Day;
        List<string> options = new List<string> { correctDay.ToString() };
        int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        List<int> otherDays = Enumerable.Range(1, daysInMonth).Where(d => d != correctDay).ToList();
        for (int i = 0; i < 3; i++)
        {
            if (otherDays.Count == 0) break;
            int randomIndex = UnityEngine.Random.Range(0, otherDays.Count);
            options.Add(otherDays[randomIndex].ToString());
            otherDays.RemoveAt(randomIndex);
        }
        ShuffleList(options);
        return options;
    }

    List<string> GenerateMonthOptions()
    {
        string[] monthNames = { "", "1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月", "9月", "10月", "11月", "12月" };
        int currentMonth = DateTime.Now.Month;
        List<string> options = new List<string> { monthNames[currentMonth] };
        List<int> otherMonthIndices = Enumerable.Range(1, 12).Where(m => m != currentMonth).ToList();
        for (int i = 0; i < 3; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, otherMonthIndices.Count);
            options.Add(monthNames[otherMonthIndices[randomIndex]]);
            otherMonthIndices.RemoveAt(randomIndex);
        }
        ShuffleList(options);
        return options;
    }

    List<string> GenerateDayOfWeekOptions()
    {
        string[] dayNames = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
        int correctDayIndex = (int)DateTime.Now.DayOfWeek;
        List<string> options = new List<string> { dayNames[correctDayIndex] };
        List<int> otherDayIndices = Enumerable.Range(0, 7).Where(d => d != correctDayIndex).ToList();
        for (int i = 0; i < 3; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, otherDayIndices.Count);
            options.Add(dayNames[otherDayIndices[randomIndex]]);
            otherDayIndices.RemoveAt(randomIndex);
        }
        ShuffleList(options);
        return options;
    }
    
    void ShuffleList<T>(List<T> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            T temp = list[i];
            int randomIndex = UnityEngine.Random.Range(i, list.Count);
            list[i] = list[randomIndex];
            list[randomIndex] = temp;
        }
    }
    #endregion

    void UpdateButtonsForQuestion(string questionType, List<string> options)
    {
        Button[] buttons = null;
        switch(questionType)
        {
            case "Year": buttons = yearButtons; break;
            case "Month": buttons = monthButtons; break;
            case "Day": buttons = dayButtons; break;
            case "DayOfWeek": buttons = dayOfWeekButtons; break;
            case "Hour": buttons = hourButtons; break;
        }
        if (buttons == null) return;
        for (int i = 0; i < buttons.Length; i++)
        {
            if (i < options.Count)
            {
                buttons[i].gameObject.SetActive(true);
                buttons[i].GetComponentInChildren<Text>().text = options[i];
                buttons[i].onClick.RemoveAllListeners();
                string optionValue = options[i];
                buttons[i].onClick.AddListener(() => RecordSelection(optionValue));
            }
            else
            {
                buttons[i].gameObject.SetActive(false);
            }
        }
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
        List<string> options = null;
        
        switch(currentQuestionKey)
        {
            case "Year":
                options = GenerateYearOptions();
                if (titleText != null) titleText.text = "請問今年是哪一年？";
                break;
            case "Month":
                options = GenerateMonthOptions();
                if (titleText != null) titleText.text = "現在是幾月呢？";
                break;
            case "Day": 
                options = GenerateDayOptions();
                if (titleText != null) titleText.text = "今天幾號？";
                break;
            case "DayOfWeek":
                options = GenerateDayOfWeekOptions();
                if (titleText != null) titleText.text = "那今天是星期幾？";
                break;
            case "Hour":
                options = GenerateHourOptions();
                // ----- 修改：調整問題文字 -----
                if (titleText != null) titleText.text = "現在大概是什麼時候了？";
                break;
        }
        
        if (options != null) UpdateButtonsForQuestion(currentQuestionKey, options);
        if (questionPanels.ContainsKey(currentQuestionKey) && questionPanels[currentQuestionKey] != null)
        {
            questionPanels[currentQuestionKey].SetActive(true);
        }
        if (confirmButton != null)
        {
            confirmButton.gameObject.SetActive(true);
            confirmButton.interactable = false;
        }
    }

    public void RecordSelection(string selection)
    {
        selectedAnswer = selection;
        if (confirmButton != null) confirmButton.interactable = true;
    }

    // ----- 修改：ConfirmAnswer 以便能解析 12 小時制文字 -----
    public void ConfirmAnswer()
    {
        if (string.IsNullOrEmpty(selectedAnswer)) return;

        string currentQuestionKey = questions[currentQuestionIndex];
        bool isCorrect = false;

        if (currentQuestionKey == "Hour")
        {
            int selectedHour = Parse12HourFormat(selectedAnswer); // 使用新的解析函式
            int currentHour = DateTime.Now.Hour;
            
            if (selectedHour != -1) // 確保解析成功
            {
                int diff = Math.Abs(selectedHour - currentHour);
                int distance = Math.Min(diff, 24 - diff);

                if (distance <= 2)
                {
                    isCorrect = true;
                }
            }
        }
        else
        {
            isCorrect = (selectedAnswer == correctAnswers[currentQuestionKey]);
        }
        
        if (isCorrect)
        {
            score++;
            if (feedbackText != null) { feedbackText.text = "答對了！"; feedbackText.color = Color.green; }
            if (correctSFX != null && audioSource != null) { audioSource.PlayOneShot(correctSFX); }
        }
        else
        {
            if (feedbackText != null) { feedbackText.text = "再想想看喔。"; feedbackText.color = Color.red; }
            if (incorrectSFX != null && audioSource != null) { audioSource.PlayOneShot(incorrectSFX); }
        }

        currentQuestionIndex++;
        HideAllPanels();
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        
        Invoke("AskNextQuestion", 2f);
    }

    void ShowResults()
    {
        HideAllPanels();
        if (resultPanel != null) resultPanel.SetActive(true);
        if (titleText != null) titleText.text = "測驗結束！";
        if (resultScoreText != null) resultScoreText.text = "您的得分是：" + score + " / " + TOTAL_QUESTIONS;
        if (audioSource != null) audioSource.Stop();
    }

    void HideAllPanels()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (yearPanel != null) yearPanel.SetActive(false);
        if (monthPanel != null) monthPanel.SetActive(false);
        if (dayPanel != null) dayPanel.SetActive(false);
        if (dayOfWeekPanel != null) dayOfWeekPanel.SetActive(false);
        if (hourPanel != null) hourPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
    }
    
    #region Unchanged Methods
    public void StartTest() { HideAllPanels(); AskNextQuestion(); }
    public void RestartTest() { if (audioSource != null) audioSource.Stop(); InitializeTest(); }
    string GetCurrentSeason() { int m = DateTime.Now.Month; if (m >= 3 && m <= 5) return "春天"; if (m >= 6 && m <= 8) return "夏天"; if (m >= 9 && m <= 11) return "秋天"; return "冬天"; }
    void SetAmbienceBySeason(string season) { /* ... 內容不變 ... */ }
    void SetSceneryBySeason(string season, bool animated = true) { /* ... 內容不變 ... */ }
    IEnumerator TransitionToSeasonMaterial(int targetSeasonIndex) { /* ... 內容不變 ... */ yield return null; }
    IEnumerator CycleAllSeasons() { /* ... 內容不變 ... */ yield return null; }
    #endregion
}