using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using TMPro;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable))]
public class DwellSelectOnHover : MonoBehaviour
{
    [Header("停留選取")]
    public float dwellSeconds = 5f;   // 停留幾秒才選取

    // ===== 固定大小 / 穩定擺放（改這幾個數字就好）=====
    const float kTextYOffset   = 0.08f;  // 從「物件最上緣」再往上抬
    const float kPushToCamera  = 0.15f;  // 再沿 物件→相機 推出去（避免卡在櫃子裡）
    const float kWorldScale    = 0.9f;   // 文字世界尺寸
    const float kFontSize      = 3.0f;   // font size
    const bool  kFlip180       = true;   // 若你在 HMD 裡看到數字鏡像，改 true（預設已 true）

    UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
    SelectionHighlighter highlighter;
    SelectableTarget selectable;

    Camera cam;
    TMP_Text countdownText;
    Transform textTf;

    Collider[]  _colliders;
    Renderer[]  _renderers;

    Coroutine dwellCo;

    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
        highlighter  = GetComponent<SelectionHighlighter>();
        selectable   = GetComponent<SelectableTarget>();
        cam = Camera.main ?? Camera.current;

        _colliders  = GetComponentsInChildren<Collider>(true);
        _renderers  = GetComponentsInChildren<Renderer>(true);

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

    // ----------------- Hover / Dwell -----------------
    void OnHoverEntered(HoverEnterEventArgs _)
    {
        // 若本物件就是「目前選中者」，沒必要再倒數
        if (highlighter != null && highlighter.IsCurrentSelected)
        {
            HideText();
            return;
        }
        StartDwell();
    }

    void OnHoverExited(HoverExitEventArgs _)
    {
        StopDwell();
    }

    void OnAnySelected(SelectEnterEventArgs _)
    {
        HideText();
        StopDwell();
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
        float t = 0f;
        ShowText();
        while (t < dwellSeconds)
        {
            // 中途若變成目前選中者，就結束倒數
            if (highlighter != null && highlighter.IsCurrentSelected)
            {
                HideText();
                yield break;
            }

            t += Time.deltaTime;
            float remain = Mathf.Max(0f, dwellSeconds - t);
            countdownText.text = $"{remain:0.0}";
            yield return null;
        }

        HideText();
        dwellCo = null;

        // 正式選取
        if (highlighter) highlighter.ManualSelect();
        if (selectable)  selectable.Submit();
    }

    // ----------------- 文字生成 / 擺放 -----------------
    void BuildCountdownText()
    {
        var go = new GameObject("DwellText");
        go.transform.SetParent(transform, false);
        textTf = go.transform;

        // 先給固定世界大小，LateUpdate 每幀再校正
        textTf.localScale = Vector3.one * kWorldScale;

        var tmp = go.AddComponent<TextMeshPro>();
        tmp.text = "";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = false;
        tmp.fontSize = kFontSize;
        tmp.color = Color.white;
        tmp.outlineWidth = 0.3f;
        tmp.outlineColor = new Color(0, 0, 0, 0.85f);

        // 顯示在最前面
        var mr = tmp.GetComponent<MeshRenderer>();
        mr.sortingOrder = 5000;

        countdownText = tmp;
        HideText();
    }

    void LateUpdate()
    {
        if (!textTf || !countdownText) return;
        if (!cam) cam = Camera.main ?? Camera.current;
        if (!cam) return;

        // 1) 找到「物件的最上緣中心」
        Vector3 top = GetTopOfBounds();
        Vector3 basePos = top + Vector3.up * kTextYOffset;

        // 2) 往相機方向推，確保在你與物件之間
        Vector3 dirToCam = (cam.transform.position - basePos).normalized;
        textTf.position = basePos + dirToCam * kPushToCamera;

        // 3) 面向相機（穩定 billboard）
        var rot = Quaternion.LookRotation(cam.transform.position - textTf.position, Vector3.up);
        if (kFlip180) rot *= Quaternion.Euler(0f, 180f, 0f); // 若看到鏡像就開啟：我們預設已開
        textTf.rotation = rot;

        // 4) 固定世界大小（不受父節點縮放）
        textTf.localScale = Vector3.one * kWorldScale;
    }

    Vector3 GetTopOfBounds()
    {
        bool has = false; Bounds b = new Bounds(transform.position, Vector3.zero);

        // 優先用 Collider
        foreach (var c in _colliders)
        {
            if (!c || !c.enabled) continue;
            if (!has) { b = c.bounds; has = true; }
            else b.Encapsulate(c.bounds);
        }
        // 沒 Collider 就用 Renderer
        if (!has)
        {
            foreach (var r in _renderers)
            {
                if (!r || !r.enabled) continue;
                if (!has) { b = r.bounds; has = true; }
                else b.Encapsulate(r.bounds);
            }
        }
        if (!has) return transform.position;
        return new Vector3(b.center.x, b.max.y, b.center.z);
    }

    void ShowText() { if (countdownText) countdownText.gameObject.SetActive(true); }
    void HideText() { if (countdownText) countdownText.gameObject.SetActive(false); }
}
