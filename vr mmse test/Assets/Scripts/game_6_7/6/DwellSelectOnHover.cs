using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

[DisallowMultipleComponent]
[ExecuteAlways] // 編輯模式也能看到與調位置
public class DwellSelectOnHover : MonoBehaviour
{
    [Header("停留選取")]
    public float dwellSeconds = 3f;
    public bool showTenths = true;

    [Header("Position / Rotation")]
    [Tooltip("DwellText 的本地座標位移 (X/Y/Z)")]
    public Vector3 localOffset = new Vector3(0f, 0.05f, 0.12f);
    [Tooltip("DwellText 的本地旋轉 (Pitch/Yaw/Roll)")]
    public Vector3 localEuler = Vector3.zero;

    public enum BillboardMode { None, YAxisOnly, Full }
    [Tooltip("看向相機的方式：None 不看相機、YAxisOnly 只繞 Y、Full 完全面向相機")]
    public BillboardMode billboard = BillboardMode.None;

    [Header("Scale / Render")]
    [Tooltip("文字世界尺寸（不受父縮放）")]
    public float textWorldScale = 0.90f;
    public bool compensateParentScale = true;
    public int sortingOrder = 5000;

    [Header("Text Style")]
    [Tooltip("TextMeshPro 字號")]
    public float fontSize = 2.0f;
    public Color textColor = Color.white;
    [Range(0, 1f)] public float outlineWidth = 0.3f;
    public Color outlineColor = new Color(0, 0, 0, 0.85f);

    // ------- 方法A：手動指定永久文字元件 -------
    [Header("Manual Text (方法A)")]
    [Tooltip("把你手動建立的 TextMeshPro（命名建議 DwellText）拖進來；若留空，只有在播放時才會臨時建立")]
    [SerializeField] TMP_Text countdownText;

    [Header("Editor Preview / Control")]
    [Tooltip("非播放時是否顯示預覽文字，方便對位置")]
    public bool previewInEditor = true;
    [Tooltip("非播放時顯示的預覽內容")]
    public string editorPreviewText = "3.0";
    [Tooltip("編輯器下是否由腳本鎖定位置與旋轉到欄位值")]
    public bool lockPositionInEditor = false;

    // 依賴
    UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    SelectionHighlighter highlighter;
    SelectableTarget selectable;

    Transform textTf;
    Coroutine dwellCo;

    // -------- 生命週期 --------
    void Awake()
    {
        TryGetDependencies();
        EnsureCountdownTextExists();   // 優先使用手動指定的
        ApplyVisualToTMP();
        UpdateTransformNow(force: true);
        RefreshEditorPreview();
    }

    void OnEnable()
    {
        TryGetDependencies();
        EnsureCountdownTextExists();
        ApplyVisualToTMP();
        UpdateTransformNow(force: true);
        RefreshEditorPreview();

        if (Application.isPlaying && interactable != null)
        {
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
            interactable.selectEntered.AddListener(OnAnySelected);
        }
    }

    void OnDisable()
    {
        if (Application.isPlaying && interactable != null)
        {
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
            interactable.selectEntered.RemoveListener(OnAnySelected);
        }
    }

    void LateUpdate()
    {
        // 播放中一律套用；編輯器則依 lockPositionInEditor
        if (Application.isPlaying || lockPositionInEditor)
            UpdateTransformNow();

        if (!Application.isPlaying) RefreshEditorPreview();
    }

    // -------- 互動事件（只在播放時使用）--------
    void OnHoverEntered(HoverEnterEventArgs _)
    {
        // ✅ 改成問 QuizManager
        if (FindObjectOfType<QuizManager>()?.CanInteract() == false)
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
            // ✅ 改成問 QuizManager
            if (FindObjectOfType<QuizManager>()?.CanInteract() == false)
            { HideText(); yield break; }

            if (highlighter != null && highlighter.IsCurrentSelected) { HideText(); yield break; }

            t += Time.deltaTime;
            float remain = Mathf.Max(0f, dwellSeconds - t);
            if (countdownText) countdownText.text = showTenths ? $"{remain:0.0}" : $"{Mathf.CeilToInt(remain)}";
            yield return null;
        }

