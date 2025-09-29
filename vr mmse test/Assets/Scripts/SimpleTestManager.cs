using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // 為了方便產生不重複的隨機數

public class SimpleTestManager : MonoBehaviour
{
    [Header("UI References")]
    public Text titleText;
    public Text feedbackText;
    public GameObject startPanel;
    public GameObject yearPanel;
    public GameObject dayPanel; // 用於取代 seasonPanel
    public GameObject monthPanel;
    public GameObject dayOfWeekPanel;
    public GameObject resultPanel;
    public Button confirmButton;
    public Text resultScoreText;

    [Header("Dynamic Buttons - 手動拖拽4個按鈕")]
    public Button[] yearButtons = new Button[4];
    public Button[] dayButtons = new Button[4]; // 用於取代 seasonButtons
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
    public Material[] seasonMaterials; // 0:春, 1:夏, 2:秋, 3:冬
    public Renderer sceneryRenderer;
    
    [Header("Season Visual Effects")]
    public float seasonTransitionDuration = 1.0f;
    public AnimationCurve transitionCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    
    [Header("DEBUG - 測試用")]
    public bool forceSeasonChange = false;
    [Range(0, 3)]
    public int testSeasonIndex = 0; // 0:春, 1:夏, 2:秋, 3:冬

    private int currentQuestionIndex = 0;
    private int score = 0;
    private string selectedAnswer = "";
    private Dictionary<string, string> correctAnswers;
    private List<string> questions;
    private Dictionary<string, GameObject> questionPanels;

    void Start()
    {
        ValidateSeasonSetup();
        InitializeTest();
        
        if (forceSeasonChange)
        {
            Invoke("TestAllSeasons", 1f);
        }
    }

    #region Debug Methods
    void ValidateSeasonSetup()
    {
        Debug.Log("=== 驗證季節設置 ===");
        
        if (sceneryRenderer == null) Debug.LogError("❌ Scenery Renderer 未設置！");
        else Debug.Log($"✅ Scenery Renderer 已設置：{sceneryRenderer.name}");
        
        if (seasonMaterials == null || seasonMaterials.Length < 4)
        {
            Debug.LogError("❌ Season Materials 數組不足4個！");
            return;
        }
        
        for (int i = 0; i < 4; i++)
        {
            if (seasonMaterials[i] == null) Debug.LogError($"❌ Season Material[{i}] 未設置！");
            else
            {
                string[] seasons = {"春", "夏", "秋", "冬"};
                Debug.Log($"✅ {seasons[i]}天材質已設置：{seasonMaterials[i].name}");
            }
        }
        
        Debug.Log("=== 驗證完成 ===");
    }
    
    IEnumerator CycleAllSeasons()
    {
        string[] seasonNames = {"春天", "夏天", "秋天", "冬天"};
        
        for (int i = 0; i < 4; i++)
        {
            Debug.Log($"🔄 測試切換到 {seasonNames[i]}");
            SetSceneryBySeason(seasonNames[i], true);
            yield return new WaitForSeconds(2f);
        }
        
        Debug.Log("✅ 所有季節測試完成");
    }
    #endregion

    void InitializeTest()
    {
        score = 0;
        currentQuestionIndex = 0;
        correctAnswers = new Dictionary<string, string>();
        
        DateTime now = DateTime.Now;
        correctAnswers["Year"] = now.Year.ToString();
        correctAnswers["Day"] = now.Day.ToString(); // 改成 "日"
        
        string[] monthNames = { "", "1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月", "9月", "10月", "11月", "12月" };
        correctAnswers["Month"] = monthNames[now.Month];
        
        string[] dayNames = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
        correctAnswers["DayOfWeek"] = dayNames[(int)now.DayOfWeek];
        
        // 雖然問題拿掉，但仍然根據真實季節設定初始場景
        string currentActualSeason = GetCurrentSeason();
        Debug.Log($"🌍 初始化設定季節為：{currentActualSeason}");
        SetSceneryBySeason(currentActualSeason, false);

        questions = new List<string> { "Year", "Day", "Month", "DayOfWeek" }; // 將 "Season" 換成 "Day"
        
        questionPanels = new Dictionary<string, GameObject>();
        if (yearPanel != null) questionPanels["Year"] = yearPanel;
        if (dayPanel != null) questionPanels["Day"] = dayPanel;
        if (monthPanel != null) questionPanels["Month"] = monthPanel;
        if (dayOfWeekPanel != null) questionPanels["DayOfWeek"] = dayOfWeekPanel;

        HideAllPanels();
        if (startPanel != null) startPanel.SetActive(true);
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);
        if (feedbackText != null) feedbackText.text = "";
        if (titleText != null) titleText.text = "準備好就開始吧！";
        
