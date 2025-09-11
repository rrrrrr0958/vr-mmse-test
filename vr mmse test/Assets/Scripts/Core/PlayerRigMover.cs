using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class PlayerRigMover : MonoBehaviour
{
    [Header("Rig")]
    [Tooltip("實際要被移動的物件。不設則使用本物件 transform。")]
    public Transform rigRoot;

    [Header("Refs")]
    [Tooltip("指向玩家的主攝影機（XR 或非 XR 都可）。")]
    public Transform cameraTransform;   // XR: XR Origin 底下的 Camera
    [Tooltip("全螢幕黑幕（Alpha 0~1），可選。")]
    public CanvasGroup fadeOverlay;     // 可無

    [Header("Fade")]
    public float fadeDuration = 0.25f;

    [Header("Control")]
    [Tooltip("外部可鎖移動（答題/轉場時設為 false）。")]
    public bool allowMove = true;

    [System.Serializable] public class TeleportEvent : UnityEvent { }
    public TeleportEvent OnTeleported;  // 瞬移完成事件（給 SessionController）

    CharacterController _cc;
    Rigidbody _rb;
    bool _isMoving;

    void Awake()
    {
        if (!rigRoot) rigRoot = transform;

        _cc = rigRoot.GetComponent<CharacterController>();
        if (!_cc) _cc = rigRoot.GetComponentInParent<CharacterController>();

        _rb = rigRoot.GetComponent<Rigidbody>();

        if (!cameraTransform)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam) cameraTransform = cam.transform;
        }
        if (!fadeOverlay)
        {
            var fo = GameObject.Find("FadeOverlay");
            if (fo) fadeOverlay = fo.GetComponent<CanvasGroup>();
        }
    }

    /// <summary>瞬移到指定 Viewpoint（含淡入淡出與頭高補償）。</summary>
    public void GoTo(Transform targetVP)
    {
        if (!allowMove) return;
        if (!targetVP)
        {
            Debug.LogWarning("[Mover] GoTo 失敗：targetVP 為空");
            return;
        }
        if (_isMoving) return; // 防重入

        PrintChain(targetVP);
        Debug.Log($"[Mover] GoTo '{targetVP.name}'");

        StopAllCoroutines();
        StartCoroutine(MoveRoutine(targetVP));
    }

    IEnumerator MoveRoutine(Transform targetVP)
    {
        _isMoving = true;

        // 淡出
        if (fadeOverlay) yield return FadeTo(1f, fadeDuration);

        // 暫停會干擾瞬移的元件
        bool ccWasEnabled = false;
        if (_cc) { ccWasEnabled = _cc.enabled; _cc.enabled = false; }

        bool rbWasKinematic = false;
        RigidbodyConstraints rbOldConstraints = RigidbodyConstraints.None;
        if (_rb)
        {
            rbWasKinematic = _rb.isKinematic;
            rbOldConstraints = _rb.constraints;
            _rb.isKinematic = true;
            _rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        // === 位置 & 朝向（VR 友善：頭高補償 + 僅改 Y 軸朝向） ===
        var beforePos = rigRoot.position;

        // 取得目前相機相對地面高度（若沒有相機，使用 1.6m 預設）
        float headHeight = 1.6f;
        if (cameraTransform)
            headHeight = Mathf.Max(0.2f, cameraTransform.position.y - rigRoot.position.y);

        // 目的地的「腳底座標」= 目標點 - 頭高
        Vector3 destFeet = new Vector3(
            targetVP.position.x,
            targetVP.position.y - headHeight,
            targetVP.position.z
        );
        rigRoot.position = destFeet;

        // 朝向：依目標 forward 的 Y 平面方向
        Vector3 fwd = targetVP.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude > 1e-6f)
            rigRoot.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);

        Debug.Log($"[Mover] Teleported: {beforePos:F3} -> {rigRoot.position:F3}  Δ={Vector3.Distance(beforePos, rigRoot.position):F2}m");

        // 還原元件
        if (_rb)
        {
            _rb.constraints = rbOldConstraints;
            _rb.isKinematic = rbWasKinematic;
        }
        if (_cc) _cc.enabled = ccWasEnabled;

        // 淡入
        if (fadeOverlay) yield return FadeTo(0f, fadeDuration);

        OnTeleported?.Invoke();
        _isMoving = false;
    }

    IEnumerator FadeTo(float alpha, float dur)
    {
        if (!fadeOverlay) yield break;
        float s = fadeOverlay.alpha;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            fadeOverlay.alpha = Mathf.Lerp(s, alpha, Mathf.Clamp01(t / dur));
            yield return null;
        }
        fadeOverlay.alpha = alpha;
    }

    // ======= 除錯輔助 =======
    string GetPath(Transform t)
    {
        System.Collections.Generic.List<string> names = new();
        var p = t;
        while (p != null) { names.Add(p.name); p = p.parent; }
        names.Reverse();
        return string.Join("/", names);
    }

    void PrintChain(Transform t)
    {
        if (!t) return;
        Debug.Log($"[VP DEBUG] Target='{t.name}' path={GetPath(t)} localPos={t.localPosition} worldPos={t.position}");
        var p = t;
        while (p != null)
        {
            Debug.Log($"[VP DEBUG]  - {p.name}  localScale={p.localScale}  localRotY={p.localEulerAngles.y:0.###}");
            p = p.parent;
        }
        if (!cameraTransform)
            Debug.LogWarning("[Mover] cameraTransform 未指派：將使用預設頭高 1.6m 進行補償。");
    }

    void Reset()
    {
        if (!rigRoot) rigRoot = transform;
        if (!cameraTransform)
        {
            var cam = GetComponentInChildren<Camera>();
            if (cam) cameraTransform = cam.transform;
        }
        if (!fadeOverlay)
        {
            var fo = GameObject.Find("FadeOverlay");
            if (fo) fadeOverlay = fo.GetComponent<CanvasGroup>();
        }
    }
}
