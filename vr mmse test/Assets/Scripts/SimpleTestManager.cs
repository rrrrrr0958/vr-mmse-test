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
    
    // ----- 修改：GenerateDayOptions 方法以符合「均勻分佈」的規則 -----
    List<string> GenerateDayOptions()
    {
        DateTime now = DateTime.Now;
        int correctDay = now.Day;
        int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);

        // 1. 將月份大致分為四個「週」區塊
        List<List<int>> weeklyBuckets = new List<List<int>>
        {
            Enumerable.Range(1, 7).ToList(),                                  // 第1週 (1-7號)
            Enumerable.Range(8, 7).ToList(),                                  // 第2週 (8-14號)
            Enumerable.Range(15, 7).ToList(),                                 // 第3週 (15-21號)
            Enumerable.Range(22, daysInMonth - 21).Where(d => d <= daysInMonth).ToList() // 第4週及之後 (22號到月底)
        };

        // 2. 找出正確答案在哪一週
        int correctBucketIndex = -1;
        for (int i = 0; i < weeklyBuckets.Count; i++)
        {
            if (weeklyBuckets[i].Contains(correctDay))
            {
                correctBucketIndex = i;
                break;
            }
        }

        // 3. 建立選項列表，先加入正確答案
        List<string> options = new List<string> { correctDay.ToString() };

        // 4. 從「其他三週」中，各隨機挑選一個號碼作為錯誤答案
        for (int i = 0; i < weeklyBuckets.Count; i++)
        {
            if (i == correctBucketIndex) continue; // 跳過正確答案所在的那一週

            // 確保該週有日期可選 (例如2月最後一週可能很短)
            if (weeklyBuckets[i].Count > 0)
            {
                int randomDay = weeklyBuckets[i][UnityEngine.Random.Range(0, weeklyBuckets[i].Count)];
                options.Add(randomDay.ToString());
            }
        }

        // 5. 為防止意外情況 (例如月份太短導致選項不足4個)，進行補齊
        List<int> allDaysPool = Enumerable.Range(1, daysInMonth).Where(d => !options.Contains(d.ToString())).ToList();
        while (options.Count < 4 && allDaysPool.Count > 0)
        {
            int randomIndex = UnityEngine.Random.Range(0, allDaysPool.Count);
            options.Add(allDaysPool[randomIndex].ToString());
            allDaysPool.RemoveAt(randomIndex);
        }

        ShuffleList(options);
        return options;
    }


    List<string> GenerateHourOptions()
    {
        int currentHour = DateTime.Now.Hour;
        List<string> options = new List<string>();

        List<int> forbiddenHours = new List<int>();
        for (int i = -2; i <= 2; i++) { forbiddenHours.Add((currentHour + i + 24) % 24); }

        List<int> incorrectAmPool = Enumerable.Range(0, 12).Where(h => !forbiddenHours.Contains(h)).ToList();
        List<int> incorrectPmPool = Enumerable.Range(12, 12).Where(h => !forbiddenHours.Contains(h)).ToList();

        bool isCurrentHourAm = currentHour < 12;

        if (isCurrentHourAm)
        {
            options.Add(FormatHourTo12(currentHour));
            if (incorrectAmPool.Count > 0) { int r = UnityEngine.Random.Range(0, incorrectAmPool.Count); options.Add(FormatHourTo12(incorrectAmPool[r])); incorrectAmPool.RemoveAt(r); }
            for (int i = 0; i < 2; i++) { if (incorrectPmPool.Count > 0) { int r = UnityEngine.Random.Range(0, incorrectPmPool.Count); options.Add(FormatHourTo12(incorrectPmPool[r])); incorrectPmPool.RemoveAt(r); } }
        }
        else
        {
            options.Add(FormatHourTo12(currentHour));
            if (incorrectPmPool.Count > 0) { int r = UnityEngine.Random.Range(0, incorrectPmPool.Count); options.Add(FormatHourTo12(incorrectPmPool[r])); incorrectPmPool.RemoveAt(r); }
            for (int i = 0; i < 2; i++) { if (incorrectAmPool.Count > 0) { int r = UnityEngine.Random.Range(0, incorrectAmPool.Count); options.Add(FormatHourTo12(incorrectAmPool[r])); incorrectAmPool.RemoveAt(r); } }
        }
        
        while (options.Count < 4) { int rH = UnityEngine.Random.Range(0, 24); string fH = FormatHourTo12(rH); if (!options.Contains(fH)) { options.Add(fH); } }

        ShuffleList(options);
        return options;
    }

    // --- 其他選項生成方法與輔助函式保持不變 ---
    private string FormatHourTo12(int hour) { if (hour == 0) return "午夜 12:00"; if (hour < 10) return $"上午  {hour}:00"; if (hour < 12) return $"上午 {hour}:00"; if (hour == 12) return "中午 12:00"; int pmHour = hour - 12; if (pmHour < 10) return $"下午  {pmHour}:00"; return $"下午 {pmHour}:00"; }
    private int Parse12HourFormat(string timeString) { try { string[] parts = timeString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries); string period = parts[0]; int hour = int.Parse(parts[1].Split(':')[0]); if (period == "午夜" && hour == 12) return 0; if (period == "中午" && hour == 12) return 12; if (period == "上午") return hour; if (period == "下午") return (hour == 12) ? 12 : hour + 12; return hour; } catch { return -1; } }
    List<string> GenerateYearOptions() { int y = DateTime.Now.Year; var l = new List<string> {y.ToString(),(y-1).ToString(),(y+1).ToString(),(y-2).ToString()}; ShuffleList(l); return l; }
    List<string> GenerateMonthOptions() { string[] m={"","1月","2月","3月","4月","5月","6月","7月","8月","9月","10月","11月","12月"}; int cur=DateTime.Now.Month; var o=new List<string>{m[cur]}; var p=Enumerable.Range(1,12).Where(i=>i!=cur).ToList(); for(int i=0;i<3;i++){ if(p.Count==0)break; int r=UnityEngine.Random.Range(0,p.Count); o.Add(m[p[r]]); p.RemoveAt(r); } ShuffleList(o); return o; }
    List<string> GenerateDayOfWeekOptions() { string[] d={"星期日","星期一","星期二","星期三","星期四","星期五","星期六"}; int cur=(int)DateTime.Now.DayOfWeek; var o=new List<string>{d[cur]}; var p=Enumerable.Range(0,7).Where(i=>i!=cur).ToList(); for(int i=0;i<3;i++){ if(p.Count==0)break; int r=UnityEngine.Random.Range(0,p.Count); o.Add(d[p[r]]); p.RemoveAt(r); } ShuffleList(o); return o; }
    void ShuffleList<T>(List<T> list) { for (int i = 0; i < list.Count; i++) { T temp = list[i]; int randomIndex = UnityEngine.Random.Range(i, list.Count); list[i] = list[randomIndex]; list[randomIndex] = temp; } }
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
            case "Year": options = GenerateYearOptions(); if (titleText != null) titleText.text = "請問今年是哪一年？"; break;
            case "Month": options = GenerateMonthOptions(); if (titleText != null) titleText.text = "現在是幾月呢？"; break;
            case "Day": options = GenerateDayOptions(); if (titleText != null) titleText.text = "今天幾號？"; break;
            case "DayOfWeek": options = GenerateDayOfWeekOptions(); if (titleText != null) titleText.text = "那今天是星期幾？"; break;
            case "Hour": options = GenerateHourOptions(); if (titleText != null) titleText.text = "現在大概是什麼時候了？"; break;
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

    public void ConfirmAnswer()
    {
        if (string.IsNullOrEmpty(selectedAnswer)) return;

        string currentQuestionKey = questions[currentQuestionIndex];
        bool isCorrect = false;

        if (currentQuestionKey == "Hour")
        {
            int selectedHour = Parse12HourFormat(selectedAnswer);
            int currentHour = DateTime.Now.Hour;
            
            if (selectedHour != -1)
            {
                int diff = Math.Abs(selectedHour - currentHour);
                int distance = Math.Min(diff, 24 - diff);
                if (distance <= 2) isCorrect = true;
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
            if (feedbackText != null) { feedbackText.text = "再想想看喔."; feedbackText.color = Color.red; }
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