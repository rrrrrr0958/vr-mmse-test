using UnityEngine;
using UnityEngine.SceneManagement;
using System.Collections;

/// <summary>
/// 安全場景切換器：單一入口、可選黑幕淡入淡出、防止重複觸發。
/// 注意：出題/鎖移動等流程仍由 SessionController 的 sceneLoaded 勾子負責。
/// </summary>
[DisallowMultipleComponent]
public class SceneLoader : MonoBehaviour
{
    static SceneLoader _instance;
    public static SceneLoader Instance {
        get {
            if (_instance == null) {
                var go = new GameObject("[SceneLoader]");
                _instance = go.AddComponent<SceneLoader>();
                DontDestroyOnLoad(go);
            }
            return _instance;
        }
    }

    [Header("Options")]
    [Tooltip("切場景時是否嘗試做黑幕淡入淡出。需場景內有名為 'FadeOverlay' 的 CanvasGroup。")]
    public bool useFade = true;
    [Tooltip("黑幕淡入淡出時間（秒）")]
    public float fadeDuration = 0.25f;

    bool _isLoading;
    bool _pendingFadeIn;

    void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        if (_instance == this) _instance = null;
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        // 新場景載入後，如果開了「用黑幕淡入淡出」，在新場景找 Overlay 淡回來
        if (_pendingFadeIn && useFade)
        {
            var overlay = FindFadeOverlay();
            if (overlay) StartCoroutine(FadeTo(overlay, 0f, fadeDuration));
        }
        _pendingFadeIn = false;
        _isLoading = false;
    }

    // ========== 對外 API（推薦用這個） ==========
    public static void Load(string sceneName, bool withFade = true)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;
        Instance.LoadInternal(sceneName, withFade);
    }

    /// <summary>給 Button 直接綁的方法（使用序列化選項）。</summary>
    public void LoadScene(string sceneName)
    {
        if (string.IsNullOrWhiteSpace(sceneName)) return;
        Load(sceneName, useFade);
    }

    // ========== 內部實作 ==========
    void LoadInternal(string sceneName, bool withFade)
    {
        if (_isLoading) return;              // 防止短時間重複觸發
        _isLoading = true;

        if (!withFade)
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            return;
        }

        StartCoroutine(LoadWithFadeRoutine(sceneName));
    }

    IEnumerator LoadWithFadeRoutine(string sceneName)
    {
        var overlay = FindFadeOverlay();
        if (overlay)
        {
            yield return FadeTo(overlay, 1f, fadeDuration); // 淡出
            _pendingFadeIn = true;                          // 讓新場景淡回來
        }
        else
        {
            // 找不到黑幕就直接切
            _pendingFadeIn = false;
        }

        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    CanvasGroup FindFadeOverlay()
    {
        // 專案慣例：場景中放一個名為 "FadeOverlay" 的全螢幕 Image + CanvasGroup
        var go = GameObject.Find("FadeOverlay");
        if (!go) return null;
        return go.GetComponent<CanvasGroup>();
    }

    IEnumerator FadeTo(CanvasGroup cg, float target, float dur)
    {
        if (!cg) yield break;
        float start = cg.alpha;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            cg.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(t / dur));
            yield return null;
        }
        cg.alpha = target;
    }
}
