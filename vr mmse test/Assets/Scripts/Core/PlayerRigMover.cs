using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class PlayerRigMover : MonoBehaviour {
    [Header("Refs")]
    public Transform cameraTransform;   // 指向主相機（非 VR/VR 皆可）
    public CanvasGroup fadeOverlay;     // 全螢幕黑幕（Alpha 0~1）

    [Header("Fade")]
    public float fadeDuration = 0.25f;

    [System.Serializable] public class TeleportEvent : UnityEvent {}
    public TeleportEvent OnTeleported;  // 瞬移完成事件（給 SessionController）

    /// <summary>瞬移到指定 VP（含淡入淡出）</summary>
    public void GoTo(Transform targetVP) {
        if (!targetVP) return;
        StopAllCoroutines();
        StartCoroutine(MoveRoutine(targetVP));
    }

    IEnumerator MoveRoutine(Transform targetVP) {
        // 淡出
        if (fadeOverlay) yield return FadeTo(1f, fadeDuration);

        // 位置&朝向（僅 Y 軸朝向）
        transform.position = targetVP.position;
        var fwd = targetVP.forward; fwd.y = 0f;
        if (fwd.sqrMagnitude > 0.0001f)
            transform.rotation = Quaternion.LookRotation(fwd.normalized, Vector3.up);

        // 淡入
        if (fadeOverlay) yield return FadeTo(0f, fadeDuration);

        OnTeleported?.Invoke();
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

    void Reset(){
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
