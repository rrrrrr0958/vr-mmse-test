// Assets/Scripts/LevelIntro/OnGUIIntroDirector.cs
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Events;
using UnityEngine.Playables;
using UnityEngine.AI;

public class OnGUIIntroDirector : MonoBehaviour
{
    public static OnGUIIntroDirector Instance { get; private set; }
    public static bool IsIntroActive { get; private set; } = false;

    [Header("Definition")]
    public LevelIntroDefinition definition; // 你的 ScriptableObject
    [Tooltip("若 LevelIntroDefinition.Entry 沒有提供 targetSceneName，可在這裡強制指定要載入的場景（可留空）。")]
    public string forceTargetSceneName = "";

    [Header("Style")]
    public int fontSize = 32;
    public Color textColor = Color.black;
    public Font customFont;
    [Range(0.8f, 2.0f)] public float lineHeightScale = 1.2f;

    [Header("Layout")]
    [Range(0f, 0.4f)] public float marginPct = 0.1f;
    public float bottomPaddingPx = 8f;
    public float backgroundPaddingPx = 16f;

    [Header("Background")]
    public bool showBackground = true;
    [Range(0f, 1f)] public float backgroundAlpha = 0.92f;
    public Color backgroundColor = Color.white;

    [Header("Audio")]
    public AudioSource audioSource;
    public bool playVoiceAfterFadeIn = true;
    public float voiceDelaySeconds = 0f;

    [Header("Events (Optional)")]
    public UnityEvent OnIntroStart;
    public UnityEvent OnIntroEnd;

    // 內部 UI 狀態
    string _msg = null;
    float _alpha = 0f;
    float _fadeIn = 0.3f, _hold = 2.0f, _fadeOut = 0.3f;
    bool _showing = false;
    GUIStyle _style;
    static Texture2D _whiteTex;

    // Loading 畫面
    bool _showLoading = false;
    float _loadingProgress01 = 0f;
    string _loadingHint = "Loading...";
    AsyncOperation _pendingOp;

