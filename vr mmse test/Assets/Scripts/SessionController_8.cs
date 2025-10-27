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

    // 1) 先精確找
    _correctEntry = db?.FindBySceneAndVP(sceneName, vpName);

    // 2) 模糊找：去掉尾巴的 "_數字"
    if (_correctEntry == null && !string.IsNullOrEmpty(vpName))
    {
        var baseName = System.Text.RegularExpressions.Regex.Replace(vpName, @"_\d+$", "");
        if (!string.Equals(baseName, vpName, StringComparison.Ordinal))
        {
            _correctEntry = db?.FindBySceneAndVP(sceneName, baseName);

            // 再寬鬆一點：以開頭相同比對（避免 DB 用 VP_Fruit 而物件叫 VP_Fruit_8_Extra）
            if (_correctEntry == null && db != null)
            {
                _correctEntry = db.entries.FirstOrDefault(e =>
                    e.sceneName == sceneName &&
                    !string.IsNullOrEmpty(e.viewpointName) &&
                    e.viewpointName.StartsWith(baseName, StringComparison.Ordinal));
            }
        }
    }

    // 3) 仍找不到才回退
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
        // 只在第三階段有效
        if (_stage != Stage.Stall) return;

        // 立刻收起面板，避免殘留與連點
        if (quizPanel) quizPanel.Hide();

        // 安全保護：避免 _currentOptions 或 _correctIndex 越界
        int count = _currentOptions != null ? _currentOptions.Count : 0;
        if (count <= 0)
        {
            Debug.LogWarning("[Session] Stage3: 無可用選項，視為作答錯誤。");
            idx = -1;
        }
        else
        {
            if (idx >= count) idx = count - 1;
            // idx 可為 -1（理論上不會），表示無選擇
        }

        // 取 canonical 的正解鍵
        string sceneName = SceneManager.GetActiveScene().name;
        string correctKey = (_correctIndex >= 0 && _correctIndex < count)
            ? (_currentOptions[_correctIndex]?.viewpointName ?? "")
            : (_correctEntry?.viewpointName ?? "");
        string correctDisplay = (_correctIndex >= 0 && _correctIndex < count)
            ? (!string.IsNullOrEmpty(_currentOptions[_correctIndex]?.displayText)
                ? _currentOptions[_correctIndex].displayText
                : (_currentOptions[_correctIndex]?.stallLabel ?? ""))
            : (_correctEntry?.displayText ?? _correctEntry?.stallLabel ?? "");

        // 使用者選擇
        string selectedKey = (idx >= 0)
            ? (_currentOptions[idx]?.viewpointName ?? "")
            : "";
        string selectedDisplay = (idx >= 0)
            ? (!string.IsNullOrEmpty(_currentOptions[idx]?.displayText)
                ? _currentOptions[idx].displayText
                : (_currentOptions[idx]?.stallLabel ?? ""))
            : "";

        // 以鍵值判定第三階段是否正確（避免參考不一致）
        bool stallOk = (idx >= 0) &&
                    string.Equals(selectedKey, correctKey, StringComparison.Ordinal);

        bool finalOk = (finalCorrectMode == CorrectMode.AllStages)
            ? (_stage1Ok && _stage2Ok && stallOk)
            : stallOk;

        // Console 詳細紀錄
        string floorNorm = NormalizeFloorLabel(_correctEntry?.floorLabel ?? "");
        Debug.Log($"[Session] Stage3 Result | scene={sceneName}, floor={floorNorm}, " +
                $"correct=({correctDisplay})[{correctKey}], chosen=({selectedDisplay})[{selectedKey}], " +
                $"S1={_stage1Ok}, S2={_stage2Ok}, S3={stallOk}, FINAL={finalOk}");

        // 紀錄到 logger（沿用你既有的三階段記錄 API）
        logger?.EndThreeStageQuestion(
            categoryCorrect: stage1CorrectCategory,
            categoryChosen: _pickedCategory,
            categoryIsCorrect: _stage1Ok,

            floorCorrect: floorNorm,
            floorChosen: _pickedFloor,
            floorIsCorrect: _stage2Ok,

            stallChosenKey: selectedKey,
            stallChosenDisplay: selectedDisplay,
            stallIsCorrect: stallOk,

            finalCorrect: finalOk
        );

        // 邏輯收尾
        if (_mover) _mover.allowMove = true;
        if (showNavPanelAfterAnswer) ShowNavPanel();

        _stage = Stage.Done;
        _quizActive = false;

        // 跳下一關（保護：instance 可能為 null）
        if (SceneFlowManager.instance != null)
            SceneFlowManager.instance.LoadNextScene();
    }

    // ====== Stage 1 Builders ======
    // 取代原本的 BuildStage1Options
