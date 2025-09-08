using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class PlayerRigMover : MonoBehaviour {
    [Header("Rig")]
    [Tooltip("實際要被移動的物件。不設則使用本物件 transform。")]
    public Transform rigRoot;

    [Header("Refs")]
    public Transform cameraTransform;   // 指向主相機（非 VR/VR 皆可）
    public CanvasGroup fadeOverlay;     // 全螢幕黑幕（Alpha 0~1）

    [Header("Fade")]
    public float fadeDuration = 0.25f;

    [System.Serializable] public class TeleportEvent : UnityEvent {}
    public TeleportEvent OnTeleported;  // 瞬移完成事件（給 SessionController）

    CharacterController _cc;
    Rigidbody _rb;
    bool _isMoving;

    void Awake() {
        if (!rigRoot) rigRoot = transform;
        _cc = rigRoot.GetComponent<CharacterController>();
        if (!_cc) _cc = rigRoot.GetComponentInParent<CharacterController>();
        _rb = rigRoot.GetComponent<Rigidbody>();

        if (!cameraTransform) {
            var cam = GetComponentInChildren<Camera>();
            if (cam) cameraTransform = cam.transform;
        }
        if (!fadeOverlay) {
            var fo = GameObject.Find("FadeOverlay");
            if (fo) fadeOverlay = fo.GetComponent<CanvasGroup>();
        }
    }

    /// <summary>瞬移到指定 VP（含淡入淡出）</summary>
    public void GoTo(Transform targetVP) {
        PrintChain(targetVP);
        Debug.Log($"[Mover] GoTo '{targetVP?.name}'");
        if (!targetVP) return;
        if (_isMoving) return; // 防重入
        StopAllCoroutines();
        StartCoroutine(MoveRoutine(targetVP));
    }

    IEnumerator MoveRoutine(Transform targetVP) {
        _isMoving = true;

        // 淡出
        if (fadeOverlay) yield return FadeTo(1f, fadeDuration);

        // 暫停會干擾瞬移的元件
        bool ccWasEnabled = false;
        if (_cc) { ccWasEnabled = _cc.enabled; _cc.enabled = false; }
        bool rbWasKinematic = false;
        RigidbodyConstraints rbOldConstraints = RigidbodyConstraints.None;
        if (_rb) {
            rbWasKinematic = _rb.isKinematic;
            rbOldConstraints = _rb.constraints;
            _rb.isKinematic = true;
            _rb.constraints = RigidbodyConstraints.FreezeAll;
        }

        // 位置 & 朝向（僅 Y 軸朝向）
        var beforePos = rigRoot.position;
        var dest = targetVP.position;
        var fwd = targetVP.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-6f) fwd = rigRoot.forward;

        rigRoot.position = dest;
        rigRoot.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);

        Debug.Log($"[Mover] Teleported: {beforePos:F3} -> {dest:F3}  Δ={Vector3.Distance(beforePos, dest):F2}m");

        // 還原元件
        if (_rb) {
            _rb.constraints = rbOldConstraints;
            _rb.isKinematic = rbWasKinematic;
        }
        if (_cc) _cc.enabled = ccWasEnabled;

        // 淡入
        if (fadeOverlay) yield return FadeTo(0f, fadeDuration);

        OnTeleported?.Invoke();
        _isMoving = false;
    }

    IEnumerator FadeTo(float alpha, float dur) {
        if (!fadeOverlay) yield break;
        float s = fadeOverlay.alpha;
        float t = 0f;
        while (t < dur) {
            t += Time.deltaTime;
            fadeOverlay.alpha = Mathf.Lerp(s, alpha, Mathf.Clamp01(t / dur));
            yield return null;
        }
        fadeOverlay.alpha = alpha;
    }
    string GetPath(Transform t) {
        System.Collections.Generic.List<string> names = new();
        var p = t;
        while (p != null) { names.Add(p.name); p = p.parent; }
        names.Reverse();
        return string.Join("/", names);
    }

    void PrintChain(Transform t) {
        var p = t;
        Debug.Log($"[VP DEBUG] Target='{t.name}' path={GetPath(t)} localPos={t.localPosition} worldPos={t.position}");
        while (p != null) {
            Debug.Log($"[VP DEBUG]  - {p.name}  localScale={p.localScale}  localRotY={p.localEulerAngles.y:0.###}");
            p = p.parent;
        }
    }

    void Reset(){
        if (!rigRoot) rigRoot = transform;
        if (!cameraTransform) {
            var cam = GetComponentInChildren<Camera>();
            if (cam) cameraTransform = cam.transform;
        }
        if (!fadeOverlay) {
            var fo = GameObject.Find("FadeOverlay");
            if (fo) fadeOverlay = fo.GetComponent<CanvasGroup>();
        }
    }
}