        HideText();
        dwellCo = null;

        // 正式選取
        if (highlighter) highlighter.ManualSelect();
        if (selectable) selectable.Submit();
    }

    // -------- 建立 / 視覺 --------
    void TryGetDependencies()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        highlighter = GetComponent<SelectionHighlighter>();
        selectable = GetComponent<SelectableTarget>();
    }

    void EnsureCountdownTextExists()
    {
        if (countdownText != null) { textTf = countdownText.transform; return; }

        var exist = transform.Find("DwellText");
        if (exist != null) countdownText = exist.GetComponent<TMP_Text>();
        if (countdownText != null) { textTf = countdownText.transform; return; }

        if (!Application.isPlaying) return;

        var go = new GameObject("DwellText");
        go.transform.SetParent(transform, false);
        textTf = go.transform;

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

        countdownText.color = textColor;
        countdownText.fontSize = fontSize;

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            var mat = countdownText.fontSharedMaterial;
            if (mat != null)
            {
                mat.SetFloat(TMPro.ShaderUtilities.ID_OutlineWidth, outlineWidth);
                mat.SetColor(TMPro.ShaderUtilities.ID_OutlineColor, outlineColor);
                UnityEditor.EditorUtility.SetDirty(mat);
            }
        }
        else
#endif
        {
            countdownText.outlineWidth = outlineWidth;
            countdownText.outlineColor = outlineColor;
        }

        var mr = countdownText.GetComponent<MeshRenderer>();
        if (mr) mr.sortingOrder = sortingOrder;
    }

    void UpdateTransformNow(bool force = false)
    {
        if (!countdownText) return;
        if (textTf == null) textTf = countdownText.transform;

        if (force || Application.isPlaying || lockPositionInEditor)
        {
            ApplyScaleCompensation(textTf);
            textTf.localPosition = localOffset;
        }

        Quaternion finalRot = Quaternion.Euler(localEuler);

        var cam = Camera.main;
        if (billboard != BillboardMode.None && cam != null)
        {
            if (billboard == BillboardMode.Full)
            {
                Vector3 dir = (cam.transform.position - textTf.position);
                if (dir.sqrMagnitude > 0.0001f)
                    finalRot = Quaternion.LookRotation(dir) * Quaternion.Euler(localEuler);
            }
            else
            {
                Vector3 dir = cam.transform.position - textTf.position;
                dir.y = 0f;
                if (dir.sqrMagnitude > 0.0001f)
                    finalRot = Quaternion.LookRotation(dir.normalized, Vector3.up) * Quaternion.Euler(localEuler);
            }
        }

        if (force || Application.isPlaying || lockPositionInEditor)
            textTf.rotation = finalRot;
    }

    void ShowText() { if (countdownText) countdownText.gameObject.SetActive(true); }
    void HideText() { if (countdownText) countdownText.gameObject.SetActive(false); }

    void RefreshEditorPreview()
    {
        if (Application.isPlaying || !countdownText) return;
        countdownText.gameObject.SetActive(previewInEditor);
        if (previewInEditor) countdownText.text = editorPreviewText;
    }

#if UNITY_EDITOR
    void OnValidate()
    {
        EnsureCountdownTextExists();
        ApplyVisualToTMP();

        if (Application.isPlaying || lockPositionInEditor)
            UpdateTransformNow(force: true);

        if (!Application.isPlaying && countdownText)
        {
            var thisRef = this;
            TMPro.TMP_Text textRef = countdownText;

            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (thisRef && textRef)
                {
                    textRef.gameObject.SetActive(previewInEditor);
                    if (previewInEditor) textRef.text = editorPreviewText;
                    UnityEditor.EditorUtility.SetDirty(textRef);
                }
            };
        }
    }
#endif
}
