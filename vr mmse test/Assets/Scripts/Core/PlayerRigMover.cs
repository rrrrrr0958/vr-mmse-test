using UnityEngine;
using UnityEngine.Events;
using System.Collections;
// ✅ XROrigin 在 CoreUtils 命名空間
using Unity.XR.CoreUtils;

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

    [Header("Fade")] public float fadeDuration = 0.25f;

    [Header("Control")]
    [Tooltip("外部可鎖移動（答題/轉場時設為 false）。")]
    public bool allowMove = true;

    public enum VPAnchor { Feet, EyeLevel } // VP 代表腳底或頭部
    [Header("Viewpoint Interpretation")]
    [Tooltip("Feet = 將 VP 視為地面落點；EyeLevel = 將 VP 視為頭部高度。")]
    public VPAnchor vpAnchor = VPAnchor.Feet;
    [Tooltip("額外的垂直位移（正值=往上），用來微調落地高度")]
    public float vpExtraYOffset = 0f;

    [System.Serializable] public class TeleportEvent : UnityEvent { }
    public TeleportEvent OnTeleported;  // 瞬移完成事件（給 SessionController）

    CharacterController _cc;
    Rigidbody _rb;
    bool _isMoving;
    XROrigin _xr; // ✅ 來自 Unity.XR.CoreUtils

    void Awake()
    {
        if (!rigRoot) rigRoot = transform;

        _cc = rigRoot.GetComponent<CharacterController>() ?? rigRoot.GetComponentInParent<CharacterController>();
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

        // ✅ 從父層抓 XROrigin（XR Origin (Action-based) 預置就有）
        _xr = GetComponentInParent<XROrigin>();
    }

    public void GoTo(Transform targetVP)
    {
        if (!allowMove) return;
        if (!targetVP) { Debug.LogWarning("[Mover] GoTo 失敗：targetVP 為空"); return; }
        if (_isMoving) return;

        PrintChain(targetVP);
        Debug.Log($"[Mover] GoTo '{targetVP.name}'");

        StopAllCoroutines();
        StartCoroutine(MoveRoutine(targetVP));
    }

    IEnumerator MoveRoutine(Transform targetVP)
    {
        _isMoving = true;

        if (fadeOverlay) yield return FadeTo(1f, fadeDuration);

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

        var beforePos = rigRoot.position;

        float headHeight = 1.6f;
        if (cameraTransform)
            headHeight = Mathf.Max(0.2f, cameraTransform.position.y - rigRoot.position.y);

        Vector3 desiredFwd = targetVP.forward; desiredFwd.y = 0f;
        if (desiredFwd.sqrMagnitude < 1e-6f) desiredFwd = rigRoot.forward;
        desiredFwd.Normalize();

        // 計算「相機目標世界座標」
        Vector3 desiredCamPos = targetVP.position;
        switch (vpAnchor)
        {
            case VPAnchor.Feet:     desiredCamPos.y += headHeight + vpExtraYOffset; break;
            case VPAnchor.EyeLevel: desiredCamPos.y += vpExtraYOffset;              break;
        }

        if (_xr != null && cameraTransform != null)
        {
            // ✅ 用 XROrigin API 正確定位相機（避免 Camera Offset 造成 x/z/y 漂移）
            _xr.MoveCameraToWorldLocation(desiredCamPos);

            Vector3 camFwd = cameraTransform.forward; camFwd.y = 0f;
            if (camFwd.sqrMagnitude < 1e-6f) camFwd = Vector3.forward;
            camFwd.Normalize();

            float deltaYaw = Vector3.SignedAngle(camFwd, desiredFwd, Vector3.up);
            _xr.RotateAroundCameraUsingOriginUp(deltaYaw);
        }
        else
        {
            // 後備：無 XROrigin（純 PC 測試）
            Vector3 rigPos = targetVP.position;
            switch (vpAnchor)
            {
                case VPAnchor.Feet:     rigPos.y += vpExtraYOffset; break;
                case VPAnchor.EyeLevel: rigPos.y -= headHeight; rigPos.y += vpExtraYOffset; break;
            }
            rigRoot.position = rigPos;
            rigRoot.rotation = Quaternion.LookRotation(desiredFwd, Vector3.up);
        }

        Debug.Log($"[Mover] Teleported: {beforePos:F3} -> {rigRoot.position:F3}  Δ={Vector3.Distance(beforePos, rigRoot.position):F2}m");

        if (_rb)
        {
            _rb.constraints = rbOldConstraints;
            _rb.isKinematic = rbWasKinematic;
        }
        if (_cc) _cc.enabled = ccWasEnabled;

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
        if (_xr == null)
            Debug.Log("[Mover] 場景中未找到 XROrigin，將使用後備路徑定位（僅 PC 測試用）。");
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
        _xr = GetComponentInParent<XROrigin>();
    }
}
