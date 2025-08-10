using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable))]
public class SelectionHighlighter : MonoBehaviour
{
    [Header("Size (統一大小)")]
    public float ringRadius = 0.18f;
    public float ringWidth  = 0.03f;
    public float yOffset    = 0.02f;
    public int   segments   = 64;

    [Header("Visual")]
    public Color hoverColor  = new Color(0.20f, 0.80f, 1.00f); // 藍
    public Color selectColor = new Color(2.0f,  2.0f,  2.0f);  // 白（HDR）

    [Header("Behaviour")]
    public bool compensateParentScale = true;
    public int  sortingOrder = 4000;

    const string kRingName = "HighlightRing";

    UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    LineRenderer ring;
    Material ringMat;
    Coroutine pulseCo;

    // 便捷：目前是否就是「全域唯一選中者」
    public bool IsCurrentSelected => SelectionHighlightRegistry.Current == this;


    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        CreateOrReuseRing();
        BindEvents(true);
    }

    void OnDestroy()
    {
        BindEvents(false);
        if (IsCurrentSelected) SelectionHighlightRegistry.Clear(this);
    }

    void BindEvents(bool on)
    {
        if (!interactable) return;

        if (on)
        {
            // XRI v3 用 AddListener/RemoveListener
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
            // 你若還有 select/activate 也可掛，但我們現在靠 ManualSelect()
        }
        else
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }
    }

    void CreateOrReuseRing()
    {
        Transform t = transform.Find(kRingName);
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

        t.localPosition = new Vector3(0f, yOffset, 0f);
        t.localRotation = Quaternion.Euler(90, 0, 0);
        ApplyScaleCompensation(t);

        ring.loop = true;
        ring.useWorldSpace = false;
        ring.positionCount = segments;
        ring.widthMultiplier = ringWidth;
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.alignment = LineAlignment.View;

        var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
        ringMat = new Material(shader);
        ringMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        ring.material = ringMat;

        // 讓線也能依顏色排序到前面（保險）
        var mr = ring.GetComponent<MeshRenderer>();
        if (mr) mr.sortingOrder = sortingOrder;

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

    // ========== 公開 API：讓其它系統（例如 DwellSelectOnHover）呼叫 ==========
    // 被選中 → 成為全域唯一選中者（白圈常亮）
    public void ManualSelect()
    {
        SelectionHighlightRegistry.Take(this);   // 把上一顆熄掉，登記我
        if (!ring) return;

        SetRingColor(selectColor);
        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = StartCoroutine(Pulse());
        ring.enabled = true;
    }

    // 被別人搶走或要關閉時呼叫
    public void ForceDeselect()
    {
        if (!ring) return;
        ring.enabled = false;
        if (pulseCo != null) { StopCoroutine(pulseCo); pulseCo = null; }
    }

    // ========== XRI 事件（只用來處理 hover 顏色） ==========
    void OnHoverEntered(HoverEnterEventArgs _)
    {
        ring.enabled = true;
        // 只有「目前被選中的那顆」在 hover 時保持白色，其它顯示藍色
        SetRingColor(IsCurrentSelected ? selectColor : hoverColor);
    }

    void OnHoverExited(HoverExitEventArgs _)
    {
        // 不是目前選中者才在離開時關閉；目前那顆保持常亮
        if (!IsCurrentSelected && pulseCo == null)
            ring.enabled = false;
    }

    IEnumerator Pulse()
    {
        float t = 0f;
        float baseW = ringWidth;
        ring.enabled = true;
        while (SelectionHighlightRegistry.Current == this)
        {
            t += Time.deltaTime * 4f;
            ring.widthMultiplier = baseW * (1f + 0.15f * Mathf.Sin(t));
            yield return null;
        }
        ring.widthMultiplier = baseW;
        pulseCo = null;
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
