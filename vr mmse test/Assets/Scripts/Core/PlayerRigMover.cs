using UnityEngine;
using System.Collections;

public class PlayerRigMover : MonoBehaviour
{
    [Header("Fade")]
    public CanvasGroup fadeCanvas;
    public float fadeTime = 0.15f;

    [Header("Rig parts")]
    public Transform cameraTransform; // 指向場景中的 Main Camera（PlayerRig 的子物件）

    void Awake()
    {
        if (cameraTransform == null && Camera.main != null)
            cameraTransform = Camera.main.transform;
    }

    public void MoveTo(Transform targetCamPose)
    {
        if (targetCamPose == null) return;
        StartCoroutine(MoveRoutine(targetCamPose));
    }

    IEnumerator MoveRoutine(Transform targetCamPose)
    {
        SetBlock(true);
        yield return Fade(1f);

        // 讓「相機」對齊目標 → 反推出「Rig 根」該放哪
        // 1) 相機在 Rig 底下的「本地位姿」
        Vector3 camLocalPos = transform.InverseTransformPoint(cameraTransform.position);
        Quaternion camLocalRot = Quaternion.Inverse(transform.rotation) * cameraTransform.rotation;

        // 2) 先算 Rig 需要的旋轉，讓 camLocalRot 被乘上去後 = 目標旋轉
        Quaternion rigRot = targetCamPose.rotation * Quaternion.Inverse(camLocalRot);

        // 3) 再算 Rig 需要的位置，讓 rigRot * camLocalPos 位移後落在目標位置
        Vector3 rigPos = targetCamPose.position - (rigRot * camLocalPos);

        transform.SetPositionAndRotation(rigPos, rigRot);

        yield return Fade(0f);
        SetBlock(false);

        var sc = FindObjectOfType<SessionController>();
        if (sc) sc.AskWhereAmINow();
    }

    IEnumerator Fade(float to)
    {
        if (fadeCanvas == null) yield break;
        float from = fadeCanvas.alpha, t = 0f;
        while (t < fadeTime)
        {
            t += Time.deltaTime;
            fadeCanvas.alpha = Mathf.Lerp(from, to, t / fadeTime);
            yield return null;
        }
        fadeCanvas.alpha = to;
    }

    void SetBlock(bool on)
    {
        if (fadeCanvas == null) return;
        fadeCanvas.blocksRaycasts = on;
        fadeCanvas.interactable   = on;
    }
}
