using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable))]
public class SelectionHighlighter : MonoBehaviour
{
    [Header("Size (統一大小)")]
    public float ringRadius = 0.18f;   // 統一半徑（公尺）
    public float ringWidth  = 0.03f;   // 線寬
    public float yOffset    = 0.02f;   // 離地（或底部）高度
    public int   segments   = 64;      // 圓的解析度

    [Header("Visual")]
    public Color hoverColor  = new Color(0.20f, 0.80f, 1.00f); // 懸停
    public Color selectColor = new Color(2.0f,  2.0f,  2.0f);  // 選取（HDR 亮一點會 Bloom）

    [Header("Behaviour")]
    public bool compensateParentScale = true; // 抵銷父層縮放，確保半徑以世界公尺為準

    const string kRingName = "HighlightRing";

    UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    LineRenderer ring;
    Material ringMat;
    Coroutine pulseCo;

    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        CreateOrReuseRing();
        BindEvents(true);
    }

    void OnDestroy() => BindEvents(false);

    void BindEvents(bool on)
    {
        if (!interactable) return;

        if (on)
        {
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
            interactable.selectEntered.AddListener(OnSelectEntered);
            interactable.selectExited.AddListener(OnSelectExited);
        }
        else
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
            interactable.selectEntered.RemoveListener(OnSelectEntered);
            interactable.selectExited.RemoveListener(OnSelectExited);
        }
    }

    void CreateOrReuseRing()
    {
        // 取／建子物件
        var t = transform.Find(kRingName);
        if (!t)
        {
            var go = new GameObject(kRingName);
            go.transform.SetParent(transform, false);
            t = go.transform;
            ring = go.AddComponent<LineRenderer>();
        }
        else
        {
            ring = t.GetComponent<LineRenderer>() ?? t.gameObject.AddComponent<LineRenderer>();
        }

        // 位置與朝向
        t.localPosition = new Vector3(0f, yOffset, 0f);
        t.localRotation = Quaternion.Euler(90, 0, 0);
        ApplyScaleCompensation(t);

        // LineRenderer 設定
        ring.loop = true;
        ring.useWorldSpace = false;
        ring.positionCount = segments;
        ring.widthMultiplier = ringWidth;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.alignment = LineAlignment.View;

        // 重要：用會吃頂點色的 URP 粒子 Unlit，顏色才會正確顯示
        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        ringMat = new Material(shader);
        ringMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        ring.material = ringMat;

        RebuildCircle();
        ring.enabled = false;
    }

    void RebuildCircle()
    {
        var pts = new Vector3[segments];
        float r = Mathf.Max(0.0001f, ringRadius);
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            pts[i] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }
        ring.SetPositions(pts);
        ring.widthMultiplier = ringWidth;
    }

    void ApplyScaleCompensation(Transform ringTransform)
    {
        if (!compensateParentScale) { ringTransform.localScale = Vector3.one; return; }

        // 抵銷父層世界縮放（只需處理 X/Z，Y 保持 1）
        Vector3 s = transform.lossyScale;
        float sx = Mathf.Approximately(s.x, 0f) ? 1f : 1f / s.x;
        float sz = Mathf.Approximately(s.z, 0f) ? 1f : 1f / s.z;
        ringTransform.localScale = new Vector3(sx, 1f, sz);
    }

    void LateUpdate()
    {
        if (ring) ApplyScaleCompensation(ring.transform);
    }

    void SetRingColor(Color c) { ring.startColor = c; ring.endColor = c; }

    // -------- XRI Events --------
    void OnHoverEntered(HoverEnterEventArgs _)
    {
        ring.enabled = true;
        SetRingColor(hoverColor);
    }

    void OnHoverExited(HoverExitEventArgs _)
    {
        if (pulseCo == null) ring.enabled = false;
    }

    void OnSelectEntered(SelectEnterEventArgs _)
    {
        SetRingColor(selectColor);
        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = StartCoroutine(Pulse());
    }

    void OnSelectExited(SelectExitEventArgs _)
    {
        if (pulseCo != null) { StopCoroutine(pulseCo); pulseCo = null; }
        if (interactable != null && interactable.isHovered) SetRingColor(hoverColor);
        else ring.enabled = false;
    }

    IEnumerator Pulse()
    {
        float t = 0f;
        float baseW = ringWidth;
        ring.enabled = true;
        while (interactable != null && interactable.isSelected)
        {
            t += Time.deltaTime * 4f;
            ring.widthMultiplier = baseW * (1f + 0.15f * Mathf.Sin(t));
            yield return null;
        }
        ring.widthMultiplier = baseW;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        if (ring != null)
        {
            ApplyScaleCompensation(ring.transform);
            RebuildCircle();
        }
    }
#endif
}
