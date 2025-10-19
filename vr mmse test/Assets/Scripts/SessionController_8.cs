  // Assets/Scripts/Game/SessionController.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using System;

[DefaultExecutionOrder(100)]
public class SessionController : MonoBehaviour
{
    private FirebaseManager_Firestore FirebaseManager;

    [Header("Data")]
    public LocationDB db;

    [Header("UI")]
    public QuizPanel quizPanel; // 可留空，會自動尋找（XR Origin 下優先）
    [Tooltip("指到 NavPanel 根物件上的 CanvasGroup（不指定也會自動尋找/補上）。此物件會被整體顯示/隱藏。")]
    [SerializeField] CanvasGroup navPanelGroup;

    [Header("Nav Panel 行為")]
    [Tooltip("作答後是否恢復顯示 NavPanel（左/右轉等）。設為 false = 作答後仍保持隱藏。")]
    public bool showNavPanelAfterAnswer = false;
    [Tooltip("當本元件被停用/換場時，下一個場景是否要自動顯示 NavPanel。")]
    public bool forceShowNavPanelOnDisable = true;

    [Header("Logging")]
    public RunLogger logger;
    [Tooltip("若場景內找不到 RunLogger，是否自動建立一顆常駐的？")]
    public bool autoCreateLoggerIfMissing = true;

    [Header("Options")]
    [Range(2, 6)] public int optionsPerQuestion = 4;

    // ===== Internal =====
    PlayerRigMover _mover;
    System.Random _rng = new System.Random();
    List<LocationEntry> _currentOptions = new();
    int _correctIndex = -1;
    bool _quizActive = false;
    bool _boundTargetEvent = false;

    // NavPanel 期望顯示狀態（避免在 OnDisable 階段 SetActive 造成錯誤）
    bool _navPanelVisibleDesired = true;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SafeBindMover("OnEnable");
        RewireQuizPanelIfNeeded();
        TryWireNavPanelGroup();          // 自動掛線或補 CanvasGroup
        RewireLoggerIfNeeded();
        logger?.StartRun();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindMover();
        logger?.EndRun();

        // 只更新「期望狀態」旗幟，不在停用中呼叫 SetActive
        if (forceShowNavPanelOnDisable) _navPanelVisibleDesired = true;

        if (quizPanel) quizPanel.Hide();
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        UnbindMover();
        SafeBindMover("sceneLoaded");
        RewireQuizPanelIfNeeded();
        TryWireNavPanelGroup();          // 切樓層後重綁 NavPanelGroup
        RewireLoggerIfNeeded();

