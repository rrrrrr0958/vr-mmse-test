using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

public class QuizManager_6 : MonoBehaviour
{
    private FirebaseManager_Firestore FirebaseManager;

    [Header("診斷輸出")]
    public bool logEvaluationToConsole = true;

    [Header("Feedback")]
    public FeedbackUI_6 feedbackUI;

    [Header("UI")]
    public TextMeshProUGUI questionText;

    [Tooltip("完成後要顯示的面板，可空")]
    public GameObject completionPanel;

    [Header("完成本關後要做的事（保留，可用也可不綁）")]
    public UnityEvent onQuestionCleared;

    [Header("流程設定")]
    [Tooltip("勾選：只要選到任何一個物件，就直接鎖關並進下一關（不管正誤）")]
    public bool advanceOnAnySelection = true;

    [Tooltip("預留：真的要換關時呼叫，之後可換成組員的轉場函式")]
    public UnityEvent onAnySelection; // 例如：LoadNextStage() / 切場景 / 轉 180 度 等

    [Header("進關延遲 / 回饋")]
    [Tooltip("第一次選中之後，維持被圈選的回饋秒數，再進下一關")]
    [Min(0f)] public float postSelectionHoldSeconds = 0.8f;

    [Tooltip("完成面板是否在延遲之後才顯示（預設：是）。若關卡會立即切換，也可關掉避免閃一下。")]
    public bool showCompletionPanelAfterHold = true;

    readonly List<(string prompt, string id)> pool = new()
    {
        ("請用控制器指向桌上的起司並停留3秒", "cheese"),
        ("請用控制器指向桌上的茄子並停留3秒", "eggplant"),
        ("請用控制器指向桌上的麵包並停留3秒",   "bread"),
        ("請用控制器指向桌上的魚肉並停留3秒", "fish"),
    };

    int picked = -1;
    string currentAnswer = "";
    bool isLocked = false;
    Coroutine holdCo;

    void Start() { PickOneQuestion(); }

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
        var all = FindObjectsByType<SelectableTarget_6>(FindObjectsSortMode.None);
#else
        var all = FindObjectsOfType<SelectableTarget_6>(true);
#endif
        foreach (var t in all) if (t.targetId == id) return true;
        return false;
    }

    // ===== 提交流程：給 SelectableTarget 呼叫 =====
    public void Submit(string targetId)
    {
        if (isLocked) return; // 已經鎖定，不再接受答案

        bool ok = targetId == currentAnswer;
        int score = ok ? 1 : 0; 

        if (logEvaluationToConsole)
        {
            string msg = $"[Quiz] Selected='{targetId}'  Answer='{currentAnswer}'  Result={(ok ? "CORRECT" : "WRONG")}";
            if (ok) Debug.Log(msg);
            else Debug.LogWarning(msg); // 錯誤用黃色比較醒目（但不當成 Error）
        }


        // 立即鎖互動：避免在停留期間被再次提交
        isLocked = true;

        // 畫面回饋（此時選到的物件白圈會持續脈動，因為未 ClearAll）
        if (feedbackUI)
        {
            if (ok) feedbackUI.ShowCorrect("答對了！");
            else feedbackUI.ShowWrong("已選取，準備進下一關…");
        }

        // 啟動「選後停留 → 進關」序列
        if (holdCo != null) StopCoroutine(holdCo);
        holdCo = StartCoroutine(HoldThenAdvanceCoroutine(ok));
        string testId = FirebaseManager_Firestore.Instance.testId;
        string levelIndex = "10";
        FirebaseManager_Firestore.Instance.totalScore = FirebaseManager_Firestore.Instance.totalScore + score;
        FirebaseManager_Firestore.Instance.SaveLevelData(testId, levelIndex, score);

        var correctDict = new Dictionary<string, string> { { "option", currentAnswer } };
        var chosenDict = new Dictionary<string, string> { { "option", targetId } };
        FirebaseManager_Firestore.Instance.SaveLevelOptions(testId, levelIndex, correctDict, chosenDict);
        FirebaseManager_Firestore.Instance.SaveTestResult(testId);
        SceneFlowManager.instance.LoadNextScene();
    }

    IEnumerator HoldThenAdvanceCoroutine(bool isCorrect)
    {
        // 避免負數或極小值
        float wait = Mathf.Max(0f, postSelectionHoldSeconds);
        if (wait > 0f)
            yield return new WaitForSeconds(wait);

        // （可選）此時才顯示完成面板作為過場提示
        if (showCompletionPanelAfterHold && completionPanel != null)
            completionPanel.SetActive(true);

        // 清理所有高亮/hover 鎖，避免殘留
        SelectionHighlightRegistry.ClearAll();

        // 進行你預留的換關 Hook（把組員的轉場函式綁在這裡）
        onAnySelection?.Invoke();

        // 若你還有其它需要（沿用舊事件）
        onQuestionCleared?.Invoke();

        holdCo = null;
    }

    // 提供外部查詢是否可互動
    public bool CanInteract() => !isLocked;

    // 外部手動切題可用
    public void SetQuestion(string prompt, string id)
    {
        if (questionText) questionText.text = prompt;
        currentAnswer = id;
    }
}
