// Assets/Scripts/Game/SessionController.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// 場景/瞬移抵達後出題，銜接 QuizPanel 與 RunLogger，支援 VR 鎖移動。
/// </summary>
public class SessionController : MonoBehaviour
{
    [Header("Data")]
    public LocationDB db;

    [Header("UI")]
    public QuizPanel quizPanel;          // 世界空間面板（每場景一個也可，由本類自動重新抓）

    [Header("Logging")]
    public RunLogger logger;

    [Header("Options")]
    [Range(2, 6)] public int optionsPerQuestion = 4;

    // 內部狀態
    PlayerRigMover _mover;
    System.Random _rng;
    List<LocationEntry> _currentOptions = new();
    int _correctIndex = -1;
    bool _quizActive = false;            // 出題/作答中的鎖
    bool _pendingArriveInvoke = false;   // 防重入（sceneLoaded + OnTeleported 同時來）

    void Awake()
    {
        _rng = new System.Random();
        WireSceneRefs();                 // 先嘗試佈線

        // 換場景載入完成 → 重新佈線並出題
        SceneManager.sceneLoaded += OnSceneLoaded;

        logger?.StartRun();
    }

    void Start()
    {
        // 若場景內已經站在某 VP，進場先出一次題（防 sceneLoaded 時序）
        SafeInvokeArrived();
    }

    void OnDestroy()
    {
        UnwireMover();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        logger?.EndRun();
    }

    // ========= 佈線 / 解佈線 =========
    void WireSceneRefs()
    {
        // 重新抓 PlayerRigMover（跨場景後物件會重建）
        var mover = FindFirstObjectByType<PlayerRigMover>();
        if (mover != _mover)
        {
            UnwireMover();
            _mover = mover;
            if (_mover) _mover.OnTeleported.AddListener(OnArrived);
        }

        // 重新抓 QuizPanel（每個場景可各自放一個）
        if (!quizPanel) quizPanel = FindFirstObjectByType<QuizPanel>(FindObjectsInactive.Exclude);
    }

    void UnwireMover()
    {
        if (_mover)
        {
            _mover.OnTeleported.RemoveListener(OnArrived);
            _mover = null;
        }
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        WireSceneRefs();
        SafeInvokeArrived();
    }

    // ========= 出題主流程 =========
    void SafeInvokeArrived()
    {
        // 避免 Start/sceneLoaded 連續觸發兩次
        if (_pendingArriveInvoke) return;
        _pendingArriveInvoke = true;
        // 讓引擎跑一幀再做，避免同一幀抓不到剛生成的 UI
        StartCoroutine(InvokeNextFrame(() =>
        {
            _pendingArriveInvoke = false;
            OnArrived();
        }));
    }

    System.Collections.IEnumerator InvokeNextFrame(System.Action act)
    {
        yield return null;
        act?.Invoke();
    }

    void OnArrived()
    {
        if (_quizActive) return;                 // 正在作答就不重複出題
        if (!EnsureDbOrFallback()) return;       // 檢查 DB / fallback 題目
        if (!EnsureQuizPanelOrWarn()) return;    // 檢查面板是否存在

        string sceneName = SceneManager.GetActiveScene().name;

        // 依玩家最近 VP 推定
        string vpName = GuessVPNameByNearest(sceneName);
        AskQuestion(sceneName, vpName);
    }

    // 供「攤販 Trigger」直接呼叫（若你要改成靠近攤販才出題）
    public void StartQuizByStallId(string stallId)
    {
        if (_quizActive) return;
        if (!EnsureDbOrFallback()) return;
        if (!EnsureQuizPanelOrWarn()) return;

        var correct = db.FindByStallId(stallId);
        if (correct == null)
        {
            Debug.LogWarning($"[Session] StartQuizByStallId 找不到 stallId={stallId}，改回到 OnArrived 自動推定。");
            OnArrived();
            return;
        }
        AskQuestion(correct.sceneName, correct.viewpointName);
    }