    // 備援：凍結現場景（只有當沒指定目標場景時才用）
    float _prevTimeScale = 1f;

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.spatialBlend = 0f;
        audioSource.ignoreListenerPause = true;

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (Instance == this) SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (!definition) return;
        if (definition.TryGet(scene.name, out var e))
        {
            // 讀取 Entry 的 targetSceneName（可選），需要在你的 LevelIntroDefinition.Entry 新增 public string targetSceneName;
            string targetScene = SafeTargetSceneFromEntry(e);
            if (!string.IsNullOrEmpty(forceTargetSceneName)) targetScene = forceTargetSceneName;

            if (!string.IsNullOrEmpty(targetScene) && targetScene != scene.name)
            {
                // —— 推薦的「加載模式」：Intro 在這個 Loader 場景播，實際關卡等結束才啟用 ——
                StartCoroutine(LoadThenIntroThenActivate(e, targetScene, scene));
            }
            else
            {
                // —— 相容模式（無目標場景）：只在現場景上顯示 intro，並暫停 timeScale —— 
                StartCoroutine(ShowOnCurrentScene(e));
            }
        }
    }

    string SafeTargetSceneFromEntry(LevelIntroDefinition.Entry e)
    {
        // 若你已在 Entry 加了 public string targetSceneName，就回傳它；否則回傳空字串。
        // 為了不讓你改 ScriptableObject 也能用，可以暫時維持空字串，由 forceTargetSceneName 來指定。
        try
        {
            var f = typeof(LevelIntroDefinition.Entry).GetField("targetSceneName");
            if (f != null && f.FieldType == typeof(string))
            {
                return (string)f.GetValue(e);
            }
        }
        catch { }
        return "";
    }

    // ===================== 加載模式（建議） =====================

    IEnumerator LoadThenIntroThenActivate(LevelIntroDefinition.Entry e, string targetSceneName, Scene loaderScene)
    {
        // 啟動 loading 覆蓋
        _showLoading = true;
        _loadingProgress01 = 0f;
        _loadingHint = "Loading...";

        // 先開啟 Intro 狀態，但此時不凍結任何東西，因為真正的關卡還沒啟用
        OnIntroStart?.Invoke();
        IsIntroActive = true;

        // 準備 Intro 文案與時間（用 unscaled）
        PrepareIntroTimings(e);

        // 開始非同步載入，Additive + 不允許啟用
        _pendingOp = SceneManager.LoadSceneAsync(targetSceneName, LoadSceneMode.Additive);
        if (_pendingOp == null)
        {
            Debug.LogError($"[OnGUIIntroDirector] LoadSceneAsync 失敗：{targetSceneName}");
            yield break;
        }
        _pendingOp.allowSceneActivation = false;

        // 播語音（可選）
        if (e.voiceClip)
            StartCoroutine(PlayVoiceWithDelay(e.voiceClip,
                (playVoiceAfterFadeIn ? _fadeIn : 0f) + Mathf.Max(0f, voiceDelaySeconds)));

        // 進入 Intro：淡入 → 停留，同步更新 loading 進度（最多到 0.9）
        _alpha = 0f; _showing = true;
        float t = 0f;

        // 淡入
        while (t < _fadeIn)
        {
            UpdateLoadingProgress();
            t += Time.unscaledDeltaTime;
            _alpha = Mathf.SmoothStep(0f, 1f, t / _fadeIn);
            yield return null;
        }
        _alpha = 1f;

        // 停留
        if (_hold > 0f)
        {
            float holdT = 0f;
            while (holdT < _hold)
            {
                UpdateLoadingProgress();
                holdT += Time.unscaledDeltaTime;
                yield return null;
            }
        }

        // 淡出（此時還不啟用關卡）
        t = 0f;
        while (t < _fadeOut)
        {
            UpdateLoadingProgress();
            t += Time.unscaledDeltaTime;
            _alpha = Mathf.SmoothStep(1f, 0f, t / _fadeOut);
            yield return null;
        }

        // Intro 覆蓋結束
        _alpha = 0f;
        _showing = false;
        _msg = null;

        // 等待載入至少到 0.9（Unity 規格：allowSceneActivation=false 時卡在 0.9）
        while (_pendingOp.progress < 0.9f)
        {
            UpdateLoadingProgress();
            yield return null;
        }

        // Intro 已播完 → 允許啟用關卡
        _loadingHint = "Activating...";
        _pendingOp.allowSceneActivation = true;

        // 等待啟用完成
        while (!_pendingOp.isDone) { yield return null; }

        // 設為主動場景
        var loadedScene = SceneManager.GetSceneByName(targetSceneName);
        if (loadedScene.IsValid())
        {
            SceneManager.SetActiveScene(loadedScene);
        }

        // 卸載 Loader 場景（保留本 Director，因為有 DontDestroyOnLoad）
        if (loaderScene.IsValid())
        {
            SceneManager.UnloadSceneAsync(loaderScene);
        }

        // 關閉 loading 覆蓋
        _showLoading = false;
        _pendingOp = null;

        // 結束事件
        IsIntroActive = false;
        OnIntroEnd?.Invoke();
    }

    void UpdateLoadingProgress()
    {
        if (_pendingOp == null) return;
        // progress 在 allowSceneActivation=false 時最大到 0.9
        float p = Mathf.Clamp01(_pendingOp.progress / 0.9f);
        _loadingProgress01 = p;
        _loadingHint = $"Loading {Mathf.RoundToInt(p * 100f)}%";
    }

    void PrepareIntroTimings(LevelIntroDefinition.Entry e)
    {
        _msg = e.message ?? "";
        _fadeIn  = Mathf.Max(0.01f, e.fadeInSeconds);
        _fadeOut = Mathf.Max(0.01f, e.fadeOutSeconds);
        float total = Mathf.Max(_fadeIn + _fadeOut + 0.1f, e.totalDisplaySeconds);
        _hold = Mathf.Max(0f, total - _fadeIn - _fadeOut);
    }

    // ===================== 相容模式（無目標場景） =====================

    IEnumerator ShowOnCurrentScene(LevelIntroDefinition.Entry e)
    {
        // 備援：仍然凍結 timeScale（雖然你說不要動其他邏輯，但這是 fallback）
        _prevTimeScale = Time.timeScale;
        Time.timeScale = 0f;

        OnIntroStart?.Invoke();
        IsIntroActive = true;

        PrepareIntroTimings(e);

        if (e.voiceClip)
            StartCoroutine(PlayVoiceWithDelay(e.voiceClip,
                (playVoiceAfterFadeIn ? _fadeIn : 0f) + Mathf.Max(0f, voiceDelaySeconds)));

        _alpha = 0f; _showing = true;

        float t = 0f;
        while (t < _fadeIn)
        {
            t += Time.unscaledDeltaTime;
            _alpha = Mathf.SmoothStep(0f, 1f, t / _fadeIn);
            yield return null;
        }
        _alpha = 1f;

        if (_hold > 0f) yield return new WaitForSecondsRealtime(_hold);

        t = 0f;
        while (t < _fadeOut)
        {
            t += Time.unscaledDeltaTime;
            _alpha = Mathf.SmoothStep(1f, 0f, t / _fadeOut);
            yield return null;
        }

        _alpha = 0f;
        _showing = false;
        _msg = null;

        // 恢復時間
        Time.timeScale = _prevTimeScale;

        IsIntroActive = false;
        OnIntroEnd?.Invoke();
    }

    IEnumerator PlayVoiceWithDelay(AudioClip clip, float delayUnscaled)
    {
        if (delayUnscaled > 0f)
            yield return new WaitForSecondsRealtime(delayUnscaled);
        audioSource.PlayOneShot(clip);
    }

    // ===================== OnGUI：Intro + Loading 覆蓋 =====================

    void EnsureStyle()
    {
        if (_style != null) return;
        _style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap = true,
            richText = true,
            font = customFont ? customFont
                              : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
        };
        _style.clipping = TextClipping.Overflow;
    }

    static void EnsureWhiteTex()
    {
        if (_whiteTex != null) return;
        _whiteTex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _whiteTex.SetPixel(0, 0, Color.white);
        _whiteTex.Apply();
    }

    void OnGUI()
    {
        EnsureStyle();
        EnsureWhiteTex();

        // Loading 覆蓋（在最底層鋪一層半透明幕 + 進度條/旋轉）
        if (_showLoading)
            DrawLoadingOverlay();

        // Intro 文字
        if (_showing && !string.IsNullOrEmpty(_msg))
            DrawIntroText();
    }

    void DrawIntroText()
    {
        float baseSize = Mathf.Min(Screen.width, Screen.height) / 1080f;
        _style.fontSize = Mathf.RoundToInt(fontSize * Mathf.Max(0.75f, baseSize));
        _style.normal.textColor = new Color(textColor.r, textColor.g, textColor.b, _alpha);

        float margin = Screen.width * Mathf.Clamp01(marginPct);
        float boxTop = Screen.height * 0.35f;
        float boxH = Screen.height * 0.3f + bottomPaddingPx;
        var rect = new Rect(margin, boxTop, Screen.width - margin * 2f, boxH);

        float totalH = MeasureMultilineHeight(rect.width, _msg, _style, lineHeightScale);
        float startY = rect.center.y - totalH * 0.5f;

        if (showBackground)
        {
            var bg = new Rect(
                rect.xMin,
                startY - backgroundPaddingPx,
                rect.width,
                totalH + backgroundPaddingPx * 2f + bottomPaddingPx
            );

            Color old = GUI.color;
            GUI.color = new Color(backgroundColor.r, backgroundColor.g, backgroundColor.b, backgroundAlpha * _alpha);
            GUI.DrawTexture(bg, _whiteTex);
            GUI.color = old;
        }

        DrawMultilineWithSpacing(rect, _msg, _style, lineHeightScale, startY, bottomPaddingPx);
    }

    void DrawLoadingOverlay()
    {
        // 半透明遮罩
        var full = new Rect(0, 0, Screen.width, Screen.height);
        Color old = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(full, _whiteTex);
        GUI.color = old;

        // 簡單進度條
        float w = Mathf.Min(Screen.width * 0.6f, 600f);
        float h = 18f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height * 0.75f;

        // 背板
        GUI.color = new Color(1f, 1f, 1f, 0.85f);
        GUI.DrawTexture(new Rect(x, y, w, h), _whiteTex);

        // 進度
        GUI.color = new Color(0.2f, 0.6f, 1f, 0.95f);
        GUI.DrawTexture(new Rect(x, y, w * Mathf.Clamp01(_loadingProgress01), h), _whiteTex);

        // 文字提示
        var hintStyle = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18, normal = { textColor = Color.white } };
        GUI.Label(new Rect(0, y - 28f, Screen.width, 24f), _loadingHint, hintStyle);

        // 旋轉小圓弧（簡單動畫，不依賴 timeScale，用 unscaled）
        float r = 20f;
        float cx = Screen.width * 0.5f;
        float cy = y - 60f;
        DrawSpinner(cx, cy, r);
    }

    void DrawSpinner(float cx, float cy, float r)
    {
        // 用 GUI.DrawTexture 畫一個簡單的圓環段替代（為了簡潔，這裡用旋轉方塊感）
        float size = r * 1.8f;
        var rect = new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size);

        // 簡單的旋轉矩形（依 unscaled time）
        float angle = (Time.unscaledTime * 180f) % 360f;
        Matrix4x4 prev = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, new Vector2(cx, cy));
        GUI.color = new Color(1f, 1f, 1f, 0.95f);
        GUI.DrawTexture(rect, _whiteTex);
        GUI.matrix = prev;

        // 蓋一層中心遮罩做成圓環感
        GUI.color = new Color(0f, 0f, 0f, 0.35f);
        GUI.DrawTexture(new Rect(cx - r, cy - r, r * 2f, r * 2f), _whiteTex);
        GUI.color = Color.white;
    }

    static float MeasureMultilineHeight(float width, string text, GUIStyle style, float lineScale)
    {
        string[] lines = text.Split('\n');
        float total = 0f;
        for (int i = 0; i < lines.Length; i++)
        {
            var gc = new GUIContent(lines[i]);
            float h = Mathf.Ceil(style.CalcHeight(gc, width));
            if (h <= 0f) h = style.fontSize * 1.1f;
            total += h * lineScale;
        }
        return total;
    }

    static void DrawMultilineWithSpacing(Rect area, string text, GUIStyle style, float lineScale, float startY, float bottomPad)
    {
        string[] lines = text.Split('\n');
        float y = startY;

        for (int i = 0; i < lines.Length; i++)
        {
            var gc = new GUIContent(lines[i]);
            float h = Mathf.Ceil(style.CalcHeight(gc, area.width));
            if (h <= 0f) h = style.fontSize * 1.1f;

            var lr = new Rect(area.xMin, Mathf.Round(y), area.width, h + bottomPad);
            GUI.Label(lr, lines[i], style);
            y += h * lineScale;
        }
    }
}
