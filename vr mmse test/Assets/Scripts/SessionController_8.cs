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

    [Header("選項數量")]
    [Range(3, 6)] public int optionsPerQuestion = 3; // 三階段都用三選

    [Header("三階段題目設定")]
    [Tooltip("第一階段：類別題目的標題")]
    public string stage1Title = "這裡是哪一類場所？";
    [Tooltip("第二階段：樓層題目的標題")]
    public string stage2Title = "你在第幾樓？";
    [Tooltip("第三階段：攤位題目的標題")]
    public string stage3Title = "你現在在哪個攤位？";

    [Tooltip("第一階段類別的正解（例如：市集）")]
    public string stage1CorrectCategory = "市集";

    // ===== 修正 #1：將 "市集" 加入預設干擾池 =====
    [Tooltip("第一階段類別的干擾選項池（會隨機挑兩個組合）")]
    public string[] stage1DistractorPool = new[] { "市集", "動物園", "遊樂園", "博物館", "校園", "海邊", "車站" };
    // ============================================

    [Tooltip("正確判定模式：AllStages=三階段都要對；StallOnly=只看第三階段攤位是否正確")]
    public CorrectMode finalCorrectMode = CorrectMode.AllStages;
    public enum CorrectMode { AllStages, StallOnly }

    [Header("Logging")]
    public RunLogger logger;
    [Tooltip("若場景內找不到 RunLogger，是否自動建立一顆常駐的？")]
    public bool autoCreateLoggerIfMissing = true;

    // ===== Internal =====
    PlayerRigMover _mover;
    System.Random _rng = new System.Random();
    List<LocationEntry> _currentOptions = new(); // 僅供第三階段（攤位）使用
    string[] _currentStringOptions;              // 儲存當前顯示的字串選項
    int _correctIndex = -1;
    bool _quizActive = false;
    bool _boundTargetEvent = false;

    // NavPanel 期望顯示狀態（避免在 OnDisable 階段 SetActive 造成錯誤）
    bool _navPanelVisibleDesired = true;

    // ===== 三階段狀態 =====
    enum Stage { None, Category, Floor, Stall, Done }
    Stage _stage = Stage.None;

    LocationEntry _correctEntry;  // 當前視角對應的正解
    string _pickedCategory = "";
    string _pickedFloor = "";
    bool _stage1Ok = false;
    bool _stage2Ok = false;

    void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
        SafeBindMover("OnEnable");
        RewireQuizPanelIfNeeded();
        TryWireNavPanelGroup();
        RewireLoggerIfNeeded();
        logger?.StartRun();
    }

    void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        UnbindMover();
        logger?.EndRun();

        if (forceShowNavPanelOnDisable) _navPanelVisibleDesired = true;
        if (quizPanel) quizPanel.Hide();
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        UnbindMover();
        SafeBindMover("sceneLoaded");
        RewireQuizPanelIfNeeded();
        TryWireNavPanelGroup();
        RewireLoggerIfNeeded();
        ApplyNavPanelDesiredState();
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

    // ===== 事件回呼：瞬移完成才出題（改為三階段流程） =====
    void HandleTeleportedTarget(Transform vp)
    {
        if (_quizActive) return;
        if (!RewireQuizPanelIfNeeded()) return;
        if (!EnsureDbOrWarn()) return;

        string sceneName = SceneManager.GetActiveScene().name;
        string vpName = vp ? vp.name : GuessVPNameByNearest(sceneName);

        _correctEntry = db?.FindBySceneAndVP(sceneName, vpName);
        if (_correctEntry == null)
        {
            _correctEntry = db.AllByScene(sceneName).FirstOrDefault() ?? db.entries.FirstOrDefault();
            if (_correctEntry == null) { Debug.LogWarning("[Session] DB 空，無法出題。"); return; }
            Debug.LogWarning($"[Session] 找不到 VP '{vpName}'，回退：{_correctEntry.displayText}");
        }
        BeginThreeStageQuiz();
    }

    // ===== 三階段入口 =====
    void BeginThreeStageQuiz()
    {
        _quizActive = true;
        _stage = Stage.Category;
        _pickedCategory = "";
        _pickedFloor = "";
        _stage1Ok = false;
        _stage2Ok = false;

        if (_mover) _mover.allowMove = false;
        HideNavPanel();

        var cam = (_mover && _mover.cameraTransform) ? _mover.cameraTransform : (Camera.main ? Camera.main.transform : null);
        if (quizPanel && cam) quizPanel.PlaceHudInFront(cam, 2.1f, -0.18f);

        logger?.SetDisplayTextCache(_correctEntry.displayText);
        logger?.BeginQuestion(_correctEntry.sceneName, _correctEntry.viewpointName, _correctEntry.displayText);

        // 類別題
        var options = BuildStage1Options(out _correctIndex);
        _currentStringOptions = options; // 儲存選項
        quizPanel.Show(stage1Title, _currentStringOptions, OnPick);
    }

    // ===== OnPick：依階段進行 =====
    void OnPick(int idx)
    {
        switch (_stage)
        {
            case Stage.Category: HandleStage1(idx); break;
            case Stage.Floor: HandleStage2(idx); break;
            case Stage.Stall: HandleStage3(idx); break;
        }
    }

    // ---------- 外部觸發 (FloorNavNode / StallTrigger) ----------
    public void OnArrivedAtViewpoint(Transform vp)
    {
        HandleTeleportedTarget(vp);
    }
    public void StartQuizByStallId(string stallId)
    {
        if (_quizActive) return;
        if (!RewireQuizPanelIfNeeded()) return;
        if (!EnsureDbOrWarn()) return;

        _correctEntry = db != null ? db.FindByStallId(stallId) : null;
        if (_correctEntry == null)
        {
            Debug.LogWarning($"[Session] StartQuizByStallId: 找不到 stallId={stallId}，取消此次出題。");
            return;
        }
        BeginThreeStageQuiz();
    }

    // ---------- Stage 1：場所類別 ----------
    void HandleStage1(int idx)
    {
        // (修正點：不再重新 Build，使用 _currentStringOptions)
        _stage1Ok = (idx == _correctIndex); 
        _pickedCategory = _currentStringOptions[Mathf.Clamp(idx, 0, _currentStringOptions.Length - 1)];

        // 進 Stage 2
        _stage = Stage.Floor;
        var floorOptions = BuildStage2Options(out _correctIndex);
        _currentStringOptions = floorOptions; // 儲存 Stage 2 的選項
        quizPanel.Show(stage2Title, _currentStringOptions, OnPick);
    }

    // ---------- Stage 2：樓層 ----------
    // ---------- Stage 2：樓層 ----------
    void HandleStage2(int idx)
    {
        // (處理 Stage 2 樓層選擇的邏輯 - 保持不變)
        _stage2Ok = (idx == _correctIndex);
        _pickedFloor = _currentStringOptions[Mathf.Clamp(idx, 0, _currentStringOptions.Length - 1)];
        _currentStringOptions = null; 

        // 進 Stage 3
        _stage = Stage.Stall;

        // (建立 Stage 3 選項 - 保持不變)
        _currentOptions = BuildStage3StallOptions(_correctEntry, out _correctIndex);
        
        
        // ===== 修正：優先使用 displayText =====
        // 你的 displayText 已經是乾淨的攤位名稱，所以我們優先使用它。
        // 只有當 displayText 為空時，才不得已退回(fallback)使用 stallLabel。
        var labels = _currentOptions.Select(e => {
            string display = (e.displayText ?? "").Trim();
            return !string.IsNullOrEmpty(display) ? display : (e.stallLabel ?? "").Trim();
        }).ToArray();
        // ======================================
        

        quizPanel.Show(stage3Title, labels, OnPick);
    }
    // ---------- Stage 3：攤位（最終計分與結束） ----------
    void HandleStage3(int idx)
    {
        bool stallOk = (idx == _correctIndex);

        bool finalOk = (finalCorrectMode == CorrectMode.AllStages)
            ? (_stage1Ok && _stage2Ok && stallOk)
            : stallOk;

        string selectedKey = (idx >= 0 && idx < _currentOptions.Count) ? _currentOptions[idx].viewpointName : "";
        string selectedDisplay = (idx >= 0 && idx < _currentOptions.Count) ? _currentOptions[idx].displayText : "";

        logger?.EndThreeStageQuestion(
            categoryCorrect: stage1CorrectCategory,
            categoryChosen: _pickedCategory,
            categoryIsCorrect: _stage1Ok,
            
            floorCorrect: NormalizeFloorLabel(_correctEntry.floorLabel),
            floorChosen: _pickedFloor,
            floorIsCorrect: _stage2Ok,
            
            stallChosenKey: selectedKey,
            stallChosenDisplay: selectedDisplay, 
            stallIsCorrect: stallOk,
            
            finalCorrect: finalOk
        );

        if (quizPanel) quizPanel.Hide();
        if (_mover) _mover.allowMove = true;
        if (showNavPanelAfterAnswer) ShowNavPanel();

        _stage = Stage.Done;
        _quizActive = false;

        SceneFlowManager.instance.LoadNextScene();
    }

    // ====== Stage 1 Builders ======
    string[] BuildStage1Options(out int correctIndex)
    {
        // 1. 確定正解 (如果 Inspector 為空，預設為 "市集")
        string correctCategory = string.IsNullOrWhiteSpace(stage1CorrectCategory) ? "市集" : stage1CorrectCategory.Trim();
        
        // 2. 將正解放入 Set
        var set = new HashSet<string> { correctCategory };

        // 3. 從 Inspector 上的干擾池 (stage1DistractorPool) 隨機抓取
        var pool = stage1DistractorPool
            .Where(s => !string.IsNullOrWhiteSpace(s) && s.Trim() != correctCategory)
            .Select(s => s.Trim())
            .OrderBy(_ => _rng.Next())
            .ToList();

        foreach (var p in pool)
        {
            if (set.Count >= optionsPerQuestion) break;
            set.Add(p);
        }
        
        // ===== 修正 #2：將 "市集" 加入後備清單，確保萬無一失 =====
        // (如果 Inspector 上的干擾池不夠用，會從這裡補)
        string[] fallback = { "市集", "動物園", "遊樂園", "博物館", "校園", "車站" }; 
        // ===================================================

        foreach (var f in fallback)
        {
            if (set.Count >= optionsPerQuestion) break;
            // HashSet.Add() 會自動處理 "市集" 已經是正解 (correctCategory) 的情況
            set.Add(f); 
        }

        // 5. 轉換為列表並打亂
        var list = set.ToList().OrderBy(_ => _rng.Next()).ToList();
        correctIndex = list.IndexOf(correctCategory);

        // 6. (安全機制) 萬一正解還是不在裡面，強制替換第一項
        if (correctIndex < 0 && list.Count > 0)
        {
            correctIndex = 0;
            list[0] = correctCategory;
        }

        return list.ToArray();
    }

    // ====== Stage 2 Builders ======
    string[] BuildStage2Options(out int correctIndex)
    {
        string correctFloor = NormalizeFloorLabel(_correctEntry.floorLabel);

        var allFloors = new List<string> { "一樓", "二樓", "三樓" };
        if (!allFloors.Contains(correctFloor)) allFloors.Insert(0, correctFloor);

        var set = new HashSet<string> { correctFloor };
        foreach (var f in allFloors)
        {
            if (set.Count >= optionsPerQuestion) break;
            set.Add(f);
        }

        var list = set.ToList().OrderBy(_ => _rng.Next()).ToList();
        correctIndex = list.IndexOf(correctFloor);
        return list.ToArray();
    }

    string NormalizeFloorLabel(string src)
    {
        if (string.IsNullOrEmpty(src)) return "一樓";
        string s = src.Trim().ToUpperInvariant();
        if (s == "F1" || s.Contains("1")) return "一樓";
        if (s == "F2" || s.Contains("2")) return "二樓";
        if (s == "F3" || s.Contains("3")) return "三樓";
        return src; // 已是中文或其它格式
    }

    // ====== Stage 3 Builders（攤位）======
    List<LocationEntry> BuildStage3StallOptions(LocationEntry correct, out int correctIdx)
    {
        var list = new List<LocationEntry>();
        if (db == null || db.entries == null || db.entries.Count == 0)
        {
            correctIdx = -1;
            return list;
        }

        string normFloor = NormalizeFloorLabel(correct.floorLabel);
        IEnumerable<LocationEntry> sameFloor = db.entries.Where(e =>
            e.sceneName == correct.sceneName && NormalizeFloorLabel(e.floorLabel) == normFloor);

        var pool = sameFloor.ToList();
        if (pool.Count < optionsPerQuestion)
        {
            pool = pool.Concat(db.entries.Where(e => e.sceneName == correct.sceneName && !pool.Contains(e))).ToList();
        }
        if (pool.Count < optionsPerQuestion)
        {
            pool = pool.Concat(db.entries.Where(e => !pool.Contains(e))).ToList();
        }

        list.Add(correct);
        foreach (var e in pool.OrderBy(_ => _rng.Next()))
        {
            if (list.Count >= optionsPerQuestion) break;
            if (!list.Contains(e)) list.Add(e);
        }

        list = list.OrderBy(_ => _rng.Next()).ToList();
        correctIdx = list.IndexOf(correct);
        return list;
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

    bool EnsureDbOrWarn()
    {
        if (db != null && db.entries != null && db.entries.Count > 0) return true;
        Debug.LogWarning("[Session] LocationDB 為空，瞬移後不會出題。");
        return false;
    }

    // ===== NavPanel 顯示/隱藏 =====
    void HideNavPanel()
    {
        _navPanelVisibleDesired = false;
        if (!navPanelGroup) return;
        var go = navPanelGroup.gameObject;
        if (go.activeInHierarchy && go.activeSelf) go.SetActive(false);
    }

    void ShowNavPanel()
    {
        _navPanelVisibleDesired = true;
        if (!navPanelGroup) return;
        var go = navPanelGroup.gameObject;
        if (go.activeInHierarchy && !go.activeSelf) go.SetActive(true);
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

        var nav = FindFirstObjectByType<NavPanel>(FindObjectsInactive.Include);
        if (nav)
        {
            navPanelGroup = nav.GetComponent<CanvasGroup>();
            if (!navPanelGroup) navPanelGroup = nav.gameObject.AddComponent<CanvasGroup>();
            return true;
        }

        var go = GameObject.Find("NavPanel");
        if (go)
        {
            navPanelGroup = go.GetComponent<CanvasGroup>() ?? go.AddComponent<CanvasGroup>();
            return true;
        }
        return false;
    }
}