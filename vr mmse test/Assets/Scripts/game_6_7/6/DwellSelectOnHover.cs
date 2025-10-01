using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

[DisallowMultipleComponent]
[ExecuteAlways]
public class DwellSelectOnHover : MonoBehaviour
{
    [Header("停留選取")]
    public float dwellSeconds = 3f;
    public bool showTenths = true;

    [Header("Position / Rotation")]
    public Vector3 localOffset = new Vector3(0f, 0.05f, 0.12f);
    public Vector3 localEuler = Vector3.zero;

    public enum BillboardMode { None, YAxisOnly, Full }
    public BillboardMode billboard = BillboardMode.None;

    [Header("Scale / Render")]
    public float textWorldScale = 0.90f;
    public bool compensateParentScale = true;
    public int sortingOrder = 5000;

    [Header("Text Style")]
    public float fontSize = 2.0f;
    public Color textColor = Color.white;
    [Range(0, 1f)] public float outlineWidth = 0.3f;
    public Color outlineColor = new Color(0, 0, 0, 0.85f);

    [Header("Manual Text (方法A)")]
    [SerializeField] TMP_Text countdownText;

    [Header("Editor Preview / Control")]
    public bool previewInEditor = true;
    public string editorPreviewText = "3.0";
    public bool lockPositionInEditor = false;

    UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    SelectionHighlighter highlighter;
    SelectableTarget selectable;

    Transform textTf;
    Coroutine dwellCo;

    // 多手容錯：記住目前有哪些 interactor 在 hover 我
    readonly HashSet<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor> _hoverers = new HashSet<UnityEngine.XR.Interaction.Toolkit.Interactors.IXRInteractor>();
    bool iOwnHoverLock = false;

    void Awake()
    {
        TryGetDependencies();
        EnsureCountdownTextExists();
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

        // 保險：被停用時若持有鎖，釋放之
        if (Application.isPlaying && iOwnHoverLock && highlighter != null)
        {
            SelectionHighlightRegistry.ReleaseHover(highlighter);
            iOwnHoverLock = false;
            if (highlighter) highlighter.EndHoverVisualsIfNotSelected();
        }

        _hoverers.Clear();
        HideText();
        if (dwellCo != null) { StopCoroutine(dwellCo); dwellCo = null; }
    }

    void LateUpdate()
    {
        if (Application.isPlaying || lockPositionInEditor)
            UpdateTransformNow();

        if (!Application.isPlaying) RefreshEditorPreview();
    }

    void OnHoverEntered(HoverEnterEventArgs e)
    {
        if (e != null && e.interactorObject != null)
            _hoverers.Add(e.interactorObject);

        var qm = FindObjectOfType<QuizManager>();
        if (qm != null && !qm.CanInteract()) { HideText(); return; }

        // 只在「第一位」手進來時啟動倒數（避免多次啟動）
        if (_hoverers.Count == 1)
            StartDwell();
    }

    void OnHoverExited(HoverExitEventArgs e)
    {
        if (e != null && e.interactorObject != null)
            _hoverers.Remove(e.interactorObject);

        // 只有當「最後一位」離開時，才真正停止倒數與釋放鎖
        if (_hoverers.Count == 0)
        {
            if (iOwnHoverLock && highlighter != null)
            {
                SelectionHighlightRegistry.ReleaseHover(highlighter);
                iOwnHoverLock = false;
            }
            StopDwell();
            if (highlighter) highlighter.EndHoverVisualsIfNotSelected();
        }
        else
        {
            // 仍有人在上方，維持倒數
        }
    }

    void OnAnySelected(SelectEnterEventArgs _)
    {
        HideText();
        StopDwell();
        // 選中後圈圈持續由 SelectionHighlighter 維持
    }

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
        var me = highlighter;
        if (me == null) yield break;

        var qm = FindObjectOfType<QuizManager>();
        if (qm != null && !qm.CanInteract()) yield break;

        // 等待「有手在上面」且拿到全域 hover 鎖
        while (_hoverers.Count > 0 && !SelectionHighlightRegistry.TryAcquireHover(me))
            yield return null;

        // 若此時已經沒有任何手，就不開始
        if (_hoverers.Count == 0) yield break;

        // 我持有鎖
        iOwnHoverLock = true;

        // 補啟 hover 視覺與倒數 UI
        if (highlighter) highlighter.BeginHoverVisuals();
        ShowText();

        float t = 0f;
        while (t < dwellSeconds)
        {
            // 期間若關卡鎖互動、或沒有手在上面、或鎖被搶走，就中止
            qm = FindObjectOfType<QuizManager>();
            if (qm != null && !qm.CanInteract()) { HideText(); yield break; }

            if (_hoverers.Count == 0) { HideText(); yield break; }

            if (SelectionHighlightRegistry.IsHoverLockedFor(me)) { HideText(); yield break; }

            t += Time.deltaTime;
            float remain = Mathf.Max(0f, dwellSeconds - t);
            if (countdownText) countdownText.text = showTenths ? $"{remain:0.0}" : $"{Mathf.CeilToInt(remain)}";
            yield return null;
        }

        HideText();
        dwellCo = null;

        // 倒數完成 → 釋放鎖（選取後白圈會由 SelectionHighlighter 維持，不需要鎖）
        if (iOwnHoverLock && highlighter != null)
        {
            SelectionHighlightRegistry.ReleaseHover(highlighter);
            iOwnHoverLock = false;
        }

        // 送出選取
        if (highlighter) highlighter.ManualSelect();
        if (selectable) selectable.Submit();
    }

    void TryGetDependencies()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        highlighter = GetComponent<SelectionHighlighter>();
        selectable  = GetComponent<SelectableTarget>();
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
        TryGetDependencies();
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
