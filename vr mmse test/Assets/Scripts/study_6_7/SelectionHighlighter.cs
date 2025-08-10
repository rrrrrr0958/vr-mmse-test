using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

// 只依賴 Hover 顯示藍圈；被 ManualSelect() 後白圈常亮（直到別的被選中）
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable))]
public class SelectionHighlighter : MonoBehaviour
{
    #region Inspector
    [Header("Size（統一大小）")]
    [Min(0.001f)] public float ringRadius = 0.18f;   // 半徑（公尺）
    [Min(0.001f)] public float ringWidth  = 0.03f;   // 線寬
    public float yOffset = 0.02f;                    // 離地高度
    [Range(12,256)] public int segments = 64;        // 圓解析度

    [Header("Visual")]
    public Color hoverColor  = new Color(0.20f, 0.80f, 1.00f); // 懸停顏色
    public Color selectColor = new Color(2.0f,  2.0f,  2.0f);  // 選取顏色（HDR 讓 Bloom 更明顯）

    [Header("Behaviour")]
    public bool compensateParentScale = true; // 抵銷父層縮放，半徑固定以世界公尺計
    public int sortingOrder = 4000;           // 確保不被其它透明物擋掉
    #endregion

    const string kRingName = "HighlightRing";

    // 注意：你專案在 XRI v3，型別位於 Interactables 子命名空間
    private UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;

    private LineRenderer ring;
    private Material ringMat;
    private Coroutine pulseCo;
    private bool _stickySelected = false; // 被 dwell 選取後維持白圈

    #region Public API（給 Dwell 腳本呼叫）
    /// <summary>被 dwell 選取時呼叫：白圈常亮且保證唯一</summary>
    public void ManualSelect()
    {
        SelectionHighlightRegistry.Take(this); // 關掉上一個
        _stickySelected = true;

        if (ring == null) return;
        SetRingColor(selectColor);
        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = StartCoroutine(Pulse());
        ring.enabled = true;
    }

    /// <summary>外部取消選取（例如 Registry 切換目標時）</summary>
    public void ManualDeselect()
    {
        _stickySelected = false;
        ForceDeselect();
    }
    #endregion

    #region Unity
    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        CreateOrReuseRing();
        BindHoverEvents(true);
    }

    void OnDestroy() => BindHoverEvents(false);

    void LateUpdate()
    {
        if (ring) ApplyScaleCompensation(ring.transform);
    }
    #endregion

    #region Events（只保留 Hover）
    void BindHoverEvents(bool on)
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

    void OnHoverEntered(HoverEnterEventArgs _)
    {
        // 進入 hover：若已被選取顯示白圈；否則藍圈
        ring.enabled = true;
        SetRingColor(_stickySelected ? selectColor : hoverColor);
    }

    void OnHoverExited(HoverExitEventArgs _)
    {
        // 離開 hover：若已被 dwell 選取則維持白圈，否則關閉
        if (_stickySelected)
        {
            ring.enabled = true;
            SetRingColor(selectColor);
        }
        else
        {
            ring.enabled = false;
        }
    }
    #endregion

    #region Ring build
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

        // URP 粒子 Unlit（吃頂點色），排序抬高避免被遮
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
    #endregion

    #region Helpers
    void SetRingColor(Color c)
    {
        if (!ring) return;
        ring.startColor = c;
        ring.endColor   = c;
    }

    public void ForceDeselect()
    {
        if (!ring) return;
        ring.enabled = false;
        if (pulseCo != null) { StopCoroutine(pulseCo); pulseCo = null; }
    }

    IEnumerator Pulse()
    {
        float t = 0f;
        float baseW = ringWidth;
        ring.enabled = true;
        // 保持呼吸直到外部切換（ManualDeselect / Registry）
        while (_stickySelected)
        {
            t += Time.deltaTime * 4f;
            ring.widthMultiplier = baseW * (1f + 0.15f * Mathf.Sin(t));
            yield return null;
        }
        ring.widthMultiplier = baseW;
    }
    #endregion

#if UNITY_EDITOR
    void OnValidate()
    {
        if (ring != null)
        {
            ApplyScaleCompensation(ring.transform);
            RebuildCircle();
            var mr = ring.GetComponent<MeshRenderer>();
            if (mr) mr.sortingOrder = sortingOrder;
        }
    }
#endif
}
