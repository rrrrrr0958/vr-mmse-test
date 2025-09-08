// Assets/Scripts/Game/SessionController.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 抵達（瞬移/換場）後自動出題；接 QuizPanel；寫 RunLogger。
/// 相容你的 QuizPanel(Action<int>) 寫法。
/// </summary>
public class SessionController : MonoBehaviour
{
    [Header("Data")]
    public LocationDB db;

    [Header("UI")]
    public QuizPanel quizPanel;          // 你的 QuizPanel.cs（固定按鈕陣列版）

    [Header("Logging")]
    public RunLogger logger;

    [Header("Options")]
    [Range(2, 6)] public int optionsPerQuestion = 4;

    // 內部狀態
    private PlayerRigMover _mover;
    private System.Random _rng;
    private List<LocationEntry> _currentOptions = new();
    private int _correctIndex = -1;

    // ===== Unity lifecycle =====
    void Awake()
    {
        _rng = new System.Random();

        // 同場景瞬移結束 → 出題
        _mover = UnityEngine.Object.FindFirstObjectByType<PlayerRigMover>();
        if (_mover) _mover.OnTeleported.AddListener(OnArrived);

        // 換場景載入完成 → 出題
        SceneManager.sceneLoaded += OnSceneLoaded;

        // 開始一個 run
        logger?.StartRun();
    }

    void Start()
    {
        // 有些情況 sceneLoaded 訂閱發生在之後；保險：一進場就先出一次題
        OnArrived();
    }

    void OnDestroy()
    {
        if (_mover) _mover.OnTeleported.RemoveListener(OnArrived);
        SceneManager.sceneLoaded -= OnSceneLoaded;

        // 結束本次 run（若無紀錄不會輸出）
        logger?.EndRun();
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        OnArrived();
    }

    // ===== 出題主流程 =====
    void OnArrived()
    {
        var sceneName = SceneManager.GetActiveScene().name;
        // Debug.Log($"[Session] OnArrived scene={sceneName}");

        // Fallback：DB 尚未填資料 → 用暫時題目先跑流程
        if (db == null || db.entries == null || db.entries.Count == 0)
        {
            var labels = new[] { "一樓 麵包攤", "二樓 魚攤", "二樓 肉攤", "地下一樓 入口" };
            quizPanel.Show("你現在在哪裡？", labels, idx =>
            {
                // 這裡先不記錄 logger；等 DB 建好後，自然走正式流程
                // Debug.Log($"[Session] TEMP answer idx={idx}");
            });
            return;
        }

        // 以最近 VP 偵測（若找不到對應物件則回退至第一筆）
        var vpName = GuessVPNameByNearest(sceneName);
        AskQuestion(sceneName, vpName);
    }

    void AskQuestion(string sceneName, string vpName)
    {
        if (db == null || db.entries == null || db.entries.Count == 0) return;

        var correct = db.FindBySceneAndVP(sceneName, vpName);

        // 防呆：當前場景沒有對應 vp，回退到本場景第一筆或整庫第一筆
        if (correct == null)
        {
            correct = db.AllByScene(sceneName).FirstOrDefault() ?? db.entries.First();
        }

        _currentOptions = BuildOptions(correct, optionsPerQuestion);
        _correctIndex = _currentOptions.IndexOf(correct);

        var labels = _currentOptions.Select(e => e.displayText).ToArray();

        // 顯示題面（相容你現有的 QuizPanel.Show(title, string[], Action<int>)）
        quizPanel.Show("你現在在哪裡？", labels, OnPick);

        // 通知 logger：開始一題
        logger?.SetDisplayTextCache(correct.displayText);
        logger?.BeginQuestion(sceneName, correct.viewpointName, correct.displayText);
    }

    // ===== 作答回呼 =====
    void OnPick(int idx)
    {
        var sceneName = SceneManager.GetActiveScene().name;

        bool isCorrect = (idx == _correctIndex);
        string userKey = (idx >= 0 && idx < _currentOptions.Count)
            ? _currentOptions[idx].viewpointName
            : "";

        // 目前未從 QuizPanel 回傳 RT，先填 0；之後可升級成 (idx, rtMs)
        logger?.EndQuestion(userKey, isCorrect, 0);

        // （可選）顯示回饋＋自動關閉
        // quizPanel.ShowFeedback(isCorrect);
        // StartCoroutine(CloseLater(1f));
    }

    // ===== 工具方法 =====
    /// <summary>依玩家位置，在目前場景的 DB 條目中尋找名稱相符的 Viewpoint 物件，挑最近的一個；若找不到物件，回傳第一筆的名稱。</summary>
    string GuessVPNameByNearest(string sceneName)
    {
        var candidates = db.AllByScene(sceneName).ToList();
        if (candidates.Count == 0) return "";

        var rig = _mover ? _mover.transform : null;
        if (rig == null) return candidates[0].viewpointName;

        float best = float.MaxValue;
        string bestName = candidates[0].viewpointName;

        foreach (var e in candidates)
        {
            var go = GameObject.Find(e.viewpointName);
            if (!go) continue;
            float d = Vector3.SqrMagnitude(go.transform.position - rig.position);
            if (d < best)
            {
                best = d;
                bestName = e.viewpointName;
            }
        }
        return bestName;
    }

    /// <summary>組合選項：正解 + 同場景優先的干擾項，不足再用其他場景；最後洗牌。</summary>
    List<LocationEntry> BuildOptions(LocationEntry correct, int count)
    {
        var same = db.entries.Where(e => e.sceneName == correct.sceneName && e != correct)
                             .OrderBy(_ => _rng.Next());
        var others = db.entries.Where(e => e.sceneName != correct.sceneName)
                               .OrderBy(_ => _rng.Next());

        var list = new List<LocationEntry> { correct };
        foreach (var e in same.Concat(others))
        {
            if (list.Count >= count) break;
            if (!list.Contains(e)) list.Add(e);
        }

        // 洗牌
        return list.OrderBy(_ => _rng.Next()).ToList();
    }

    // 可選：作答後延遲關閉面板
    // System.Collections.IEnumerator CloseLater(float delay)
    // {
    //     yield return new WaitForSeconds(delay);
    //     quizPanel.Hide();
    // }
}
