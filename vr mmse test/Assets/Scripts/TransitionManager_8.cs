using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// TransitionManager
/// - 場景切換：黑幕淡出→(停留)→淡入
/// - 同場景：平滑旋轉/位移 + 頭綁遮罩（降低暈眩）
/// 放在任一場景一次即可（DontDestroyOnLoad）。可手動指定 xrOrigin；未指定時會自動尋找。
/// </summary>
public class TransitionManager : MonoBehaviour
{
    public static TransitionManager I { get; private set; }

    [Header("XR Rig / Camera Root")]
    [Tooltip("通常填 XR Origin (XR Rig)。若留空會自動尋找常見名稱，或使用主攝影機的父物件。")]
    public Transform xrOrigin;

    [Header("Comfort Speeds")]
    [Tooltip("水平旋轉角速度（度/秒）。建議 80~120。")]
    public float yawDegPerSec = 80f;

    [Tooltip("位移速度（公尺/秒）。建議 1.2~2.0。")]
    public float moveMetersPerSec = 1.2f;

    [Header("Vignette / Ease")]
    [Tooltip("旋轉/位移期間的遮罩強度（0~1）。0.35~0.55 之間較舒適。")]
    [Range(0f, 1f)] public float vignetteDuringRotate = 0.45f;

    [Tooltip("旋轉/位移前後的微停（秒），避免節奏太急。")]
    public float prePostPause = 0.05f;

    [Tooltip("插值曲線")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // ===== internal: overlay black canvas =====
    Canvas _canvas;
    CanvasGroup _group;
    Image _black;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        EnsureCanvas();
        EnsureXrOrigin();
        SceneManager.sceneLoaded += (_, __) => EnsureXrOrigin(); // 切場景後再確認一次
    }

    void OnDestroy()
    {
        if (I == this) I = null;
        SceneManager.sceneLoaded -= (_, __) => EnsureXrOrigin();
    }

    void EnsureCanvas()
    {
        if (_canvas) return;

        var go = new GameObject("[TransitionCanvas]");
        DontDestroyOnLoad(go);

        _canvas = go.AddComponent<Canvas>();
        _group  = go.AddComponent<CanvasGroup>();
        _group.alpha = 0f;
        _group.blocksRaycasts = true;
        _group.interactable = false;

        // === 關鍵：在 XR 上改用 Screen Space - Camera，綁到 HMD 相機 ===
        var cam = Camera.main; // XR 裝置時應為 CenterEye/MainCamera
        if (cam != null)
        {
            _canvas.renderMode = RenderMode.ScreenSpaceCamera;
            _canvas.worldCamera = cam;
            _canvas.planeDistance = 0.5f;     // 在相機前
            _canvas.sortingOrder = short.MaxValue;
        }
        else
        {
            // 萬一還沒抓到相機，暫時用 Overlay；sceneLoaded 後會再重綁
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = short.MaxValue;
        }

        var imgGO = new GameObject("Black");
        imgGO.transform.SetParent(go.transform, false);
        _black = imgGO.AddComponent<UnityEngine.UI.Image>();
        _black.color = Color.black;

        var rt = _black.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // 切場景後再嘗試一次把 Canvas 綁到 XR 相機（避免啟動當下還沒有 MainCamera）
        SceneManager.sceneLoaded += (_, __) =>
        {
            var cam2 = Camera.main;
            if (cam2 != null)
            {
                _canvas.renderMode = RenderMode.ScreenSpaceCamera;
                _canvas.worldCamera = cam2;
                _canvas.planeDistance = 0.5f;
            }
        };
    }

    void EnsureXrOrigin()
    {
        if (xrOrigin) return;

        // 先找 XR Rig
        var rigByName = GameObject.Find("XR Origin (XR Rig)") ??
                        GameObject.Find("XR Origin") ??
                        GameObject.Find("XRRig");
        if (rigByName) { xrOrigin = rigByName.transform; return; }

        // 用主攝影機推斷 XR 根（有父物件則用父物件，否則就用相機本身）
        var cam = Camera.main;
        if (cam) xrOrigin = cam.transform.parent ? cam.transform.parent : cam.transform;
    }

