using UnityEngine;
using UnityEngine.Video;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class TutorialVideoController3D_7 : MonoBehaviour
{
    [Header("References")]
    public VideoPlayer videoPlayer;        // 指到本物件上的 VideoPlayer（可留空自動抓）
    public AudioSource audioSource;        // 指到本物件上的 AudioSource（可留空自動抓）
    public Renderer screenRenderer;        // 指到顯示面的 MeshRenderer（Quad/Plane）
    public GameObject gameRoot;            // (選填) 影片期間要關閉的遊戲根物件

    [Header("Video Source")]
    [Tooltip("StreamingAssets 下的檔名，例如 tutorial.mp4")]
    public string streamingVideoFileName = "tutorial.mp4";

    [Header("Flow")]
    [Tooltip("勾選：影片期間以 Time.timeScale=0 暫停遊戲（物件仍可見）。\n取消：使用關閉 gameRoot 的做法。")]
    public bool useTimeScalePause = true;

    [Tooltip("若不使用 timeScale 暫停時，是否在影片期間關閉 gameRoot")]
    public bool disableGameDuringTutorial = true;

    [Tooltip("影片播完後是否切換到下一個場景")]
    public bool loadNextSceneInstead = false;
    public string nextSceneName = "SampleScene_7";

    [Header("Quality / UX")]
    public bool autoResizeScreenByAspect = true;
    public float screenBaseHeight = 1.2f;
    public KeyCode skipKey = KeyCode.Space;   // 桌機測試快速跳過

    private bool _ended = false;
    private float _prevTimeScale = 1f;

    void Awake()
    {
        if (videoPlayer == null) videoPlayer = GetComponent<VideoPlayer>();
        if (audioSource  == null) audioSource  = GetComponent<AudioSource>();
        if (videoPlayer == null)
        {
            Debug.LogError("[TutorialVideo] 找不到 VideoPlayer，請加到同一物件上。");
        }
        if (audioSource == null)
        {
            Debug.LogWarning("[TutorialVideo] 找不到 AudioSource，將自動以無聲播放。");
        }
    }

    void Start()
    {
        // ---- 遊戲啟動前的暫停策略 ----
        if (useTimeScalePause)
        {
            _prevTimeScale = Time.timeScale;
            Time.timeScale = 0f; // 全域暫停（Animator/粒子若需動，請改用 Unscaled Time）
        }
        else
        {
            if (disableGameDuringTutorial && gameRoot != null)
                gameRoot.SetActive(false);
        }

        // ---- VideoPlayer 設定與事件 ----
        if (videoPlayer != null)
        {
            videoPlayer.prepareCompleted += OnPrepared;
            videoPlayer.loopPointReached += OnVideoEnd;

            // 音訊輸出
            if (audioSource != null)
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                videoPlayer.EnableAudioTrack(0, true);
                videoPlayer.SetTargetAudioSource(0, audioSource);
                audioSource.spatialBlend = 0f; // 2D 聲音，不受距離影響
            }
            else
            {
                videoPlayer.audioOutputMode = VideoAudioOutputMode.None;
            }

            // 指定 URL（StreamingAssets）
            var url = System.IO.Path.Combine(Application.streamingAssetsPath, streamingVideoFileName);
            videoPlayer.source = VideoSource.Url;
            videoPlayer.url    = url;

            // 建議：Inspector 內取消 Play On Awake、勾 Wait For First Frame
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
        // 依影片比例自動調整螢幕尺寸
        if (autoResizeScreenByAspect && vp.texture != null && screenRenderer != null)
        {
            float w = vp.texture.width, h = vp.texture.height;
            if (h > 0f)
            {
                float aspect = w / h; // 寬/高
                float targetHeight = screenBaseHeight;
                float targetWidth  = targetHeight * aspect;
                screenRenderer.transform.localScale = new Vector3(targetWidth, targetHeight, 1f);
            }
        }

        // 開始播放
        vp.Play();
        if (audioSource != null) audioSource.Play();
    }

    private void OnVideoEnd(VideoPlayer vp)
    {
        if (_ended) return;
        _ended = true;

        // 隱藏影片畫面（可依需要改成淡出）
        if (screenRenderer != null)
            screenRenderer.gameObject.SetActive(false);

        // 邏輯收尾：恢復遊戲或切場
        if (loadNextSceneInstead && !string.IsNullOrEmpty(nextSceneName))
        {
            // 先確保 timeScale 恢復
            if (useTimeScalePause) Time.timeScale = _prevTimeScale;
            SceneManager.LoadScene(nextSceneName);
            return;
        }

        // 恢復/開啟遊戲
        if (useTimeScalePause)
        {
            Time.timeScale = _prevTimeScale;  // 恢復先前的 timeScale
        }
        else
        {
            if (disableGameDuringTutorial && gameRoot != null)
                gameRoot.SetActive(true);
        }
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

        // 若物件被銷毀前尚未結束，確保 timeScale 被恢復
        if (useTimeScalePause && !_ended)
        {
            Time.timeScale = _prevTimeScale;
        }
    }
}
