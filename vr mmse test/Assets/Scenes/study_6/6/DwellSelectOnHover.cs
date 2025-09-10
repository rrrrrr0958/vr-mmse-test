using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

[DisallowMultipleComponent]
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable))]
public class DwellSelectOnHover : MonoBehaviour
{
    [Header("停留選取")]
    public float dwellSeconds = 3f;
    public bool  showTenths   = true;

    [Header("Position (統一位置)")]
    public float yOffset    = 0.05f;   // ↑ 往上抬
    public float zOffset    = 0.12f;   // 沿本地 Z 推前/後（正值=往前）

    [Header("Text Size")]
    [Tooltip("TextMeshPro 字號")]
    public float fontSize = 2.0f;
    [Tooltip("文字世界尺寸（不受父縮放）")]
    public float textWorldScale = 0.90f;

    [Header("Visual")]
    public Color textColor = Color.white;
    [Range(0, 1f)] public float outlineWidth = 0.3f;
    public Color outlineColor = new Color(0, 0, 0, 0.85f);

    [Header("Behaviour")]
    public bool compensateParentScale = true;
    public int  sortingOrder = 5000;

    [Header("Orientation")]
    [Tooltip("繞 Y 軸額外旋轉角度（例如 180° 做翻轉）")]
    public float yawDegrees = 0f;      // Y 軸翻轉/旋轉

    // 相依
    UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    SelectionHighlighter highlighter;
    SelectableTarget selectable;

    // 文字
    TMP_Text countdownText;
    Transform textTf;

    Coroutine dwellCo;

    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        highlighter  = GetComponent<SelectionHighlighter>();
        selectable   = GetComponent<SelectableTarget>();

        BuildCountdownText();
        ApplyVisualToTMP();
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

    // -------- Hover / Dwell --------
    void OnHoverEntered(HoverEnterEventArgs _)
    {
        // 已鎖定或不是 Game1 → 不倒數
        if (GameDirector.Instance != null && !GameDirector.Instance.CanInteractGame1())
        { HideText(); return; }

        if (highlighter != null && highlighter.IsCurrentSelected) { HideText(); return; }
        StartDwell();
    }

    void OnHoverExited(HoverExitEventArgs _) => StopDwell();

    void OnAnySelected(SelectEnterEventArgs _) { HideText(); StopDwell(); }

    void StartDwell()
    {
        if (dwellCo != null) StopCoroutine(dwellCo);
        dwellCo = StartCoroutine(DwellRoutine());
    }

    void StopDwell()
    {
        if (dwellCo != null) { StopCoroutine(dwellCo); dwellCo = null; }
        HideText();
    }

    IEnumerator DwellRoutine()
    {
        float t = 0f;
        ShowText();
        while (t < dwellSeconds)
        {
            // 中途若已鎖定或不在 Game1，立刻中止
            if (GameDirector.Instance != null && !GameDirector.Instance.CanInteractGame1())
            { HideText(); yield break; }

            if (highlighter != null && highlighter.IsCurrentSelected) { HideText(); yield break; }

            t += Time.deltaTime;
            float remain = Mathf.Max(0f, dwellSeconds - t);
            countdownText.text = showTenths ? $"{remain:0.0}" : $"{Mathf.CeilToInt(remain)}";
            yield return null;
        }

        HideText();
        dwellCo = null;

        // 正式選取（會一路走到 QuizManager.Submit → GameDirector.LockAndAdvance）
        if (highlighter) highlighter.ManualSelect();
        if (selectable)  selectable.Submit();
    }

    // -------- 建立 / 視覺 --------
    void BuildCountdownText()
    {
        var go = new GameObject("DwellText");
        go.transform.SetParent(transform, false);
        textTf = go.transform;

        // 位置：X=0、Y=Offset、Z=Offset（對齊 SelectionHighlighter）
        textTf.localPosition = new Vector3(0f, yOffset, zOffset);
        textTf.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
        ApplyScaleCompensation(textTf);

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = "";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = false;
        tmp.fontSize = fontSize;

        var mr = tmp.GetComponent<MeshRenderer>();
        mr.sortingOrder = sortingOrder;

        countdownText = tmp;
        ApplyVisualToTMP();
        HideText();
    }

    void ApplyScaleCompensation(Transform t)
    {
        if (!compensateParentScale)
        { t.localScale = Vector3.one * textWorldScale; return; }

        Vector3 s = transform.lossyScale;
        float sx = Mathf.Approximately(s.x, 0f) ? 1f : 1f / s.x;
        float sy = Mathf.Approximately(s.y, 0f) ? 1f : 1f / s.y;
        float sz = Mathf.Approximately(s.z, 0f) ? 1f : 1f / s.z;
        t.localScale = new Vector3(sx, sy, sz) * textWorldScale;
    }

    void ApplyVisualToTMP()
    {
        if (!countdownText) return;
        countdownText.color        = textColor;
        countdownText.fontSize     = fontSize;
        countdownText.outlineWidth = outlineWidth;
        countdownText.outlineColor = outlineColor;

        var mr = countdownText.GetComponent<MeshRenderer>();
        if (mr) mr.sortingOrder = sortingOrder;
    }

    // -------- 每幀位置/朝向更新（對齊 SelectionHighlighter 的 LateUpdate）--------
    void LateUpdate()
    {
        if (!textTf || !countdownText) return;

        ApplyScaleCompensation(textTf);
        textTf.localPosition = new Vector3(0f, yOffset, zOffset);
        textTf.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
    }

    void ShowText() { if (countdownText) countdownText.gameObject.SetActive(true); }
    void HideText() { if (countdownText) countdownText.gameObject.SetActive(false); }

#if UNITY_EDITOR
    void OnValidate()
    {
        ApplyVisualToTMP();
        if (textTf != null)
        {
            ApplyScaleCompensation(textTf);
            textTf.localPosition = new Vector3(0f, yOffset, zOffset);
            textTf.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);

            var mr = countdownText?.GetComponent<MeshRenderer>();
            if (mr) mr.sortingOrder = sortingOrder;
        }
    }
#endif
}