    // ===========================================================
    // 公開 API
    // ===========================================================

    /// <summary>
    /// 上下樓：黑幕淡出→可選全黑停留→載入→淡入
    /// </summary>
    public async Task FadeSceneLoad(string sceneName, float fadeOut = 0.8f, float holdBlack = 0.2f, float fadeIn = 0.8f)
    {
        await FadeTo(1f, Mathf.Max(0f, fadeOut));
        if (holdBlack > 0f) await Task.Delay(Mathf.RoundToInt(holdBlack * 1000));

        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (!op.isDone) await Task.Yield();
        await Task.Yield(); // 讓新場景跑完一幀

        await FadeTo(0f, Mathf.Max(0f, fadeIn));
    }

    /// <summary>
    /// 左/右/直走：依角速度/位移速度決定時長，期間加遮罩；只調整水平面朝向。
    /// minDuration 為最短時長（秒），避免太快。
    /// </summary>
    public async Task RotateMoveTo(Transform targetViewpoint, float minDuration = 0.9f, bool move = true)
    {
        if (!xrOrigin || !targetViewpoint) return;

        // 水平朝向
        Vector3 curFwd = xrOrigin.forward; curFwd.y = 0f; curFwd.Normalize();
        Vector3 tgtFwd = targetViewpoint.forward; tgtFwd.y = 0f;
        if (tgtFwd.sqrMagnitude < 1e-6f) tgtFwd = curFwd; else tgtFwd.Normalize();
        float yawAngle = Vector3.Angle(curFwd, tgtFwd); // 度

        // 距離
        Vector3 startPos = xrOrigin.position;
        Vector3 endPos   = move ? targetViewpoint.position : startPos;
        float distance   = move ? Vector3.Distance(startPos, endPos) : 0f;

        // 計算時長（取旋轉/位移較大者，再不小於最短）
        float tYaw  = yawDegPerSec     > 0f ? yawAngle  / yawDegPerSec      : 0f;
        float tMove = moveMetersPerSec > 0f ? distance  / moveMetersPerSec  : 0f;
        float duration = Mathf.Max(minDuration, tYaw, tMove);
        duration = Mathf.Clamp(duration, minDuration, 2.0f); // 上限避免太久

        // 遮罩進入
        float baseAlpha = _group.alpha;
        await FadeTo(Mathf.Max(baseAlpha, vignetteDuringRotate), 0.12f);
        if (prePostPause > 0f) await Task.Delay(Mathf.RoundToInt(prePostPause * 1000));

        // 內插
        Quaternion startRot = xrOrigin.rotation;
        Quaternion endRot   = Quaternion.LookRotation(tgtFwd, Vector3.up);
        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duration);
            float k = ease.Evaluate(Mathf.Clamp01(t));
            xrOrigin.rotation = Quaternion.Slerp(startRot, endRot, k);
            if (move) xrOrigin.position = Vector3.Lerp(startPos, endPos, k);
            await Task.Yield();
        }

        // 收尾
        if (prePostPause > 0f) await Task.Delay(Mathf.RoundToInt(prePostPause * 1000));
        await FadeTo(baseAlpha, 0.15f);
    }

    // ===========================================================
    // 私用：黑幕插值
    // ===========================================================
    async Task FadeTo(float targetAlpha, float duration)
    {
        if (!_group) return;
        float start = _group.alpha;

        if (Mathf.Approximately(start, targetAlpha) || duration <= 0f)
        {
            _group.alpha = targetAlpha;
            return;
        }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / duration;
            _group.alpha = Mathf.Lerp(start, targetAlpha, ease.Evaluate(Mathf.Clamp01(t)));
            await Task.Yield();
        }
        _group.alpha = targetAlpha;
    }
}
