// Assets/Scripts/LevelIntro/OnGUIIntroDirector.cs
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class OnGUIIntroDirector : MonoBehaviour
{
    public static OnGUIIntroDirector Instance { get; private set; }

    [Header("Definition")]
    public LevelIntroDefinition definition; // 指向你的 LevelIntroDefinition.asset

    [Header("Style")]
    public int  fontSize   = 32;
    public Color textColor = Color.black; // 黑字
    public Font customFont;               // 不指定就用 LegacyRuntime.ttf
    [Range(0.8f, 2.0f)] public float lineHeightScale = 1.2f;

    [Header("Layout")]
    [Range(0f, 0.4f)] public float marginPct = 0.1f; // 左右邊距比例
    public float bottomPaddingPx = 8f;               // 避免下降部被切
    public float backgroundPaddingPx = 16f;          // 文字與白底邊距

    [Header("Background")]
    public bool  showBackground = true;
    [Range(0f,1f)] public float backgroundAlpha = 0.92f; // 白底透明度

    [Header("Audio")]
    public AudioSource audioSource; // 可留空，會自動補一個 2D AudioSource
    [Tooltip("若勾選，會在文字淡入結束後才開始播放語音")]
    public bool playVoiceAfterFadeIn = true;
    [Tooltip("在上面的時機點再額外延遲幾秒才播放（unscaled）")]
    public float voiceDelaySeconds = 0f;

    // 內部狀態
    string _msg = null;
    float  _alpha = 0f;
    float  _fadeIn = 0.3f, _hold = 2.0f, _fadeOut = 0.3f;
    bool   _showing = false;
    GUIStyle _style;

    static Texture2D _whiteTex; // 畫白底用

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (!audioSource)
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 0f; // 2D 播放
        }

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
            StartCoroutine(Show(e));
        }
    }

    IEnumerator Show(LevelIntroDefinition.Entry e)
    {
        // —— 暫停整個遊戲 ——（OnGUI + unscaled 計時不受影響）
        float prevScale = Time.timeScale;
        Time.timeScale = 0f;

        _msg = e.message ?? "";
        _fadeIn  = Mathf.Max(0.01f, e.fadeInSeconds);
        _fadeOut = Mathf.Max(0.01f, e.fadeOutSeconds);
        float total = Mathf.Max(_fadeIn + _fadeOut + 0.1f, e.totalDisplaySeconds);
        _hold = Mathf.Max(0f, total - _fadeIn - _fadeOut);

        // 語音改為延遲播放（平行協程）
        if (e.voiceClip)
            StartCoroutine(PlayVoiceWithDelay(e.voiceClip, (playVoiceAfterFadeIn ? _fadeIn : 0f) + Mathf.Max(0f, voiceDelaySeconds)));

        _alpha = 0f; _showing = true;

        // 淡入（unscaled）
        float t = 0f;
        while (t < _fadeIn)
        {
            t += Time.unscaledDeltaTime;
            _alpha = Mathf.SmoothStep(0f, 1f, t / _fadeIn);
            yield return null;
        }
        _alpha = 1f;

        // 停留（unscaled）
        if (_hold > 0f) yield return new WaitForSecondsRealtime(_hold);

        // 淡出（unscaled）
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

        // —— 恢復遊戲時間 —— 
        Time.timeScale = prevScale;
    }

    IEnumerator PlayVoiceWithDelay(AudioClip clip, float delayUnscaled)
    {
        if (delayUnscaled > 0f)
            yield return new WaitForSecondsRealtime(delayUnscaled);
        audioSource.PlayOneShot(clip);
    }

    void EnsureStyle()
    {
        if (_style != null) return;
        _style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            wordWrap  = true,
            richText  = true,
            font      = customFont ? customFont
                                   : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
        };
        _style.clipping = TextClipping.Overflow; // 避免下緣被裁切
    }

    static void EnsureWhiteTex()
    {
        if (_whiteTex != null) return;
        _whiteTex = new Texture2D(1,1, TextureFormat.RGBA32, false);
        _whiteTex.SetPixel(0,0, Color.white);
        _whiteTex.Apply();
    }

    void OnGUI()
    {
        if (!_showing || string.IsNullOrEmpty(_msg)) return;
        EnsureStyle();
        EnsureWhiteTex();

        // 依螢幕大小調整字級（1080p 為基準）
        float baseSize = Mathf.Min(Screen.width, Screen.height) / 1080f;
        _style.fontSize = Mathf.RoundToInt(fontSize * Mathf.Max(0.75f, baseSize));
        _style.normal.textColor = new Color(textColor.r, textColor.g, textColor.b, _alpha);

        float margin = Screen.width * Mathf.Clamp01(marginPct);
        float boxTop = Screen.height * 0.35f;
        float boxH   = Screen.height * 0.3f + bottomPaddingPx;
        var   rect   = new Rect(margin, boxTop, Screen.width - margin*2f, boxH);

        // 先量測實際文字總高度，取得置中的起始 Y
        float totalH = MeasureMultilineHeight(rect.width, _msg, _style, lineHeightScale);
        float startY = rect.center.y - totalH * 0.5f;

        // 先畫白底（隨 alpha 同步淡入/淡出）
        if (showBackground)
        {
            var bg = new Rect(
                rect.xMin,
                startY - backgroundPaddingPx,
                rect.width,
                totalH + backgroundPaddingPx*2f + bottomPaddingPx
            );

            Color old = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, backgroundAlpha * _alpha);
            GUI.DrawTexture(bg, _whiteTex);
            GUI.color = old;
        }

        // 再畫文字
        DrawMultilineWithSpacing(rect, _msg, _style, lineHeightScale, startY, bottomPaddingPx);
    }

    // 量測多行總高度
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

    // 依起始 Y 逐行繪製，保留底部補償避免截字
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
