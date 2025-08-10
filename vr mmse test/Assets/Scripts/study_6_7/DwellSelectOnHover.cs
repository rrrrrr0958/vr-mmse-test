using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable))]
public class DwellSelectOnHover : MonoBehaviour
{
    [Header("停留選取")]
    public float dwellSeconds = 5f;           // 停留幾秒自動選取
    public bool  showTenths   = true;         // 顯示到小數一位

    [Header("倒數顯示")]
    public float textYOffset      = 0.18f;    // 從物件「頂面」往上
    public float textScale        = 1.2f;     // 世界大小（會做父級縮放補償）
    public float pushTowardCamera = 0.05f;    // 往鏡頭推，避免被遮
    public int   sortingOrder     = 9999;     // 畫在最上層

    [Tooltip("用 Collider/Renderer 的 bounds 定位（較準）；關掉則以物件Transform為基準。")]
    public bool placeByBounds = true;

    [Tooltip("除錯用：永遠顯示倒數文字（不需hover也會顯示）。調完位置再關掉。")]
    public bool debugAlwaysShow = false;

    UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    SelectionHighlighter highlighter;
    SelectableTarget selectable;

    Camera cam;
    TMP_Text countdownText;
    Transform textTf;

    Coroutine dwellCo;

    // 暫存 bounds
    Collider[] _colliders;
    Renderer[] _renderers;

    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        highlighter  = GetComponent<SelectionHighlighter>();
        selectable   = GetComponent<SelectableTarget>();

        _colliders  = GetComponentsInChildren<Collider>(true);
        _renderers  = GetComponentsInChildren<Renderer>(true);

        cam = Camera.main ?? Camera.current;

        BuildCountdownText();
    }

    void OnEnable()
    {
        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
        interactable.selectEntered.AddListener(OnAnySelected);
    }

    void OnDisable()
    {
        interactable.hoverEntered.RemoveListener(OnHoverEntered);
        interactable.hoverExited.RemoveListener(OnHoverExited);
        interactable.selectEntered.RemoveListener(OnAnySelected);
    }

    void LateUpdate()
    {
        if (!countdownText || !textTf) return;

        bool shouldShow = debugAlwaysShow || (dwellCo != null);
        if (countdownText.gameObject.activeSelf != shouldShow)
            countdownText.gameObject.SetActive(shouldShow);

        if (!countdownText.gameObject.activeSelf) return;

        if (!cam) cam = Camera.main ?? Camera.current;
        if (!cam) return;

        // 1) 基準點：bounds 中心 + 往上
        Vector3 basePos = placeByBounds ? BoundsCenterWorld() : transform.position;
        basePos += Vector3.up * textYOffset;

        // 2) 往相機方向推
        Vector3 dirToCam = (cam.transform.position - basePos).normalized;
        textTf.position = basePos + dirToCam * Mathf.Max(0f, pushTowardCamera);

        // 3) 面向相機
        textTf.LookAt(cam.transform);
        textTf.Rotate(0, 180f, 0f);

        // 4) 世界尺度補償：父物件再怎麼縮放都保持同視覺大小
        Vector3 s = transform.lossyScale;
        float avgXZ = Mathf.Max(0.0001f, (Mathf.Abs(s.x) + Mathf.Abs(s.z)) * 0.5f);
        textTf.localScale = Vector3.one * (textScale / avgXZ);
    }

    // 取得綜合 bounds 中心（優先 Collider）
    Vector3 BoundsCenterWorld()
    {
        bool has = false;
        Bounds b = default;

        foreach (var c in _colliders)
        {
            if (!c || !c.enabled) continue;
            if (!has) { b = c.bounds; has = true; }
            else b.Encapsulate(c.bounds);
        }

        if (!has)
        {
            foreach (var r in _renderers)
            {
                if (!r || !r.enabled) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
        }

        return has ? b.center : transform.position;
    }

    // -------- XRI Callbacks --------

    void OnHoverEntered(HoverEnterEventArgs _)
    {
        StartDwell();
    }

    void OnHoverExited(HoverExitEventArgs _)
    {
        StopDwell();
    }

    void OnAnySelected(SelectEnterEventArgs _)
    {
        StopDwell();
        HideText();
    }

    // -------- Dwell --------

    void StartDwell()
    {
        if (dwellCo != null) StopCoroutine(dwellCo);
        dwellCo = StartCoroutine(DwellRoutine());
    }

    void StopDwell()
    {
        if (dwellCo != null) { StopCoroutine(dwellCo); dwellCo = null; }
        if (!debugAlwaysShow) HideText();
    }

    IEnumerator DwellRoutine()
    {
        float t = 0f;
        ShowText();

        while (t < dwellSeconds)
        {
            t += Time.deltaTime;
            float remain = Mathf.Max(0f, dwellSeconds - t);
            countdownText.text = showTenths ? $"{remain:0.0}" : $"{Mathf.CeilToInt(remain)}";
            yield return null;
        }

        HideText();
        dwellCo = null;

        if (highlighter) highlighter.ManualSelect(); // 白圈常亮
        if (selectable)  selectable.Submit();        // 回報答題
    }

    // -------- Text 建立/顯示 --------

    void BuildCountdownText()
    {
        var go = new GameObject("DwellText");
        go.transform.SetParent(transform, false);
        textTf = go.transform;

        var tmp = go.AddComponent<TextMeshPro>(); // 3D TMP
        tmp.text      = "";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = false;
        tmp.fontSize  = 0.6f;                     // 初始字級
        tmp.color     = Color.white;
        tmp.outlineWidth = 0.25f;
        tmp.outlineColor = new Color(0, 0, 0, 0.9f);

        // 排序在最上層 + 關深度測試
        var mr = tmp.GetComponent<MeshRenderer>();
        if (mr) mr.sortingOrder = sortingOrder;

        var mat = new Material(tmp.fontMaterial);
        mat.renderQueue = 5000; // 接近 Overlay
        if (mat.HasProperty("_ZTestMode")) mat.SetInt("_ZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
        else if (mat.HasProperty("_ZTest")) mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        if (mat.HasProperty("_ZWrite"))     mat.SetInt("_ZWrite", 0);
        tmp.fontMaterial = mat;

        countdownText = tmp;
        if (!debugAlwaysShow) HideText();
        else ShowText(); // 除錯模式一開始就顯示
    }

    void ShowText() { if (countdownText) countdownText.gameObject.SetActive(true); }
    void HideText() { if (countdownText) countdownText.gameObject.SetActive(false); }
}
