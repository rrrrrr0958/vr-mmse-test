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
    
    [Header("Loading Effect")]
    public float rotationSpeed = 180f; // 每秒旋轉角度
    public float loadingSegmentSize = 0.3f; // 亮部分佔整個圓的比例 (0-1)
    public float loadingDimFactor = 0.5f;   // 暗部分的亮度係數 (0-1)

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
    Coroutine loadingCo;
    float currentRotation = 0f;
    bool isHovering = false;

    // 便捷：目前是否就是「全域唯一選中者」
    public bool IsCurrentSelected => SelectionHighlightRegistry.Current == this;

    // ✅ 改用 QuizManager 取代 GameDirector
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

        // ★ 若不能互動，且不是目前選中的那顆 → 一律關閉圈圈
        if (!CanInteract && !IsCurrentSelected)
            ring.enabled = false;
    }

    void SetRingColor(Color c)
    {
        if (!ring) return;
        ring.startColor = c;
        ring.endColor = c;
    }
    
    // 設置漸層色環效果
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
        
        startPos = startPos - Mathf.Floor(startPos);
        endPos = endPos - Mathf.Floor(endPos);
        
        GradientColorKey[] colorKeys;
        GradientAlphaKey[] alphaKeys = new GradientAlphaKey[2];
        alphaKeys[0].alpha = 1.0f;
        alphaKeys[0].time = 0.0f;
        alphaKeys[1].alpha = 1.0f;
        alphaKeys[1].time = 1.0f;
        
        if (endPos < startPos)
        {
            colorKeys = new GradientColorKey[4];
            colorKeys[0].color = baseColor;
            colorKeys[0].time = 0.0f;
            colorKeys[1].color = dimColor;
            colorKeys[1].time = endPos;
            colorKeys[2].color = dimColor;
            colorKeys[2].time = startPos;
            colorKeys[3].color = baseColor;
            colorKeys[3].time = 1.0f;
        }
        else
        {
            colorKeys = new GradientColorKey[4];
            colorKeys[0].color = dimColor;
            colorKeys[0].time = 0.0f;
            colorKeys[1].color = baseColor;
            colorKeys[1].time = startPos;
            colorKeys[2].color = baseColor;
            colorKeys[2].time = endPos;
            colorKeys[3].color = dimColor;
            colorKeys[3].time = 1.0f;
        }
        
        gradient.SetKeys(colorKeys, alphaKeys);
        ring.colorGradient = gradient;
    }

    public void ForceDeselect()
    {
        if (!ring) return;
        ring.enabled = false;
        if (pulseCo != null) { StopCoroutine(pulseCo); pulseCo = null; }
        if (loadingCo != null) { StopCoroutine(loadingCo); loadingCo = null; }
        isHovering = false;
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

    void OnHoverEntered(HoverEnterEventArgs _)
    {
        if (!ring) return;

        if (!CanInteract)
        {
            ring.enabled = false;
            return;
        }

        isHovering = true;
        ring.enabled = true;
        
        if (!IsCurrentSelected)
        {
            if (loadingCo != null) StopCoroutine(loadingCo);
            loadingCo = StartCoroutine(LoadingAnimation());
        }
        else
        {
            SetRingColor(selectColor);
        }
    }

    void OnHoverExited(HoverExitEventArgs _)
    {
        if (!ring) return;

        isHovering = false;
        
        if (!CanInteract)
        {
            ring.enabled = false;
            return;
        }

        if (loadingCo != null)
        {
            StopCoroutine(loadingCo);
            loadingCo = null;
        }

        if (!IsCurrentSelected && pulseCo == null)
            ring.enabled = false;
        else if (IsCurrentSelected)
            SetRingColor(selectColor);
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
        
        while (isHovering && !IsCurrentSelected)
        {
            currentRotation += rotationSpeed * Time.deltaTime;
            if (currentRotation >= 360f)
                currentRotation -= 360f;
                
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
            loadingDimFactor = Mathf.Clamp01(loadingDimFactor);
        }
    }
#endif
}
