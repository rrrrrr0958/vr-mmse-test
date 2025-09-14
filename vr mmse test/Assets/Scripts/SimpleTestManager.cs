using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
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
        // 啟動時立即測試材質設置
        ValidateSeasonSetup();
        InitializeTest();
        
        // 強制測試季節切換（調試用）
        if (forceSeasonChange)
        {
            Invoke("TestAllSeasons", 1f);
        }
    }

    // 驗證季節設置
    void ValidateSeasonSetup()
    {
        Debug.Log("=== 驗證季節設置 ===");
        
        if (sceneryRenderer == null)
        {
            Debug.LogError("❌ Scenery Renderer 未設置！請拖入 Window_Interactive 的 Renderer 組件");
            return;
        }
        else
        {
            Debug.Log($"✅ Scenery Renderer 已設置：{sceneryRenderer.name}");
        }
        
        if (seasonMaterials == null || seasonMaterials.Length < 4)
        {
            Debug.LogError("❌ Season Materials 數組不足4個！請設置春夏秋冬4個材質");
            return;
        }
        
        for (int i = 0; i < 4; i++)
        {
            if (seasonMaterials[i] == null)
            {
                Debug.LogError($"❌ Season Material[{i}] 未設置！");
            }
            else
            {
                string[] seasons = {"春", "夏", "秋", "冬"};
                Debug.Log($"✅ {seasons[i]}天材質已設置：{seasonMaterials[i].name}");
            }
        }
        
        Debug.Log("=== 驗證完成 ===");
    }

    // 測試所有季節（調試用）
    void TestAllSeasons()
    {
        StartCoroutine(CycleAllSeasons());
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
        
        // 初始化時設定場景材質
        Debug.Log($"🌍 初始化設定季節為：{correctAnswers["Season"]}");
        SetSceneryBySeason(correctAnswers["Season"], false);

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

    // 強化版材質切換，包含更多調試信息
    void SetSceneryBySeason(string season, bool animated = true)
    {
        Debug.Log($"🎨 開始設定場景材質：{season}，動畫：{animated}");
        
        if (sceneryRenderer == null)
        {
            Debug.LogError("❌ sceneryRenderer 為空！請檢查設置");
            return;
        }
        
        if (seasonMaterials == null || seasonMaterials.Length < 4)
        {
            Debug.LogError("❌ seasonMaterials 未正確設置！");
            return;
        }

        int seasonIndex = 0;
        switch(season)
        {
            case "春天": seasonIndex = 0; break;
            case "夏天": seasonIndex = 1; break;
            case "秋天": seasonIndex = 2; break;
            case "冬天": seasonIndex = 3; break;
            default:
                Debug.LogWarning($"⚠️ 未知季節：{season}，使用春天");
                seasonIndex = 0;
                break;
        }
        
        if (seasonMaterials[seasonIndex] == null)
        {
            Debug.LogError($"❌ 季節材質[{seasonIndex}]為空！");
            return;
        }
        
        Material oldMaterial = sceneryRenderer.material;
        Debug.Log($"📝 當前材質：{(oldMaterial != null ? oldMaterial.name : "null")}");
        Debug.Log($"📝 目標材質：{seasonMaterials[seasonIndex].name}");
        
        if (animated && Application.isPlaying)
        {
            Debug.Log("🔄 使用動畫切換");
            StartCoroutine(TransitionToSeasonMaterial(seasonIndex));
        }
        else
        {
            Debug.Log("⚡ 直接切換材質");
            sceneryRenderer.material = seasonMaterials[seasonIndex];
            Debug.Log($"✅ 材質已設置為：{sceneryRenderer.material.name}");
        }
    }
    
    IEnumerator TransitionToSeasonMaterial(int targetSeasonIndex)
    {
        Debug.Log($"🔄 開始材質動畫過渡到索引 {targetSeasonIndex}");
        
        if (seasonMaterials[targetSeasonIndex] == null)
        {
            Debug.LogError($"❌ 目標材質[{targetSeasonIndex}]為空！");
            yield break;
        }

        Material currentMaterial = sceneryRenderer.material;
        Material targetMaterial = seasonMaterials[targetSeasonIndex];
        
        if (currentMaterial == targetMaterial)
        {
            Debug.Log("ℹ️ 已經是目標材質，無需切換");
            yield break;
        }
        
        float elapsedTime = 0;
        
        Debug.Log($"⏱️ 開始 {seasonTransitionDuration} 秒的過渡動畫");
        
        while (elapsedTime < seasonTransitionDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / seasonTransitionDuration;
            
            if (progress >= 0.5f && sceneryRenderer.material != targetMaterial)
            {
                sceneryRenderer.material = targetMaterial;
                Debug.Log($"🔄 動畫中途切換材質：{targetMaterial.name}");
            }
            
            yield return null;
        }
        
        sceneryRenderer.material = targetMaterial;
        Debug.Log($"✅ 動畫完成，最終材質：{sceneryRenderer.material.name}");
    }

    // 其他方法保持不變...
    List<string> GenerateYearOptions()
    {
        int currentYear = DateTime.Now.Year;
        List<string> options = new List<string>();
        
        options.Add(currentYear.ToString());
        options.Add((currentYear - 1).ToString());
        options.Add((currentYear + 1).ToString());
        options.Add((currentYear - 2).ToString());
        
        ShuffleList(options);
        return options;
    }

    List<string> GenerateSeasonOptions()
    {
        string[] allSeasons = { "春天", "夏天", "秋天", "冬天" };
        string correctSeason = GetCurrentSeason();
        
        List<string> options = new List<string>();
        options.Add(correctSeason);
        
        foreach(string season in allSeasons)
        {
            if(season != correctSeason)
                options.Add(season);
        }
        
        ShuffleList(options);
        return options;
    }

    List<string> GenerateMonthOptions()
    {
        string[] monthNames = { "", "1月", "2月", "3月", "4月", "5月", "6月",
                              "7月", "8月", "9月", "10月", "11月", "12月" };
        int currentMonth = DateTime.Now.Month;
        string correctMonth = monthNames[currentMonth];
        
        List<string> options = new List<string>();
        options.Add(correctMonth);
        
        List<string> otherMonths = new List<string>();
        for(int i = 1; i <= 12; i++)
        {
            if(i != currentMonth)
                otherMonths.Add(monthNames[i]);
        }
        
        for(int i = 0; i < 3; i++)
        {
            int randomIndex = UnityEngine.Random.Range(0, otherMonths.Count);
            options.Add(otherMonths[randomIndex]);
            otherMonths.RemoveAt(randomIndex);
        }
        
        ShuffleList(options);
        return options;
    }

    List<string> GenerateDayOfWeekOptions()
    {
        string[] dayNames = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
        string correctDay = dayNames[(int)DateTime.Now.DayOfWeek];
        
        List<string> options = new List<string>();
        options.Add(correctDay);
        
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
        
        for(int i = 0; i < buttons.Length && i < options.Count; i++)
        {
            if(buttons[i] != null)
            {
                Text buttonText = buttons[i].GetComponentInChildren<Text>();
                if(buttonText != null)
                {
                    buttonText.text = options[i];
                }
                
                buttons[i].onClick.RemoveAllListeners();
                
                string optionValue = options[i];
                buttons[i].onClick.AddListener(() => RecordSelection(optionValue));
                
                buttons[i].gameObject.SetActive(true);
            }
        }
        
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
        Debug.Log("🎯 測驗開始");
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
        Debug.Log($"❓ 詢問問題: {currentQuestionKey}");
       
        // ★ 強化版季節場景切換
        if (currentQuestionKey == "Season")
        {
            string currentSeason = GetCurrentSeason();
            Debug.Log($"🌟 季節問題觸發！當前季節：{currentSeason}");
            SetSceneryBySeason(currentSeason, true);
        }
       
        List<string> options = null;
        switch(currentQuestionKey)
        {
            case "Year":
                options = GenerateYearOptions();
                if (titleText != null) titleText.text = "請問今年是哪一年？";
                break;
            case "Season":
                options = GenerateSeasonOptions();
                if (titleText != null) titleText.text = "請看看左邊，現在是什麼季節？";
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
        
        if(options != null)
        {
            UpdateButtonsForQuestion(currentQuestionKey, options);
        }

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

    // 新增的調試方法
    [ContextMenu("強制測試季節切換")]
    public void ForceTestSeasonChange()
    {
        string[] seasons = {"春天", "夏天", "秋天", "冬天"};
        SetSceneryBySeason(seasons[testSeasonIndex], true);
    }
    
    [ContextMenu("驗證設置")]
    public void ValidateSetup()
    {
        ValidateSeasonSetup();
    }
}