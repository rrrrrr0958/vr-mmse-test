using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class QuizManager : MonoBehaviour
{
    [Header("Feedback")]
    public FeedbackUI feedbackUI;

    [Header("UI")]
    public TextMeshProUGUI questionText;
    [Tooltip("完成後要顯示的面板，可空")]
    public GameObject completionPanel;

    [Header("完成本關後要做的事（保留，可用也可不綁）")]
    public UnityEvent onQuestionCleared;

    // 題庫（題目文字 與 對應正解 targetId 必須一一對齊）
    readonly List<(string prompt, string id)> pool = new()
    {
        ("請用控制器指向桌上的起司並停留5秒", "cheese"),
        ("請用控制器指向桌上的香腸並停留5秒", "sausage"),
        ("請用控制器指向桌上的碗並停留5秒",   "bowl"),
        ("請用控制器指向桌上的肉排並停留5秒", "meat"),
    };

    int picked = -1;            // 這次抽到第幾題
    string currentAnswer = "";  // 這題正解的 targetId
    bool isLocked = false;      // 是否鎖定互動

    void Start()
    {
        PickOneQuestion();
    }

    void PickOneQuestion()
    {
        int last = PlayerPrefs.GetInt("last_quiz_idx", -1);

        if (pool.Count == 0)
        {
            if (questionText) questionText.text = "（題庫是空的）";
            return;
        }

        int tries = 10;
        do { picked = Random.Range(0, pool.Count); }
        while (picked == last && pool.Count > 1 && --tries > 0);

        PlayerPrefs.SetInt("last_quiz_idx", picked);

        var q = pool[picked];
        currentAnswer = q.id;
        if (questionText) questionText.text = q.prompt;

        // 抽到場景不存在目標就換題（一次嘗試）
        if (!TargetExistsInScene(currentAnswer))
        {
            for (int i = 0; i < pool.Count; i++)
            {
                int alt = (picked + 1 + i) % pool.Count;
                if (TargetExistsInScene(pool[alt].id))
                {
                    picked = alt;
                    currentAnswer = pool[alt].id;
                    if (questionText) questionText.text = pool[alt].prompt;
                    PlayerPrefs.SetInt("last_quiz_idx", picked);
                    break;
                }
            }
        }
    }

    bool TargetExistsInScene(string id)
    {
#if UNITY_2022_2_OR_NEWER
        var all = FindObjectsByType<SelectableTarget>(FindObjectsSortMode.None);
#else
        var all = FindObjectsOfType<SelectableTarget>(true);
#endif
        foreach (var t in all) if (t.targetId == id) return true;
        return false;
    }

    // ===== 提交流程：給 SelectableTarget 呼叫 =====
    public void Submit(string targetId)
    {
        if (isLocked) return; // 已經鎖定，不再接受答案

        bool ok = targetId == currentAnswer;

        // ★ 顯示對錯到畫面 ★
        if (feedbackUI)
        {
            if (ok)
                feedbackUI.ShowCorrect("答對了！");
            else
                feedbackUI.ShowWrong("答錯了！繼續加油");
        }

        if (ok)
        {
            LockAndComplete();
            onQuestionCleared?.Invoke();
        }
    }

    void LockAndComplete()
    {
        isLocked = true;

        // 顯示完成面板
        if (completionPanel != null)
            completionPanel.SetActive(true);
    }

    // 提供外部查詢是否可互動（取代原本的 GameDirector.Instance.CanInteractGame1）
    public bool CanInteract() => !isLocked;

    // 若要外部手動切題可用
    public void SetQuestion(string prompt, string id)
    {
        if (questionText) questionText.text = prompt;
        currentAnswer = id;
    }
}
