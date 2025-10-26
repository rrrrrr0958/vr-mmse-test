using System.Collections;
using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class TutorialVideoController3D_7 : MonoBehaviour
{
    [Header("References")]
    public VideoPlayer videoPlayer;
    public AudioSource audioSource;
    public Renderer screenRenderer;   // VideoSurface 的 MeshRenderer（影片平面）
    public GameObject gameRoot;

    [Header("Video Source")]
    [Tooltip("StreamingAssets 下的檔名，例如 tutorial.mp4")]
    public string streamingVideoFileName = "draw-tutorial.mp4";

    [Header("Flow")]
    public bool useTimeScalePause = true;
    public bool disableGameDuringTutorial = true;
    public bool loadNextSceneInstead = false;
    public string nextSceneName = "SampleScene_7";

    [Header("Quality / UX")]
    public bool autoResizeScreenByAspect = true;
    public float screenBaseHeight = 1.2f;
    public float videoScaleMultiplier = 1.0f; // 額外整體放大倍率
    public KeyCode skipKey = KeyCode.Space;

    // ====== 變暗效果（擇一：Sphere 或 Overlay Quad）======
    [Header("Dim by Sphere (推薦)")]
    public bool useDimSphere = true;
    [Tooltip("圓球 Renderer（放在主相機底下的 Sphere）")]
    public Renderer dimSphereRenderer;
    [Tooltip("圓球跟隨的目標（預設主相機）")]
    public Transform dimSphereFollowTarget;
    [Tooltip("圓球半徑（公尺）。預設 3~5 之間看場景大小調整")]
    public float dimSphereRadius = 4.0f;

    [Header("Dim by Overlay Quad（舊方案，可留空）")]
    public Renderer dimOverlay;  // 若不用圓球，可用平面

    [Header("Dim Intensity / Timing")]
    [Range(0f,1f)] public float initialDimAlpha = 0.8f; // 一開始就暗
    public float dimFadeOutDuration = 0.8f;             // 結束淡出秒數
    public bool enableDim = true;

    // ---- private ----
    private bool _ended = false;
    private float _prevTimeScale = 1f;
    private Material _dimMatInstance;
    private Coroutine _dimRoutine;

    void Awake()
    {
        if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
        if (audioSource  == null) audioSource  = GetComponent<AudioSource>();
        if (videoPlayer == null) Debug.LogError("[TutorialVideo] 找不到 VideoPlayer。");

        // 取得主相機當預設跟隨目標
        if (dimSphereFollowTarget == null && Camera.main != null)
            dimSphereFollowTarget = Camera.main.transform;

        // 準備「變暗材質」實例（Sphere 優先，否則 Overlay）
        var targetRenderer = useDimSphere ? dimSphereRenderer : dimOverlay;
        if (enableDim && targetRenderer != null)
        {
            _dimMatInstance = new Material(targetRenderer.sharedMaterial);
            targetRenderer.material = _dimMatInstance;

            // 強制透明混合 + 僅渲染內側（Front Cull）
            ForceMaterialTransparent(_dimMatInstance, useDimSphere);

            // 一開始就處於暗狀態
            var c = _dimMatInstance.color;
            c.a = Mathf.Clamp01(initialDimAlpha);
            _dimMatInstance.color = c;

            // 若是圓球：跟隨主相機 & 設置半徑
            if (useDimSphere && dimSphereRenderer != null && dimSphereFollowTarget != null)
            {
                var t = dimSphereRenderer.transform;
                t.SetParent(dimSphereFollowTarget, worldPositionStays: false);
                t.localPosition = Vector3.zero;
                t.localRotation = Quaternion.identity;

                // Unity Sphere 原始直徑 ≈ 1，所以 scale = 半徑 * 2
                float d = Mathf.Max(0.01f, dimSphereRadius * 2f);
                t.localScale = new Vector3(d, d, d);
            }
        }
    }

    void Start()
    {
        // 暫停策略
        if (useTimeScalePause)
        {
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }
        else if (disableGameDuringTutorial && gameRoot != null)
        {
            gameRoot.SetActive(false);
        }

        // VideoPlayer 設定
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted += OnPrepared;
            videoPlayer.loopPointReached += OnVideoEnd;

            if (audioSource != null)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                videoPlayer.EnableAudioTrack(0, true);
                videoPlayer.SetTargetAudioSource(0, audioSource);
                audioSource.spatialBlend = 0f;
            }
            else
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            }

            var url = "file://" + System.IO.Path.Combine(Application.streamingAssetsPath, streamingVideoFileName);
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url    = url;
            videoPlayer.Prepare();
        }
    }

    void Update()
    {
        if (!_ended && Input.GetKeyDown(skipKey))
            SkipTutorial();
    }

    private void OnPrepared(VideoPlayer vp)
    {
        // 自動依比例設定影片平面大小（再乘 videoScaleMultiplier）
        if (autoResizeScreenByAspect && vp.texture != null && screenRenderer != null)
        {
            float w = vp.texture.width, h = vp.texture.height;
            if (h > 0f)
            {
                float aspect = w / h; // 寬/高
                float targetHeight = screenBaseHeight * Mathf.Max(0.01f, videoScaleMultiplier);
                float targetWidth  = targetHeight * aspect;
                screenRenderer.transform.localScale = new Vector3(targetWidth, targetHeight, 1f);
            }
        }

        vp.Play();
        if (audioSource != null) audioSource.Play();
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        if (_ended) return;
        _ended = true;

        // 結束 → 淡出黑幕
        if (enableDim && _dimMatInstance != null)
            StartDimFade(0f, dimFadeOutDuration);

        // 可選：關掉影片螢幕
        if (screenRenderer != null)
            screenRenderer.gameObject.SetActive(false);

        // 收尾：切場或恢復遊戲
        if (loadNextSceneInstead && !string.IsNullOrEmpty(nextSceneName))
        {
            if (useTimeScalePause) Time.timeScale = _prevTimeScale;
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        if (useTimeScalePause) Time.timeScale = _prevTimeScale;
        else if (disableGameDuringTutorial && gameRoot != null) gameRoot.SetActive(true);
    }

    public void SkipTutorial()
    {
        if (_ended) return;
        if (videoPlayer != null && videoPlayer.isPlaying) videoPlayer.Stop();
        if (audioSource  != null && audioSource.isPlaying) audioSource.Stop();
        OnVideoEnd(videoPlayer);
    }

    void OnDestroy()
    {
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted -= OnPrepared;
            videoPlayer.loopPointReached -= OnVideoEnd;
        }
        if (useTimeScalePause && !_ended) Time.timeScale = _prevTimeScale;
    }

    // ===== helpers =====
    private void ForceMaterialTransparent(Material m, bool cullFrontForSphere)
    {
        if (m == null) return;

        // URP 常見屬性
        if (m.HasProperty("_Surface")) m.SetFloat("_Surface", 1f); // Transparent
        if (m.HasProperty("_Blend"))   m.SetFloat("_Blend",   0f); // Alpha
        if (m.HasProperty("_ZWrite"))  m.SetFloat("_ZWrite",  0f);
        if (m.HasProperty("_Cull"))
            m.SetFloat("_Cull", cullFrontForSphere ? 1f : 2f); // 1=Front, 2=Back
        m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
        m.renderQueue = 3000; // Transparent

        // 內建管線/通用
        m.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        m.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        m.SetInt("_ZWrite", 0);
        m.EnableKeyword("_ALPHABLEND_ON");
    }

    private void StartDimFade(float toAlpha, float duration)
    {
        if (_dimRoutine != null) StopCoroutine(_dimRoutine);
        _dimRoutine = StartCoroutine(FadeOverlay(toAlpha, duration));
    }

    private IEnumerator FadeOverlay(float targetAlpha, float duration)
    {
        if (_dimMatInstance == null) yield break;

        Color c = _dimMatInstance.color;
        float startAlpha = c.a;
        float t = 0f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            c.a = Mathf.Lerp(startAlpha, targetAlpha, t / duration);
            _dimMatInstance.color = c;
            yield return null;
        }
        c.a = targetAlpha;
        _dimMatInstance.color = c;
    }
}