string[] BuildStage1Options(out int correctIndex)
{
    // A) 正解歸一化（預設「市集」）
    string correctCategory = string.IsNullOrWhiteSpace(stage1CorrectCategory)
        ? "市集"
        : stage1CorrectCategory.Trim();

    // B) 面板實際可顯示的容量（避免超過面板按鈕數，導致正解或「市集」被裁掉）
    int panelCap = Mathf.Max(1, quizPanel ? quizPanel.MaxOptions : optionsPerQuestion);
    int targetCount = Mathf.Min(optionsPerQuestion, panelCap);

    // C) 建立集合（鍵值用 Ordinal），先放正解 & 「市集」
    var set = new HashSet<string>(StringComparer.Ordinal) { correctCategory, "市集" };

    // D) 從干擾池補滿
    var pool = stage1DistractorPool
        .Where(s => !string.IsNullOrWhiteSpace(s))
        .Select(s => s.Trim())
        .Where(s => !set.Contains(s))                    // 不重複正解/市集
        .OrderBy(_ => _rng.Next())
        .ToList();

    foreach (var p in pool)
    {
        if (set.Count >= targetCount) break;
        set.Add(p);
    }

    // E) 後備清單補足
    string[] fallback = { "市集", "動物園", "遊樂園", "博物館", "校園", "海邊", "車站" };
    foreach (var f in fallback)
    {
        if (set.Count >= targetCount) break;
        if (!set.Contains(f)) set.Add(f);
    }

    // F) 轉列表打亂
    var list = set.ToList().OrderBy(_ => _rng.Next()).ToList();

    // G) 最後保障：不論如何，「市集」與正解一定同時存在於可視範圍（targetCount）
    // 若被打亂後跑到 targetCount 之外，就把它換回列表內
    void EnsureInRange(string mustHave)
    {
        int idx = list.IndexOf(mustHave);
        if (idx < 0)
        {
            // 不存在：強插
            if (list.Count < targetCount) list.Add(mustHave);
            else list[0] = mustHave; // 直接頂掉第 0 個干擾項
        }
        else if (idx >= targetCount)
        {
            // 存在但落在可視範圍外：交換到第 0 個
            (list[0], list[idx]) = (list[idx], list[0]);
        }
    }

    EnsureInRange("市集");
    EnsureInRange(correctCategory);

    // H) 修剪至面板容量（不會裁掉「市集」與正解）
    list = list.Take(targetCount).ToList();

    // I) 重新定位正解索引（保證有效）
    correctIndex = list.IndexOf(correctCategory);
    if (correctIndex < 0)
    {
        // 極端情況：把第 0 個換成正解
        if (list.Count == 0) list.Add(correctCategory);
        else list[0] = correctCategory;
        correctIndex = 0;
    }

    // 方便除錯
    Debug.Log($"[Session] Stage1 options = {string.Join(" / ", list)} | correct='{correctCategory}' @ {correctIndex}");
    // === 安全保護：保持 _correctEntry 不受干擾 ===
// 確保在第一階段修改選項時不會重設 _correctEntry
    if (_correctEntry == null && db != null)
    {
        // 若意外被清空，強制重新指派
        var sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        _correctEntry = db.AllByScene(sceneName).FirstOrDefault();
    }
    // =================================================

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
    correctIdx = -1;
    if (db == null || db.entries == null || db.entries.Count == 0 || correct == null)
        return list;

    // 用鍵把 correct「對齊」到 db.entries 的 canonical 參考
    var correctRef = db.entries.FirstOrDefault(e =>
        e.sceneName == correct.sceneName &&
        e.viewpointName == correct.viewpointName) ?? correct;

    // 先取同樓層、同場景
    string normFloor = NormalizeFloorLabel(correctRef.floorLabel);
    var sameFloor = db.entries.Where(e =>
        e.sceneName == correctRef.sceneName &&
        NormalizeFloorLabel(e.floorLabel) == normFloor);

    // 擴充池（同場景 → 全庫），以鍵去重
    var pool = sameFloor.ToList();
    if (pool.Count < optionsPerQuestion)
        pool = pool.Concat(db.entries.Where(e => e.sceneName == correctRef.sceneName &&
                                                 !pool.Any(x => x.sceneName == e.sceneName && x.viewpointName == e.viewpointName))).ToList();
    if (pool.Count < optionsPerQuestion)
        pool = pool.Concat(db.entries.Where(e => !pool.Any(x => x.sceneName == e.sceneName && x.viewpointName == e.viewpointName))).ToList();

    // 先放正解（canonical）
    list.Add(correctRef);

    // 再隨機補齊到目標數量（以鍵去重）
    foreach (var e in pool.OrderBy(_ => _rng.Next()))
    {
        if (list.Count >= optionsPerQuestion) break;
        bool sameKey = list.Any(x => x.sceneName == e.sceneName && x.viewpointName == e.viewpointName);
        if (!sameKey) list.Add(e);
    }

    // 若數量仍不足，就從全庫補（仍以鍵去重）
    if (list.Count < optionsPerQuestion)
    {
        foreach (var e in db.entries.OrderBy(_ => _rng.Next()))
        {
            if (list.Count >= optionsPerQuestion) break;
            bool sameKey = list.Any(x => x.sceneName == e.sceneName && x.viewpointName == e.viewpointName);
            if (!sameKey) list.Add(e);
        }
    }

    // 打亂
    list = list.OrderBy(_ => _rng.Next()).ToList();

    // 用「鍵」求正解索引（不要用參考）
    correctIdx = list.FindIndex(x => x.sceneName == correctRef.sceneName &&
                                     x.viewpointName == correctRef.viewpointName);

    // 萬一找不到（極端），強制把第 0 個換成正解
    if (correctIdx < 0)
    {
        if (list.Count == 0) list.Add(correctRef);
        else list[0] = correctRef;
        correctIdx = 0;
    }

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