        // 場景載入完成後，統一套用 NavPanel 期望狀態（避免 OnDisable 期間切換）
        ApplyNavPanelDesiredState();
    }

    public void StartQuizByStallId(string stallId)
    {
        if (_quizActive) return;
        if (!RewireQuizPanelIfNeeded()) return;
        if (!EnsureDbOrWarn()) return;

        var correct = db != null ? db.FindByStallId(stallId) : null;
        if (correct == null)
        {
            Debug.LogWarning($"[Session] StartQuizByStallId: 找不到 stallId={stallId}，取消此次出題。");
            return;
        }
        AskQuestion(correct.sceneName, correct.viewpointName);
    }

    // ===== 綁定 mover（僅使用帶 Transform 的事件）=====
    void SafeBindMover(string from)
    {
        if (_mover == null) _mover = FindFirstObjectByType<PlayerRigMover>(FindObjectsInactive.Exclude);
        if (_mover)
        {
            if (!_boundTargetEvent)
            {
                _mover.OnTeleportedTarget -= HandleTeleportedTarget;
                _mover.OnTeleportedTarget += HandleTeleportedTarget;
                _boundTargetEvent = true;
                Debug.Log($"[Session] Bound to mover.OnTeleportedTarget ({from})");
            }
        }
        else
        {
            Debug.LogWarning($"[Session] No PlayerRigMover found ({from}) — will retry next frame.");
            StartCoroutine(RetryBindNextFrame());
        }
    }
    System.Collections.IEnumerator RetryBindNextFrame() { yield return null; SafeBindMover("retry-next-frame"); }

    void UnbindMover()
    {
        if (_mover != null && _boundTargetEvent)
        {
            _mover.OnTeleportedTarget -= HandleTeleportedTarget;
        }
        _mover = null;
        _boundTargetEvent = false;
        Debug.Log("[Session] Unbound from mover.");
    }

    // ===== QuizPanel 查找（XR Origin 下優先）=====
    bool RewireQuizPanelIfNeeded()
    {
        if (quizPanel && quizPanel.gameObject) return true;

        var xr = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Include);
        if (xr)
        {
            var qp = xr.GetComponentInChildren<QuizPanel>(includeInactive: true);
            if (qp) { quizPanel = qp; return true; }
        }
        var found = FindFirstObjectByType<QuizPanel>(FindObjectsInactive.Include);
        if (found) { quizPanel = found; return true; }

        Debug.LogWarning("[Session] 找不到 QuizPanel。請在 XR Origin 底下放 World Space 的 QuizPanel。");
        return false;
    }

    // ===== RunLogger 查找/自建 =====
    bool RewireLoggerIfNeeded()
    {
        if (logger) return true;

        var found = FindFirstObjectByType<RunLogger>(FindObjectsInactive.Include);
        if (found) { logger = found; return true; }

        if (autoCreateLoggerIfMissing)
        {
            var go = new GameObject("RunLogger");
            logger = go.AddComponent<RunLogger>();
            DontDestroyOnLoad(go);
            Debug.Log("[Session] 自動建立 RunLogger 並設為常駐。");
            return true;
        }
        Debug.LogWarning("[Session] 找不到 RunLogger（且未啟用自動建立）。將不記錄此次作答。");
        return false;
    }

    // ===== 事件回呼：只有瞬移完成才出題 =====
    void HandleTeleportedTarget(Transform vp)
    {
        if (_quizActive) return;
        if (!RewireQuizPanelIfNeeded()) return;
        if (!EnsureDbOrWarn()) return;

        string sceneName = SceneManager.GetActiveScene().name;
        string vpName = vp ? vp.name : GuessVPNameByNearest(sceneName);
        AskQuestion(sceneName, vpName);
    }
    // 供外部（FloorNavNode / TransitionManager）通知到達用
    public void OnArrivedAtViewpoint(Transform vp)
    {
        HandleTeleportedTarget(vp);   // 直接沿用你原本的流程：
                                    // AskQuestion(...)、HideNavPanel() 都會被執行
    }

    void AskQuestion(string sceneName, string vpName)
    {
        var correct = db?.FindBySceneAndVP(sceneName, vpName);
        if (correct == null)
        {
            correct = db.AllByScene(sceneName).FirstOrDefault() ?? db.entries.FirstOrDefault();
            if (correct == null) { Debug.LogWarning("[Session] DB 空，無法出題。"); return; }
            Debug.LogWarning($"[Session] 找不到 VP '{vpName}'，回退：{correct.displayText}");
        }

        int panelCapacity = quizPanel ? quizPanel.MaxOptions : optionsPerQuestion;
        int want = Mathf.Clamp(optionsPerQuestion, 2, Mathf.Max(2, panelCapacity));
        _currentOptions = BuildOptions(correct, want);
        _correctIndex = _currentOptions.IndexOf(correct);

        var labels = _currentOptions.Select(e =>
            string.IsNullOrEmpty(e.displayText) ? $"{e.floorLabel} {e.stallLabel}".Trim() : e.displayText
        ).ToArray();

        Debug.Log("[Session] labels count = " + labels.Length);
        _quizActive = true;
        if (_mover) _mover.allowMove = false;

        // HUD 到相機前
        var cam = Camera.main ? Camera.main.transform : (_mover ? _mover.cameraTransform : null);
        if (quizPanel && cam) quizPanel.PlaceHudInFront(cam, 2.0f, 0f);

        quizPanel.Show("你現在在哪裡?", labels, OnPick);

        // 出題時讓 NavPanel 直接消失
        HideNavPanel();

        logger?.SetDisplayTextCache(correct.displayText);
        logger?.BeginQuestion(sceneName, correct.viewpointName, correct.displayText);
    }

    void OnPick(int idx)
    {
        bool isCorrect = (idx == _correctIndex);
        string userKey = (idx >= 0 && idx < _currentOptions.Count) ? _currentOptions[idx].viewpointName : "";
        logger?.EndQuestion(userKey, isCorrect, 0);

        if (quizPanel) quizPanel.Hide();
        if (_mover) _mover.allowMove = true;

        // 依設定決定是否恢復 NavPanel（預設：不顯示）
        if (showNavPanelAfterAnswer) ShowNavPanel();

        _quizActive = false;
        string testId = FirebaseManager_Firestore.Instance.testId;
        string levelIndex = "9";
        FirebaseManager.SaveLevelData(testId, levelIndex, 5);//這裡的分數在哪?
        SceneFlowManager.instance.LoadNextScene();
    }

    // ===== utils =====
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
            float d = (go.transform.position - rig.position).sqrMagnitude;
            if (d < best) { best = d; bestName = e.viewpointName; }
        }
        return bestName;
    }

    List<LocationEntry> BuildOptions(LocationEntry correct, int count)
    {
        if (db == null || db.entries == null || db.entries.Count == 0) return new List<LocationEntry>();
        var same = db.entries.Where(e => e.sceneName == correct.sceneName && e != correct).OrderBy(_ => _rng.Next());
        var others = db.entries.Where(e => e.sceneName != correct.sceneName).OrderBy(_ => _rng.Next());

        var list = new List<LocationEntry> { correct };
        foreach (var e in same.Concat(others))
        {
            if (list.Count >= count) break;
            if (!list.Contains(e)) list.Add(e);
        }
        return list.OrderBy(_ => _rng.Next()).ToList();
    }

    bool EnsureDbOrWarn()
    {
        if (db != null && db.entries != null && db.entries.Count > 0) return true;
        Debug.LogWarning("[Session] LocationDB 為空，瞬移後不會出題。");
        return false;
    }

    // ===== NavPanel 顯示/隱藏（整個物件顯示/消失） =====
    void HideNavPanel()
    {
        _navPanelVisibleDesired = false;
        if (!navPanelGroup) return;

        var go = navPanelGroup.gameObject;
        // 只有在目前可安全切換時才立即 SetActive
        if (go.activeInHierarchy && go.activeSelf)
            go.SetActive(false);
    }

    void ShowNavPanel()
    {
        _navPanelVisibleDesired = true;
        if (!navPanelGroup) return;

        var go = navPanelGroup.gameObject;
        if (go.activeInHierarchy && !go.activeSelf)
            go.SetActive(true);
    }

    void ApplyNavPanelDesiredState()
    {
        if (!navPanelGroup) return;
        var go = navPanelGroup.gameObject;
        if (_navPanelVisibleDesired)
        {
            if (!go.activeSelf) go.SetActive(true);
        }
        else
        {
            if (go.activeSelf) go.SetActive(false);
        }
    }

    bool TryWireNavPanelGroup()
    {
        if (navPanelGroup) return true;

        // 先嘗試從 NavPanel 腳本找
        var nav = FindFirstObjectByType<NavPanel>(FindObjectsInactive.Include);
        if (nav)
        {
            navPanelGroup = nav.GetComponent<CanvasGroup>();
            if (!navPanelGroup) navPanelGroup = nav.gameObject.AddComponent<CanvasGroup>();
            return true;
        }

        // 退而求其次：用名稱找物件
        var go = GameObject.Find("NavPanel");
        if (go)
        {
            navPanelGroup = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            return true;
        }
        return false;
    }
}