        Debug.Log("Simple Test Manager initialized");
        Debug.Log("正確答案: 年=" + correctAnswers["Year"] + ", 日=" + correctAnswers["Day"] + ", 月=" + correctAnswers["Month"] + ", 星期=" + correctAnswers["DayOfWeek"]);
    }

    string GetCurrentSeason()
    {
        int month = DateTime.Now.Month;
        switch(month)
        {
            case 3: case 4: case 5: return "春天";
            case 6: case 7: case 8: return "夏天";
            case 9: case 10: case 11: return "秋天";
            case 12: case 1: case 2: return "冬天";
            default: return "春天";
        }
    }

    #region Audio and Scenery Logic
    void SetAmbienceBySeason(string season)
    {
        if (audioSource == null) return;
        AudioClip clipToPlay = null;
        switch(season)
        {
            case "春天": clipToPlay = springAmbience; break;
            case "夏天": clipToPlay = summerAmbience; break;
            case "秋天": clipToPlay = autumnAmbience; break;
            case "冬天": clipToPlay = winterAmbience; break;
        }

        if (clipToPlay != null && audioSource.clip != clipToPlay)
        {
            audioSource.clip = clipToPlay;
            audioSource.loop = true;
            audioSource.Play();
        }
    }

    void SetSceneryBySeason(string season, bool animated = true)
    {
        SetAmbienceBySeason(season);
        if (sceneryRenderer == null || seasonMaterials == null || seasonMaterials.Length < 4) return;
        
        int seasonIndex = 0;
        switch(season)
        {
            case "春天": seasonIndex = 0; break;
            case "夏天": seasonIndex = 1; break;
            case "秋天": seasonIndex = 2; break;
            case "冬天": seasonIndex = 3; break;
        }
        
        if (seasonMaterials[seasonIndex] == null) return;
        
        if (animated && Application.isPlaying)
        {
            StartCoroutine(TransitionToSeasonMaterial(seasonIndex));
        }
        else
        {
            sceneryRenderer.material = seasonMaterials[seasonIndex];
        }
    }
    
    IEnumerator TransitionToSeasonMaterial(int targetSeasonIndex)
    {
        Material targetMaterial = seasonMaterials[targetSeasonIndex];
        if (sceneryRenderer.material.name.StartsWith(targetMaterial.name)) yield break;
        
        float elapsedTime = 0;
        while (elapsedTime < seasonTransitionDuration)
        {
            elapsedTime += Time.deltaTime;
            if (elapsedTime >= seasonTransitionDuration / 2f && !sceneryRenderer.material.name.StartsWith(targetMaterial.name))
            {
                sceneryRenderer.material = targetMaterial;
            }
            yield return null;
        }
        sceneryRenderer.material = targetMaterial;
    }
    #endregion

    #region Options Generation
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
            case "Day": buttons = dayButtons; break;
            case "Month": buttons = monthButtons; break;
            case "DayOfWeek": buttons = dayOfWeekButtons; break;
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
    
    public void StartTest()
    {
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
        List<string> options = null;
        
        switch(currentQuestionKey)
        {
            case "Year":
                options = GenerateYearOptions();
                if (titleText != null) titleText.text = "請問今年是哪一年？";
                break;
            case "Day": 
                options = GenerateDayOptions();
                if (titleText != null) titleText.text = "今天幾號？";
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
        Debug.Log("選擇: " + selection);
    }

    public void ConfirmAnswer()
    {
        if (string.IsNullOrEmpty(selectedAnswer)) return;

        string currentQuestionKey = questions[currentQuestionIndex];
        bool isCorrect = (selectedAnswer == correctAnswers[currentQuestionKey]);

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
        if (resultScoreText != null) resultScoreText.text = "您的得分是：" + score + " / 4";
        
        if (audioSource != null) audioSource.Stop();
        Debug.Log("測驗完成，得分: " + score + "/4");
    }

    void HideAllPanels()
    {
        if (startPanel != null) startPanel.SetActive(false);
        if (yearPanel != null) yearPanel.SetActive(false);
        if (dayPanel != null) dayPanel.SetActive(false);
        if (monthPanel != null) monthPanel.SetActive(false);
        if (dayOfWeekPanel != null) dayOfWeekPanel.SetActive(false);
        if (resultPanel != null) resultPanel.SetActive(false);
    }
    
    public void RestartTest()
    {
        if (audioSource != null) audioSource.Stop();
        InitializeTest();
    }
}