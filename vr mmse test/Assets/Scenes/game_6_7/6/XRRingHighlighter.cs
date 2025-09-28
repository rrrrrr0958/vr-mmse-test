using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using System.Collections.Generic;

[RequireComponent(typeof(Collider))]
public class XRRingHighlighter : MonoBehaviour
{
    [Header("Ring Shape")]
    [Tooltip("半徑倍率（自動讀取 Renderer.bounds 尺寸，再乘上這個倍率）")]
    [Range(0.6f, 2.0f)] public float radiusMultiplier = 1.1f;
    [Tooltip("圓環垂直偏移（離地/桌面一點點就不會穿物件）")]
    public float yOffset = 0.01f;
    [Tooltip("圓環線寬")]
    [Range(0.001f, 0.05f)] public float ringWidth = 0.02f;
    [Tooltip("圓環段數（越多越圓）")]
    [Range(12, 256)] public int segments = 96;

    [Header("Colors")]
    public Color idleColor     = new(0.15f, 0.8f, 1f, 0.9f);
    public Color hoverColor    = new(1f, 0.85f, 0.2f, 1f);
    public Color selectedColor = new(1f, 0.3f, 0.3f, 1f);

    [Header("Behaviour")]
    [Tooltip("進入場景就常亮")]
    public bool glowOnStart = true;
    [Tooltip("是否隨相機面向微調朝上（避免被看起來變形）")]
    public bool alignUp = true;

    LineRenderer ring;
    XRBaseInteractable interactable;
    int hovering = 0;
    bool selected = false;

    void Reset()
    {
        if (!GetComponent<XRBaseInteractable>())
            gameObject.AddComponent<XRSimpleInteractable>();
    }

    void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>() ?? gameObject.AddComponent<XRSimpleInteractable>();
        EnsureRing();
        BindEvents(true);
    }

    void Start()
    {
        BuildRingGeometry();
        ApplyColor(glowOnStart ? idleColor : new Color(0,0,0,0));
    }

    void OnDestroy() => BindEvents(false);

    void Update()
    {
        // 若物件大小/位置有變動，保持圓環跟著走
        if (ring) BuildRingGeometry();
    }

    void BindEvents(bool add)
    {
        if (!interactable) return;
        if (add)
        {
            interactable.hoverEntered.AddListener(OnHoverEnter);
            interactable.hoverExited.AddListener(OnHoverExit);
            interactable.selectEntered.AddListener(OnSelectEnter);
            interactable.selectExited.AddListener(OnSelectExit);
        }
        else
        {
            interactable.hoverEntered.RemoveListener(OnHoverEnter);
            interactable.hoverExited.RemoveListener(OnHoverExit);
            interactable.selectEntered.RemoveListener(OnSelectEnter);
            interactable.selectExited.RemoveListener(OnSelectExit);
        }
    }

    void OnHoverEnter(HoverEnterEventArgs _){ hovering++; RefreshColor(); }
    void OnHoverExit (HoverExitEventArgs  _){ hovering = Mathf.Max(0, hovering-1); RefreshColor(); }
    void OnSelectEnter(SelectEnterEventArgs _){ selected = true; RefreshColor(); }
    void OnSelectExit (SelectExitEventArgs  _){ selected = false; RefreshColor(); }

    void RefreshColor()
    {
        if (!ring) return;
        if (selected) ApplyColor(selectedColor);
        else if (hovering > 0) ApplyColor(hoverColor);
        else if (glowOnStart) ApplyColor(idleColor);
        else ApplyColor(new Color(0,0,0,0));
    }

    void EnsureRing()
    {
        var child = transform.Find("__HighlightRing");
        if (!child)
        {
            var go = new GameObject("__HighlightRing");
            go.transform.SetParent(transform, false);
            child = go.transform;
        }

        ring = child.GetComponent<LineRenderer>();
        if (!ring) ring = child.gameObject.AddComponent<LineRenderer>();

        ring.loop = true;
        ring.useWorldSpace = false;          // 相對於父物件畫
        ring.positionCount = Mathf.Max(segments, 12);
        ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        ring.receiveShadows = false;
        ring.alignment = LineAlignment.View; // 對著相機，外觀更穩
        ring.textureMode = LineTextureMode.Stretch;
        ring.widthMultiplier = ringWidth;

        // 用內建材質即可（不依賴特別 shader）
        if (!ring.sharedMaterial)
        {
            var mat = new Material(Shader.Find("Sprites/Default")); // 支援透明
            mat.renderQueue = 3000; // 在大多數不透明物件之後渲染
            ring.material = mat;
        }
    }

    void BuildRingGeometry()
    {
        if (!ring) return;
        ring.widthMultiplier = ringWidth;

        // 估半徑：取所有 Renderer bounds 的 XZ 最大半徑
        float radius = 0.2f; // 預設
        var rends = GetComponentsInChildren<Renderer>();
        if (rends.Length > 0)
        {
            var b = rends[0].bounds;
            foreach (var r in rends) b.Encapsulate(r.bounds);
            float rX = b.extents.x;
            float rZ = b.extents.z;
            radius = Mathf.Max(rX, rZ) * radiusMultiplier;
            ring.transform.localPosition = new Vector3(0, (b.center.y - transform.position.y) - b.extents.y + yOffset, 0);
        }
        else
        {
            ring.transform.localPosition = new Vector3(0, yOffset, 0);
        }

        int N = Mathf.Max(segments, 12);
        if (ring.positionCount != N) ring.positionCount = N;

        // 畫在 XZ 平面
        var pts = s_pts;
        pts.Clear();
        float step = Mathf.PI * 2f / N;
        for (int i = 0; i < N; i++)
        {
            float a = step * i;
            pts.Add(new Vector3(Mathf.Cos(a) * radius, 0f, Mathf.Sin(a) * radius));
        }
        ring.SetPositions(pts.ToArray());

        if (alignUp)
            ring.transform.up = Vector3.up; // 保持水平
    }

    void ApplyColor(Color c)
    {
        if (!ring) return;
        ring.startColor = c;
        ring.endColor = c;
        if (ring.material) ring.material.color = c; // Sprites/Default 直接吃顏色
    }

    static readonly List<Vector3> s_pts = new();
}
