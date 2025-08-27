using UnityEngine;
using System.Collections;

public class PlayerRigMover : MonoBehaviour
{
    [Header("Optional fade overlay (CanvasGroup on a full-screen black Image)")]
    public CanvasGroup fadeCanvas;
    public float fadeTime = 0.15f;

    public void MoveTo(Transform target)
    {
        if (target == null) return;
        StartCoroutine(MoveRoutine(target));
    }

    IEnumerator MoveRoutine(Transform target)
    {
        yield return Fade(1f);
        transform.SetPositionAndRotation(target.position, target.rotation);
        yield return Fade(0f);

        // 通知出題（若場景中有 SessionController）
        var sc = Object.FindObjectOfType<SessionController>();
        if (sc != null) sc.AskWhereAmINow();
    }

    IEnumerator Fade(float to)
    {
        if (fadeCanvas == null) yield break;
        float from = fadeCanvas.alpha;
        float t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            fadeCanvas.alpha = Mathf.Lerp(from, to, t / fadeTime);
            yield return null;
        }
        fadeCanvas.alpha = to;
    }
}
