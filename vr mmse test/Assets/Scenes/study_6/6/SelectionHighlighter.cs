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
    public float zOffset    = 0.00f;
    public int   segments   = 64;

    [Header("Visual")]
    public Color hoverColor  = new Color(0.20f, 0.80f, 1.00f); // 藍
    public Color selectColor = new Color(2.0f,  2.0f,  2.0f);  // 白（HDR）

    [Header("Behaviour")]
    public bool compensateParentScale = true;
    public int  sortingOrder = 4000;

    

    [Header("Orientation")]
    [Tooltip("繞 Y 軸額外旋轉角度（例如 180° 做翻轉）")]
    public float yawDegrees = 0f;

    const string kRingName = "HighlightRing";

    UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    LineRenderer ring;
    Material ringMat;
    Coroutine pulseCo;

    // 便捷：目前是否就是「全域唯一選中者」
    public bool IsCurrentSelected => SelectionHighlightRegistry.Current == this;

    bool Game1Active =>
        GameDirector.Instance == null || GameDirector.Instance.CanInteractGame1();

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
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
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

        t.localPosition = new Vector3(0f, yOffset, zOffset);
        t.localRotation = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, yawDegrees, 0f);
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

        var mr = ring.GetComponent<MeshRenderer>();
        if (mr) mr.sortingOrder = sortingOrder;

        RebuildCircle();
        ring.enabled = false;
    }

    void RebuildCircle()
    {
        if (!ring) return;

        var pts = new Vector3[segments];
        float r = Mathf.Max(0.0001f, ringRadius);
        for (int i = 0; i < segments; i++)
        {
            float a = i * Mathf.PI * 2f / segments;
            pts[i] = new Vector3(Mathf.Cos(a) * r, 0f, Mathf.Sin(a) * r);
        }
        ring.positionCount = segments;
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
        if (!ring) return;

        // Play/編輯器中參數改動即時反映
        ring.transform.localPosition = new Vector3(0f, yOffset, zOffset);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, yawDegrees, 0f);
        ApplyScaleCompensation(ring.transform);

        // ★ 若已不在 Game1，且不是目前選中的那顆 → 一律關閉圈圈
        if (!Game1Active && !IsCurrentSelected)
            ring.enabled = false;
    }

    void SetRingColor(Color c)
    {
        if (!ring) return;
        ring.startColor = c;
        ring.endColor = c;
    }

    // 公開：供 Registry 或外部關閉
    public void ForceDeselect()
    {
        if (!ring) return;
        ring.enabled = false;
        if (pulseCo != null) { StopCoroutine(pulseCo); pulseCo = null; }
    }

    // 被選中 → 成為全域唯一選中者（白圈常亮）
    public void ManualSelect()
    {
        SelectionHighlightRegistry.Take(this);
        if (!ring) return;

        SetRingColor(selectColor);
        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = StartCoroutine(Pulse());
        ring.enabled = true;
    }

    // 事件：僅用來處理 hover 顏色（Game2 時禁止顯示）
    void OnHoverEntered(HoverEnterEventArgs _)
    {
        if (!ring) return;

        if (!Game1Active)
        {
            ring.enabled = false;       // ★ Game2/已鎖定 → 禁止藍圈
            return;
        }

        ring.enabled = true;
        SetRingColor(IsCurrentSelected ? selectColor : hoverColor);
    }

    void OnHoverExited(HoverExitEventArgs _)
    {
        if (!ring) return;

        if (!Game1Active)
        {
            ring.enabled = false;       // ★ Game2/已鎖定 → 保險關閉
            return;
        }

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
            ring.transform.localPosition = new Vector3(0f, yOffset, zOffset);
            ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, yawDegrees, 0f);

            var mr = ring.GetComponent<MeshRenderer>();
            if (mr) mr.sortingOrder = sortingOrder;
        }
    }
#endif
}
