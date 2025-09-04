using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor))]
public class RayDrawDriverXRI : MonoBehaviour
{
    [Header("Whiteboard / Brush")]
    public WhiteBoard whiteboard;
    public int brushIndex = 0;                 // 對應 WhiteBoard.brushes

    [Header("Input")]
    [Tooltip("扳機/Activate 動作（XRI Default Input Actions → Activate）")]
    public InputActionProperty drawAction;

    [Header("Hover Dot")]
    public bool showHoverDot = true;
    [Tooltip("直接指定一個預製物；若為空，會自動做一顆小球")]
    public GameObject hoverDotPrefab;
    [Tooltip("點的世界尺寸（直徑，公尺）")]
    public float hoverDotSize = 0.02f;
    [Tooltip("點的顏色")]
    public Color hoverDotColor = Color.red;
    [Tooltip("點與表面的偏移量，避免 Z-fighting（公尺）")]
    public float surfaceOffset = 0.001f;
    [Tooltip("是否在按著繪圖時也顯示點")]
    public bool showWhileDrawing = true;
    [Tooltip("0~1：位置/旋轉插值，1=瞬間貼齊")]
    [Range(0f,1f)] public float followLerp = 0.5f;

    UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor ray;
    Transform dot;       // 實例
    Material dotMat;     // 動態材質（若自動生成）

    void Awake() {
        ray = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
        EnsureDot();
    }

    void OnEnable()  { if (drawAction.action != null) drawAction.action.Enable(); }
    void OnDisable() { if (drawAction.action != null) drawAction.action.Disable(); }

    void Update()
    {
        bool pressed = drawAction.action != null && drawAction.action.IsPressed();

        // --- 繪圖邏輯（跟之前一樣） ---
        if (pressed && ray.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            if (hit.collider && whiteboard && hit.collider.gameObject == whiteboard.gameObject)
            {
                whiteboard.StrokeFromHit(hit, brushIndex, 0f);
                UpdateHoverDot(hit, visible: showHoverDot && showWhileDrawing);
                return;
            }
        }
        // 沒按或沒命中白板
        if (whiteboard) whiteboard.EndStroke(brushIndex);

        // 只有懸停在白板上才顯示點（沒按著）
        if (!pressed && showHoverDot && ray.TryGetCurrent3DRaycastHit(out RaycastHit hoverHit) &&
            hoverHit.collider && whiteboard && hoverHit.collider.gameObject == whiteboard.gameObject)
        {
            UpdateHoverDot(hoverHit, visible: true);
        }
        else
        {
            SetDotVisible(false);
        }
    }

    // ================== Hover Dot ==================
    void EnsureDot()
    {
        if (!showHoverDot) return;
        if (dot != null) return;

        if (hoverDotPrefab)
        {
            dot = Instantiate(hoverDotPrefab).transform;
            dot.gameObject.SetActive(false);
            dot.localScale = Vector3.one * hoverDotSize;
        }
        else
        {
            // 自動生成一顆小球（無碰撞、Unlit）
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "HoverDot(Auto)";
            DestroyImmediate(go.GetComponent<Collider>());
            var mr = go.GetComponent<MeshRenderer>();
            dotMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            dotMat.SetColor("_BaseColor", hoverDotColor);
            mr.sharedMaterial = dotMat;
            dot = go.transform;
            dot.localScale = Vector3.one * hoverDotSize;
            dot.gameObject.SetActive(false);
        }
    }

    void UpdateHoverDot(RaycastHit hit, bool visible)
    {
        if (!showHoverDot || dot == null) return;

        // 位置：命中點略微抬離表面
        Vector3 targetPos = hit.point + hit.normal * surfaceOffset;
        dot.position = Vector3.Lerp(dot.position, targetPos, followLerp);

        // 旋轉：面向表面（使用白板的 up，避免滾動）
        if (whiteboard)
        {
            Quaternion desired = Quaternion.LookRotation(-hit.normal, whiteboard.transform.up);
            dot.rotation = Quaternion.Slerp(dot.rotation, desired, followLerp);
        }

        // 大小固定：確保不因距離變化
        dot.localScale = Vector3.one * hoverDotSize;

        // 顏色（若用了自動材質）
        if (dotMat) dotMat.SetColor("_BaseColor", hoverDotColor);

        SetDotVisible(visible);
    }

    void SetDotVisible(bool v)
    {
        if (dot && dot.gameObject.activeSelf != v)
            dot.gameObject.SetActive(v);
    }
}
