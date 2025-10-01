using System.Collections;   // ← 加這行
using System.Collections.Generic;
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
    public Color hoverColor  = new Color(0.20f, 0.80f, 1.00f);
    public Color selectColor = new Color(2.0f,  2.0f,  2.0f);

    [Header("Loading Effect")]
    public float rotationSpeed = 180f;
    [Range(0f,1f)] public float loadingSegmentSize = 0.3f;
    [Range(0f,1f)] public float loadingDimFactor   = 0.5f;

    [Header("Behaviour")]
    public bool compensateParentScale = true;
    public int  sortingOrder = 4000;

    [Header("Orientation")]
    public float yawDegrees = 0f;

    const string kRingName = "HighlightRing";

    UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    LineRenderer ring;
    Material ringMat;
    Coroutine pulseCo;
    Coroutine loadingCo;
    float currentRotation = 0f;

    // 追蹤目前在「這個物件」上方的所有 Interactor（雙手都算）
    readonly HashSet<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor> _hoverers = new HashSet<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor>();

    // 「我是否已取得 Hover 鎖而且正在顯示 hover 視覺」
    bool _hasHoverLockAndShowing = false;

    public bool IsCurrentSelected => SelectionHighlightRegistry.Current == this;

    bool CanInteract
    {
        get
        {
#if UNITY_2022_2_OR_NEWER
            var qm = FindFirstObjectByType<QuizManager>();
#else
            var qm = FindObjectOfType<QuizManager>();
#endif
            return qm != null && qm.CanInteract();
        }
    }

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
        // 保險釋放鎖
        if (SelectionHighlightRegistry.HoverOwner == this)
            SelectionHighlightRegistry.ReleaseHover(this);
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

        ring.transform.localPosition = new Vector3(0f, yOffset, zOffset);
        ring.transform.localRotation = Quaternion.Euler(90f, 0f, 0f) * Quaternion.Euler(0f, yawDegrees, 0f);
        ApplyScaleCompensation(ring.transform);

        // 鎖關之後，非選定物件一律隱藏；選定者仍應顯示
        if (!CanInteract && !IsCurrentSelected)
            ring.enabled = false;
    }

    void SetRingColor(Color c)
    {
        if (!ring) return;
        ring.startColor = c;
        ring.endColor = c;
    }

    void SetLoadingRingColors(Color baseColor, float rotation)
    {
        if (!ring) return;

        Gradient gradient = new Gradient();
        Color dimColor = new Color(
            baseColor.r * loadingDimFactor,
            baseColor.g * loadingDimFactor,
            baseColor.b * loadingDimFactor,
            baseColor.a
        );

        float startPos = rotation / 360f;
        float endPos = (rotation + loadingSegmentSize * 360f) / 360f;
        startPos -= Mathf.Floor(startPos);
        endPos   -= Mathf.Floor(endPos);

        GradientColorKey[] colorKeys;
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0].alpha = 1.0f; alphaKeys[0].time = 0.0f;
        alphaKeys[1].alpha = 1.0f; alphaKeys[1].time = 1.0f;

        if (endPos < startPos)
        {
            colorKeys = new GradientColorKey[4];
            colorKeys[0] = new GradientColorKey( baseColor, 0f);
            colorKeys[1] = new GradientColorKey( dimColor,  endPos);
            colorKeys[2] = new GradientColorKey( dimColor,  startPos);
            colorKeys[3] = new GradientColorKey( baseColor, 1f);
        }
        else
        {
            colorKeys = new GradientColorKey[4];
            colorKeys[0] = new GradientColorKey( dimColor, 0f);
            colorKeys[1] = new GradientColorKey( baseColor, startPos);
            colorKeys[2] = new GradientColorKey( baseColor, endPos);
            colorKeys[3] = new GradientColorKey( dimColor, 1f);
        }

        gradient.SetKeys(colorKeys, alphaKeys);
        ring.colorGradient = gradient;
    }

    public void ForceDeselect()
    {
        if (!ring) return;
        ring.enabled = false;
        if (pulseCo   != null) { StopCoroutine(pulseCo);    pulseCo    = null; }
        if (loadingCo != null) { StopCoroutine(loadingCo);  loadingCo  = null; }
        _hasHoverLockAndShowing = false;
        _hoverers.Clear();
    }

    public void ManualSelect()
    {
        SelectionHighlightRegistry.Take(this);
        if (!ring) return;

        if (loadingCo != null) { StopCoroutine(loadingCo); loadingCo = null; }
        SetRingColor(selectColor);

        if (pulseCo != null) StopCoroutine(pulseCo);
        pulseCo = StartCoroutine(Pulse());
        ring.enabled = true;
    }

    // 供 DwellSelect 在「等到鎖」之後補啟 hover 視覺
    public void BeginHoverVisuals()
    {
        if (!ring) return;
        _hasHoverLockAndShowing = true;

        if (!IsCurrentSelected)
        {
            if (loadingCo != null) StopCoroutine(loadingCo);
            loadingCo = StartCoroutine(LoadingAnimation());
        }
        else
        {
            SetRingColor(selectColor);
        }

        ring.enabled = true;
    }

    // 只有在非選定狀態才會關掉 hover 視覺
    public void EndHoverVisualsIfNotSelected()
    {
        if (!ring) return;

        _hasHoverLockAndShowing = false;

        if (!IsCurrentSelected)
        {
            if (loadingCo != null) { StopCoroutine(loadingCo); loadingCo = null; }
            ring.enabled = false;
        }
        else
        {
            SetRingColor(selectColor);
        }
    }

    void OnHoverEntered(HoverEnterEventArgs e)
    {
        // 記住這位 interactor
        if (e != null && e.interactorObject != null)
            _hoverers.Add(e.interactorObject);

        if (!ring) return;

        if (!CanInteract)
            return;

        // 試圖直接在本幀取得鎖（若 A 仍持鎖會失敗）
        if (SelectionHighlightRegistry.TryAcquireHover(this))
        {
            BeginHoverVisuals();
        }
        // 失敗就等 DwellSelect 來「等待鎖→補啟」；這裡不主動顯示
    }

    void OnHoverExited(HoverExitEventArgs e)
    {
        if (e != null && e.interactorObject != null)
            _hoverers.Remove(e.interactorObject);

        // 只有當「最後一位」離開時，才釋放鎖與關閉 hover 視覺
        if (_hoverers.Count == 0)
        {
            if (SelectionHighlightRegistry.HoverOwner == this)
                SelectionHighlightRegistry.ReleaseHover(this);

            EndHoverVisualsIfNotSelected();
        }
        else
        {
            // 還有人在上面，就不要動視覺/鎖
        }
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

    IEnumerator LoadingAnimation()
    {
        currentRotation = 0f;
        ring.enabled = true;

        while (_hasHoverLockAndShowing && !IsCurrentSelected)
        {
            currentRotation += rotationSpeed * Time.deltaTime;
            if (currentRotation >= 360f) currentRotation -= 360f;
            SetLoadingRingColors(hoverColor, currentRotation);
            yield return null;
        }

        if (IsCurrentSelected)
            SetRingColor(selectColor);

        loadingCo = null;
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

            loadingSegmentSize = Mathf.Clamp01(loadingSegmentSize);
            loadingDimFactor   = Mathf.Clamp01(loadingDimFactor);
        }
    }
#endif
}
