using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Newtonsoft.Json;
using UnityEngine.EventSystems; // 為了能使用 EventSystem 而新增

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SimpleTestManager : MonoBehaviour
{
    private FirebaseManager_Firestore FirebaseManager;
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

    [Header("Question Prompts")]
    public AudioClip startTestAudio;
    public AudioClip yearQuestionAudio;
    public AudioClip monthQuestionAudio;
    public AudioClip dayQuestionAudio;
    public AudioClip dayOfWeekQuestionAudio;
    public AudioClip hourQuestionAudio;

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

    private int RemainingQuestions => Mathf.Max(0, (questions != null ? questions.Count - currentQuestionIndex : 0));
    private bool IsSceneFinished() => RemainingQuestions == 0;
    private Dictionary<string, string> playerAnswers = new Dictionary<string, string>();

    void Start()
    {
        PlayQuestionSound(startTestAudio);
        InitializeTest();

        // 在遊戲一開始，就為所有問題按鈕加上「移開時取消選定」的腳本
        SetupButtonDeselection(yearButtons);
        SetupButtonDeselection(monthButtons);
        SetupButtonDeselection(dayButtons);
        SetupButtonDeselection(dayOfWeekButtons);
        SetupButtonDeselection(hourButtons);

        if (forceSeasonChange)
        {
            StartCoroutine(CycleAllSeasons());
        }
    }

    /// <summary>
    /// 輔助函式：為一組按鈕陣列中的每個按鈕加上 DeselectOnPointerExit 元件。
    /// </summary>
    /// <param name="buttons">要處理的按鈕陣列</param>
    void SetupButtonDeselection(Button[] buttons)
    {
        if (buttons == null) return;

        foreach (var button in buttons)
        {
            if (button != null && button.GetComponent<DeselectOnPointerExit>() == null)
            {
                button.gameObject.AddComponent<DeselectOnPointerExit>();
            }
        }
    }

    void InitializeTest()
    {
        score = 0;
        currentQuestionIndex = 0;
        correctAnswers = new Dictionary<string, string>();
        playerAnswers.Clear();

        DateTime now = DateTime.Now;

        questions = new List<string> { "Year", "Month", "Day", "DayOfWeek", "Hour" };

        correctAnswers["Year"] = now.Year.ToString();
        string[] monthNames = { "", "1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月", "9月", "10月", "11月", "12月" };
        correctAnswers["Month"] = monthNames[now.Month];
        correctAnswers["Day"] = now.Day.ToString();
        string[] dayNames = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
        correctAnswers["DayOfWeek"] = dayNames[(int)now.DayOfWeek];
        correctAnswers["Hour"] = FormatHourTo12(now.Hour);

        SetSceneryBySeason(GetCurrentSeason(), false);

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
        if (titleText != null) titleText.text = "今天市場有開嗎?";

        Debug.Log("Simple Test Manager initialized");
    }

    #region Options Generation & Helpers

    List<string> GenerateDayOptions()
    {
        DateTime now = DateTime.Now;
        int correctDay = now.Day;
        int daysInMonth = DateTime.DaysInMonth(now.Year, now.Month);
        List<List<int>> weeklyBuckets = new List<List<int>> {
            Enumerable.Range(1, 7).ToList(),
            Enumerable.Range(8, 7).ToList(),
            Enumerable.Range(15, 7).ToList(),
            Enumerable.Range(22, Math.Max(1, daysInMonth - 21)).Where(d => d <= daysInMonth).ToList()
        };
        int correctBucketIndex = -1;
        for (int i = 0; i < weeklyBuckets.Count; i++)
        {
            if (weeklyBuckets[i].Contains(correctDay)) { correctBucketIndex = i; break; }
        }
        List<string> options = new List<string> { correctDay.ToString() };
        for (int i = 0; i < weeklyBuckets.Count; i++)
        {
            if (i == correctBucketIndex) continue;
            if (weeklyBuckets[i].Count > 0)
            {
                int randomDay = weeklyBuckets[i][UnityEngine.Random.Range(0, weeklyBuckets[i].Count)];
                options.Add(randomDay.ToString());
            }
        }
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
        while (options.Count < 4)
        {
            int rH = UnityEngine.Random.Range(0, 24);
            string fH = FormatHourTo12(rH);
            if (!options.Contains(fH)) options.Add(fH);
        }
        ShuffleList(options);
        return options;
    }

    private string FormatHourTo12(int hour)
    {
        if (hour == 0) return "午夜 12:00";
        if (hour < 12) return $"上午 {hour}:00";
        if (hour == 12) return "中午 12:00";
        int pmHour = hour - 12;
        return $"下午 {pmHour}:00";
    }

    private int Parse12HourFormat(string timeString)
    {
        try
        {
            string[] parts = timeString.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 2) return -1;
            string period = parts[0];
            int hour = int.Parse(parts[1].Split(':')[0]);
            if (period == "午夜" && hour == 12) return 0;
            if (period == "中午" && hour == 12) return 12;
            if (period == "上午") return hour;
            if (period == "下午") return (hour == 12) ? 12 : hour + 12;
            return hour;
        }
        catch { return -1; }
    }

    List<string> GenerateYearOptions()
    {
        int y = DateTime.Now.Year;
        var l = new List<string> { y.ToString(), (y - 1).ToString(), (y + 1).ToString(), (y - 2).ToString() };
        ShuffleList(l);
        return l;
    }

    List<string> GenerateMonthOptions()
    {
        string[] m = { "", "1月", "2月", "3月", "4月", "5月", "6月", "7月", "8月", "9月", "10月", "11月", "12月" };
        int cur = DateTime.Now.Month;
        var o = new List<string> { m[cur] };
        var p = Enumerable.Range(1, 12).Where(i => i != cur).ToList();
        for (int i = 0; i < 3; i++) { if (p.Count == 0) break; int r = UnityEngine.Random.Range(0, p.Count); o.Add(m[p[r]]); p.RemoveAt(r); }
        ShuffleList(o);
        return o;
    }

    List<string> GenerateDayOfWeekOptions()
    {
        string[] d = { "星期日", "星期一", "星期二", "星期三", "星期四", "星期五", "星期六" };
        int cur = (int)DateTime.Now.DayOfWeek;
        var o = new List<string> { d[cur] };
        var p = Enumerable.Range(0, 7).Where(i => i != cur).ToList();
        for (int i = 0; i < 3; i++) { if (p.Count == 0) break; int r = UnityEngine.Random.Range(0, p.Count); o.Add(d[p[r]]); p.RemoveAt(r); }
        ShuffleList(o);
        return o;
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
        switch (questionType)
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
            else { buttons[i].gameObject.SetActive(false); }
        }
    }

    void AskNextQuestion()
    {
        selectedAnswer = "";
        if (feedbackText != null) feedbackText.text = "";

        string currentQuestionKey = questions[currentQuestionIndex];
        List<string> options = null;

        switch (currentQuestionKey)
        {
            case "Year":
                options = GenerateYearOptions();
                if (titleText != null) titleText.text = "請問今年是哪一年？";
                PlayQuestionSound(yearQuestionAudio);
                break;
            case "Month":
                options = GenerateMonthOptions();
                if (titleText != null) titleText.text = "現在是幾月呢？";
                PlayQuestionSound(monthQuestionAudio);
                break;
            case "Day":
                options = GenerateDayOptions();
                if (titleText != null) titleText.text = "今天幾號？";
                PlayQuestionSound(dayQuestionAudio);
                break;
            case "DayOfWeek":
                options = GenerateDayOfWeekOptions();
                if (titleText != null) titleText.text = "那今天是星期幾？";
                PlayQuestionSound(dayOfWeekQuestionAudio);
                break;
            case "Hour":
                options = GenerateHourOptions();
                if (titleText != null) titleText.text = "現在大概是什麼時候了？";
                PlayQuestionSound(hourQuestionAudio);
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

        playerAnswers[currentQuestionKey] = selectedAnswer;

        if (isCorrect)
        {
            score++;
            if (feedbackText != null)
            {
                feedbackText.text = "答對了！";
                feedbackText.color = Color.green;
            }
            if (correctSFX != null && audioSource != null) audioSource.PlayOneShot(correctSFX);
        }
        else
        {
            if (feedbackText != null)
            {
                feedbackText.text = "再想想看喔.";
                feedbackText.color = Color.red;
            }
            if (incorrectSFX != null && audioSource != null) audioSource.PlayOneShot(incorrectSFX);
        }

        currentQuestionIndex++;
        HideAllPanels();
        if (confirmButton != null) confirmButton.gameObject.SetActive(false);

        if (IsSceneFinished())
        {
            SaveResultToJson();
            ShowResultPanel();

            string testId = FirebaseManager_Firestore.Instance.testId;
            int levelIndex = 2;
            FirebaseManager.SaveLevelData(testId, levelIndex, score);

            if (SceneFlowManager.instance != null)
            {
                SceneFlowManager.instance.LoadNextScene();
            }
            else
            {
                Debug.LogWarning("SceneFlowManager.instance 為 null，無法切換到下一個場景。");
            }
        }
        else
        {
            Invoke(nameof(AskNextQuestion), 1f);
        }
    }

    private void PlayQuestionSound(AudioClip clip)
    {
        if (audioSource != null && clip != null)
        {
            audioSource.PlayOneShot(clip);
        }
    }

    private void ShowResultPanel()
    {
        Debug.Log($"測驗完成，共 {score} 題正確。");
    }

    [Serializable]
    private class QuestionRecord
    {
        public string Key;
        public string CorrectAnswer;
        public string PlayerAnswer;
    }

    [Serializable]
    private class ResultData
    {
        public string PlayerId;
        public string Timestamp;
        public List<QuestionRecord> Questions;
        public int TotalCorrect;
    }

    private void SaveResultToJson()
    {
        try
        {
            string folderPath = Path.Combine(Application.dataPath, "time_game_data");
            if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);

            var qList = new List<QuestionRecord>();
            foreach (var q in questions)
            {
                string ca = correctAnswers.ContainsKey(q) ? correctAnswers[q] : "";
                string pa = playerAnswers.ContainsKey(q) ? playerAnswers[q] : "";
                qList.Add(new QuestionRecord { Key = q, CorrectAnswer = ca, PlayerAnswer = pa });
            }

            var result = new ResultData
            {
                PlayerId = "001",
                Timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                Questions = qList,
                TotalCorrect = score
            };

            string json = JsonConvert.SerializeObject(result, Formatting.Indented);
            string fileName = DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".json";
            string filePath = Path.Combine(folderPath, fileName);
            File.WriteAllText(filePath, json, Encoding.UTF8);

            Debug.Log($"✅ 測驗結果已儲存：{filePath}");
        }
        catch (Exception ex)
        {
            Debug.LogError($"❌ 儲存 JSON 失敗：{ex.Message}");
        }
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

    #region Public Control Methods

    public void StartTest()
    {
        HideAllPanels();
        AskNextQuestion();
    }

    public void RestartTest()
    {
        if (audioSource != null) audioSource.Stop();
        InitializeTest();
    }

    #endregion

    #region Scenery & Season Methods
    string GetCurrentSeason() { int m = DateTime.Now.Month; if (m >= 3 && m <= 5) return "春天"; if (m >= 6 && m <= 8) return "夏天"; if (m >= 9 && m <= 11) return "秋天"; return "冬天"; }
    void SetAmbienceBySeason(string season) { }
    void SetSceneryBySeason(string season, bool animated = true) { }
    IEnumerator TransitionToSeasonMaterial(int targetSeasonIndex) { yield return null; }
    IEnumerator CycleAllSeasons() { yield return null; }
    #endregion
}