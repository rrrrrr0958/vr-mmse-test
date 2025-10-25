// Assets/Scripts/Game/SessionController.cs
using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using Unity.XR.CoreUtils;
using System;
using System.Collections;

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

    [Tooltip("第一階段類別的干擾選項池（會隨機挑兩個組合）")]
    public string[] stage1DistractorPool = new[] { "市集", "動物園", "遊樂園", "博物館", "校園", "海邊", "車站" };

    [Tooltip("正確判定模式：AllStages=三階段都要對；StallOnly=只看第三階段攤位是否正確")]
    public CorrectMode finalCorrectMode = CorrectMode.AllStages;
    public enum CorrectMode { AllStages, StallOnly }

    [Header("Logging")]
    public RunLogger logger;
    [Tooltip("若場景內找不到 RunLogger，是否自動建立一顆常駐的？")]
    public bool autoCreateLoggerIfMissing = true;

    // ★ Voice（可選）
    [Header("Voice (optional)")]
    public AudioSource voiceSource;      // 建議 2D、非空間化
    public AudioClip voiceStage1;
    public AudioClip voiceStage2;
    public AudioClip voiceStage3;
    public float voicePreDelay = 0.05f;
    public bool waitVoiceBeforeOptions = true;
    
    // ===== Internal =====
    PlayerRigMover _mover;
    System.Random _rng = new System.Random();
    List<LocationEntry> _currentOptions = new(); // 第三階段（攤位）使用
    string[] _currentStringOptions;              // 當前顯示的字串選項
    int _correctIndex = -1;
    bool _quizActive = false;
    bool _boundTargetEvent = false;

    bool _navPanelVisibleDesired = true;

    enum Stage { None, Category, Floor, Stall, Done }
    Stage _stage = Stage.None;

    LocationEntry _correctEntry;  // 當前視角對應正解
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

        // ★ 防止跨場景殘留音效
        if (voiceSource) voiceSource.Stop();

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

        // ★ 進新場景時，確保不會延續播放
        if (voiceSource) voiceSource.Stop();
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

        PlayVoiceThen(voiceStage1, () =>
        {
            quizPanel.Show(stage1Title, _currentStringOptions, OnPick);
        });
    }

    // ===== OnPick：依階段進行 =====
    void OnPick(int idx)
    {
        // ★ 修正 #2：點擊後立刻收起現有選項，避免視覺殘留與重複點擊
        if (quizPanel) quizPanel.Hide();

        switch (_stage)
        {
            case Stage.Category: HandleStage1(idx); break;
            case Stage.Floor:    HandleStage2(idx);  break;
            case Stage.Stall:    HandleStage3(idx);  break;
        }
    }

    // ---------- 外部觸發 (FloorNavNode / StallTrigger) ----------
    public void OnArrivedAtViewpoint(Transform vp) => HandleTeleportedTarget(vp);

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
        _stage1Ok = (idx == _correctIndex);
        _pickedCategory = _currentStringOptions[Mathf.Clamp(idx, 0, _currentStringOptions.Length - 1)];

        // 進 Stage 2
        _stage = Stage.Floor;
        var floorOptions = BuildStage2Options(out _correctIndex);
        _currentStringOptions = floorOptions;

        PlayVoiceThen(voiceStage2, () =>
        {
            quizPanel.Show(stage2Title, _currentStringOptions, OnPick);
        });
    }

    // ---------- Stage 2：樓層 ----------
    void HandleStage2(int idx)
    {
        _stage2Ok = (idx == _correctIndex);
        _pickedFloor = _currentStringOptions[Mathf.Clamp(idx, 0, _currentStringOptions.Length - 1)];
        _currentStringOptions = null;

        // 進 Stage 3（攤位）
        _stage = Stage.Stall;

        _currentOptions = BuildStage3StallOptions(_correctEntry, out _correctIndex);

        // 優先使用 displayText，空才退回 stallLabel
        var labels = _currentOptions.Select(e => {
            string display = (e.displayText ?? "").Trim();
            return !string.IsNullOrEmpty(display) ? display : (e.stallLabel ?? "").Trim();
        }).ToArray();

        PlayVoiceThen(voiceStage3, () =>
        {
            quizPanel.Show(stage3Title, labels, OnPick);
        });
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

        if (_mover) _mover.allowMove = true;
        if (showNavPanelAfterAnswer) ShowNavPanel();

        _stage = Stage.Done;
        _quizActive = false;

        // 結束當題才把面板收起（避免最後一刻被擋）
        if (quizPanel) quizPanel.Hide();

        SceneFlowManager.instance.LoadNextScene();
    }

    // ====== Stage 1 Builders ======
    string[] BuildStage1Options(out int correctIndex)
    {
        // 1) 正解清理
        string correctCategory = string.IsNullOrWhiteSpace(stage1CorrectCategory) ? "市集" : stage1CorrectCategory.Trim();

        // 2) 集合加入正解
        var set = new HashSet<string>(System.StringComparer.Ordinal)
        { 
            correctCategory
        };

        // 3) 從干擾池補滿
        var pool = stage1DistractorPool
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .Where(s => !string.Equals(s, correctCategory, StringComparison.Ordinal))
            .OrderBy(_ => _rng.Next())
            .ToList();

        foreach (var p in pool)
        {
            if (set.Count >= optionsPerQuestion) break;
            set.Add(p);
        }

        // 4) 後備清單，確保數量
        string[] fallback = { "市集", "動物園", "遊樂園", "博物館", "校園", "海邊", "車站" };
        foreach (var f in fallback)
        {
            if (set.Count >= optionsPerQuestion) break;
            if (!set.Contains(f)) set.Add(f);
        }

        // 5) 轉列表打亂
        var list = set.ToList().OrderBy(_ => _rng.Next()).ToList();

        // 6) **強制保障**：若因任何原因缺少正解，插回去
        if (!list.Contains(correctCategory))
        {
            if (list.Count < optionsPerQuestion) list.Add(correctCategory);
            else list[0] = correctCategory; // 直接用第一格換正解
        }

        // 7) 重新找正解索引
        correctIndex = list.IndexOf(correctCategory);
        if (correctIndex < 0)
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

        var set = new HashSet<string>(allFloors);
        // 確保正解在內
        set.Add(correctFloor);

        // 裁成目標數量
        var list = set.ToList().OrderBy(_ => _rng.Next()).Take(optionsPerQuestion).ToList();

        // 強制保障正解在列表中
        if (!list.Contains(correctFloor))
        {
            if (list.Count < optionsPerQuestion) list.Add(correctFloor);
            else list[0] = correctFloor;
        }

        correctIndex = list.IndexOf(correctFloor);
        if (correctIndex < 0) { correctIndex = 0; list[0] = correctFloor; }

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
            pool = pool.Concat(db.entries.Where(e => e.sceneName == correct.sceneName && !pool.Contains(e))).ToList();
        if (pool.Count < optionsPerQuestion)
            pool = pool.Concat(db.entries.Where(e => !pool.Contains(e))).ToList();

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

    // ---------- Voice helpers ----------
    void PlayVoiceThen(AudioClip clip, Action then)
    {
        // ★ 修正 #1 的一部份：若同一個 clip 正在播，視為已播放，避免重複觸發
        if (voiceSource && clip && voiceSource.isPlaying && voiceSource.clip == clip)
        {
            then?.Invoke();
            return;
        }

        if (!waitVoiceBeforeOptions || clip == null || voiceSource == null)
        {
            then?.Invoke();
            return;
        }
        StartCoroutine(CoPlayVoiceThen(clip, then));
    }

    IEnumerator CoPlayVoiceThen(AudioClip clip, Action then)
    {
        if (voicePreDelay > 0f) yield return new WaitForSeconds(voicePreDelay);

        voiceSource.Stop();
        voiceSource.clip = clip;
        voiceSource.Play();

        yield return new WaitWhile(() => voiceSource.isPlaying);

        then?.Invoke();
    }
        bool TryWireNavPanelGroup()
    {
        if (navPanelGroup) return true;

        var nav = FindFirstObjectByType<NavPanel>(FindObjectsInactive.Include);
        if (nav)
        {
            navPanelGroup = nav.GetComponent<CanvasGroup>();
            if (!navPanelGroup)
                navPanelGroup = nav.gameObject.AddComponent<CanvasGroup>();
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
