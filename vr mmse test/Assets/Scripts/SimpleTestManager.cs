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

        // 設定正確答案
        correctAnswers = new Dictionary<string, string>();
        DateTime now = DateTime.Now;
        correctAnswers["Year"] = now.Year.ToString();
        
        string[] monthNames = { "", "1月", "2月", "3月", "4月", "5月", "6月", 
                              "7月", "8月", "9月", "10月", "11月", "12月" };
        correctAnswers["Month"] = monthNames[now.Month];
        
        string[] dayNames = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
        correctAnswers["DayOfWeek"] = dayNames[(int)now.DayOfWeek];

        // 隨機設定季節
        if (seasonMaterials != null && seasonMaterials.Length >= 4 && sceneryRenderer != null)
        {
            int randomSeasonIndex = UnityEngine.Random.Range(0, 4);
            sceneryRenderer.material = seasonMaterials[randomSeasonIndex];
            string[] seasonNames = { "春天", "夏天", "秋天", "冬天" };
            correctAnswers["Season"] = seasonNames[randomSeasonIndex];
        }
        else
        {
            correctAnswers["Season"] = "春天";
        }

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
        
        // 更新標題
        if (titleText != null)
        {
            switch (currentQuestionKey)
            {
                case "Year":
                    titleText.text = "請問今年是哪一年？";
                    break;
                case "Season":
                    titleText.text = "請看看窗外，現在是什麼季節？";
                    break;
                case "Month":
                    titleText.text = "現在是幾月呢？";
                    break;
                case "DayOfWeek":
                    titleText.text = "那今天是星期幾？";
                    break;
            }
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

    // 以下是測試用的公開方法，用於按鈕事件
    public void SelectSpring() { RecordSelection("春天"); }
    public void SelectSummer() { RecordSelection("夏天"); }
    public void SelectAutumn() { RecordSelection("秋天"); }
    public void SelectWinter() { RecordSelection("冬天"); }
    
    public void Select2024() { RecordSelection("2024"); }
    public void Select2025() { RecordSelection("2025"); }
    public void Select2026() { RecordSelection("2026"); }
    
    public void SelectJan() { RecordSelection("1月"); }
    public void SelectFeb() { RecordSelection("2月"); }
    public void SelectMar() { RecordSelection("3月"); }
    public void SelectApr() { RecordSelection("4月"); }
    public void SelectMay() { RecordSelection("5月"); }
    public void SelectJun() { RecordSelection("6月"); }
    public void SelectJul() { RecordSelection("7月"); }
    public void SelectAug() { RecordSelection("8月"); }
    public void SelectSep() { RecordSelection("9月"); }
    public void SelectOct() { RecordSelection("10月"); }
    public void SelectNov() { RecordSelection("11月"); }
    public void SelectDec() { RecordSelection("12月"); }
    
    public void SelectSun() { RecordSelection("星期日"); }
    public void SelectMon() { RecordSelection("星期一"); }
    public void SelectTue() { RecordSelection("星期二"); }
    public void SelectWed() { RecordSelection("星期三"); }
    public void SelectThu() { RecordSelection("星期四"); }
    public void SelectFri() { RecordSelection("星期五"); }
    public void SelectSat() { RecordSelection("星期六"); }
}