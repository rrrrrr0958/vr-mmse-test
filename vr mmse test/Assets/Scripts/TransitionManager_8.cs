using System;
using System.Collections;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class TransitionManager : MonoBehaviour
{
    public static TransitionManager I { get; private set; }

    [Header("Hook")]
    public Transform xrOrigin;              // 指到 XR Origin (XR Rig)
    public float vignetteDuringRotate = 0.35f;  // 旋轉/瞬移時的遮罩強度
    public AnimationCurve ease = AnimationCurve.EaseInOut(0,0, 1,1);

    // internal
    Canvas _canvas;
    Image _black;
    CanvasGroup _group;

    void Awake()
    {
        if (I != null) { Destroy(gameObject); return; }
        I = this;
        DontDestroyOnLoad(gameObject);
        EnsureCanvas();
    }

    void EnsureCanvas()
    {
        if (_canvas != null) return;

        var go = new GameObject("[TransitionCanvas]");
        DontDestroyOnLoad(go);
        _canvas = go.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = short.MaxValue; // 確保永遠在最上層

        _group = go.AddComponent<CanvasGroup>();
        _group.alpha = 0f;  // 預設透明
        _group.blocksRaycasts = true; // 防止誤觸 UI

        var imgGO = new GameObject("Black");
        imgGO.transform.SetParent(go.transform, false);
        _black = imgGO.AddComponent<Image>();
        _black.color = Color.black;

        var rt = _black.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    // ====== 公用 API ======

    /// <summary>上下樓：黑幕淡出 → 載入 → 淡入</summary>
    public async Task FadeSceneLoad(string sceneName, float fadeOut=0.4f, float fadeIn=0.4f)
    {
        await FadeTo(1f, fadeOut);                       // 黑
        AsyncOperation op = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Single);
        while (!op.isDone) await Task.Yield();
        await Task.Yield();                               // 讓新場景一幀完成
        await FadeTo(0f, fadeIn);                        // 亮
    }

    /// <summary>
    /// 同場景轉場：輕遮罩 + 平滑旋轉/瞬移到 viewpoint
    /// - 只會改變 XR Origin 的位置與面向（不改變玩家頭部旋轉）
    /// </summary>
    public async Task RotateMoveTo(Transform targetViewpoint, float duration=0.6f, bool move=true)
    {
        if (xrOrigin == null || targetViewpoint == null) return;

        // 微暗 vignette（避免暈眩）
        float baseAlpha = _group.alpha;
        await FadeTo(Mathf.Max(baseAlpha, vignetteDuringRotate), 0.12f);

        // 計算旋轉與（可選的）位置插值
        Vector3 startPos = xrOrigin.position;
        Quaternion startRot = xrOrigin.rotation;

        // 只在水平方向對齊面向（避免 pitch/roll 不適）
        Vector3 forward = targetViewpoint.forward; forward.y = 0f;
        if (forward.sqrMagnitude < 1e-6f) forward = xrOrigin.forward;
        Quaternion endRot = Quaternion.LookRotation(forward.normalized, Vector3.up);

        Vector3 endPos = move ? targetViewpoint.position : startPos;

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / Mathf.Max(0.01f, duration);
            float k = ease.Evaluate(Mathf.Clamp01(t));
            xrOrigin.rotation = Quaternion.Slerp(startRot, endRot, k);
            if (move) xrOrigin.position = Vector3.Lerp(startPos, endPos, k);
            await Task.Yield();
        }

        // 回復亮度
        await FadeTo(baseAlpha, 0.12f);
    }

    // ====== 私用：黑幕插值 ======
    async Task FadeTo(float targetAlpha, float duration)
    {
        float start = _group.alpha;
        if (Mathf.Approximately(start, targetAlpha) || duration <= 0f) { _group.alpha = targetAlpha; return; }

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