    void AskQuestion(string sceneName, string vpName)
    {
        var correct = db?.FindBySceneAndVP(sceneName, vpName);

        // 防呆：當前場景沒有對應 vp，回退到本場景第一筆或整庫第一筆
        if (correct == null)
        {
            correct = db.AllByScene(sceneName).FirstOrDefault() ?? db.entries.FirstOrDefault();
            if (correct == null)
            {
                Debug.LogWarning("[Session] DB 內沒有任何條目，無法出題。");
                return;
            }
        }

        // 建立選項並裁切到 QuizPanel 容量
        int panelCapacity = quizPanel ? quizPanel.MaxOptions : optionsPerQuestion;
        int want = Mathf.Clamp(optionsPerQuestion, 2, Mathf.Max(2, panelCapacity));
        _currentOptions = BuildOptions(correct, want);
        _correctIndex = _currentOptions.IndexOf(correct);

        var labels = _currentOptions.Select(e => e.displayText).ToArray();

        // 鎖移動 & 顯示題面
        _quizActive = true;
        if (_mover) _mover.allowMove = false;

        quizPanel.Show("你現在在哪裡？", labels, OnPick);

        // 紀錄題目起點
        logger?.SetDisplayTextCache(correct.displayText);
        logger?.BeginQuestion(sceneName, correct.viewpointName, correct.displayText);
    }

    // ========= 作答回呼 =========
    void OnPick(int idx)
    {
        string sceneName = SceneManager.GetActiveScene().name;

        bool isCorrect = (idx == _correctIndex);
        string userKey = (idx >= 0 && idx < _currentOptions.Count)
            ? _currentOptions[idx].viewpointName
            : "";

        logger?.EndQuestion(userKey, isCorrect, 0);

        // 收面板 & 解鎖移動
        if (quizPanel) quizPanel.Hide();
        if (_mover) _mover.allowMove = true;

        _quizActive = false;

        // 需要的話可在這裡安排下一題/導覽（目前保持停留）
        // SafeInvokeArrived(); // 若想答完立即下一題
    }

    // ========= 工具方法 =========
    /// <summary>依玩家位置，在目前場景的條目中尋找最近的 Viewpoint 名稱；找不到物件則回退第一筆名稱。</summary>
    string GuessVPNameByNearest(string sceneName)
    {
        var candidates = db.AllByScene(sceneName).ToList();
        if (candidates.Count == 0) return "";

        Transform rig = _mover ? _mover.transform : null;
        if (!rig) return candidates[0].viewpointName;

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
        if (db == null || db.entries == null || db.entries.Count == 0)
            return new List<LocationEntry>();

        // 先收集候選
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

        // 可能少於 count（資料庫不足），照現有數量洗牌即可
        return list.OrderBy(_ => _rng.Next()).ToList();
    }

    bool EnsureDbOrFallback()
    {
        if (db != null && db.entries != null && db.entries.Count > 0)
            return true;

        // Fallback：DB 尚未填資料 → 用暫時題目先跑流程
        if (!EnsureQuizPanelOrWarn()) return false;

        var labels = new[] { "一樓 A 攤", "二樓 B 攤", "一樓 B 攤", "三樓 D 攤" };
        int cap = Mathf.Max(2, Mathf.Min(labels.Length, quizPanel ? quizPanel.MaxOptions : labels.Length));
        var trimmed = labels.Take(cap).ToArray();

        _quizActive = true;
        if (_mover) _mover.allowMove = false;

        quizPanel.Show("你現在在哪裡？", trimmed, idx =>
        {
            if (quizPanel) quizPanel.Hide();
            if (_mover) _mover.allowMove = true;
            _quizActive = false;
        });

        Debug.LogWarning("[Session] DB 為空，使用暫時題目。請建立 LocationDB 以啟用正式流程。");
        return false;
    }

    bool EnsureQuizPanelOrWarn()
    {
        if (quizPanel) return true;
        quizPanel = FindFirstObjectByType<QuizPanel>(FindObjectsInactive.Exclude);
        if (quizPanel) return true;

        Debug.LogWarning("[Session] 找不到 QuizPanel。請確保場景內有世界空間面板並指派。");
        return false;
    }
}
