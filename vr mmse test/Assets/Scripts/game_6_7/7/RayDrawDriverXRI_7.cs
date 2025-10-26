using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using UnityEngine.XR.Interaction.Toolkit;

[DefaultExecutionOrder(10000)] // 等 XR 系統更新完再跑，避免一幀延遲
[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor))]
public class RayDrawDriverXRI_7 : MonoBehaviour
{
    [Header("Whiteboard / Brush")]
    public WhiteBoard_7 whiteboard;
    public int brushIndex = 0;

    [Header("Input")]
    public InputActionProperty drawAction;

    [Header("Hover Dot")]
    public bool showHoverDot = true;
    public GameObject hoverDotPrefab;
    public float hoverDotSize = 0.02f;
    public Color hoverDotColor = Color.red;
    public float surfaceOffset = 0.001f;
    public bool showWhileDrawing = true;

    [Tooltip("0~1：位置/旋轉插值（未按住時使用）。按住繪圖時會強制=1。")]
    [Range(0f,1f)] public float followLerp = 0.5f;

    [Header("進階")]
    [Tooltip("命中 UI 時是否仍保持全長（不在命中點截斷）")]
    public bool keepFullLengthOnUI = true;

    UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor ray;
    Transform dot;
    Material dotMat;

    void Awake() {
        ray = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
        EnsureDot();
    }

    void OnEnable()  { if (drawAction.action != null) drawAction.action.Enable(); }
    void OnDisable() { if (drawAction.action != null) drawAction.action.Disable(); }

    void LateUpdate()
    {
        bool pressed = drawAction.action != null && drawAction.action.IsPressed();

        // --- 按住繪圖：命中白板就直接貼齊（無 Lerp） ---
        if (pressed && ray.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            if (hit.collider && whiteboard && hit.collider.gameObject == whiteboard.gameObject)
            {
                whiteboard.StrokeFromHit(hit, brushIndex, 0f);
                UpdateHoverDotImmediate(hit, visible: showHoverDot && showWhileDrawing);
                return;
            }
        }

        // 結束筆畫
        if (whiteboard) whiteboard.EndStroke(brushIndex);

        // --- 懸停顯示 ---
        if (!pressed && showHoverDot)
        {
            // 在白板上懸停 → 顯示點（有一點 Lerp 比較順）
            if (ray.TryGetCurrent3DRaycastHit(out RaycastHit hoverHit) &&
                hoverHit.collider && whiteboard && hoverHit.collider.gameObject == whiteboard.gameObject)
            {
                UpdateHoverDotLerped(hoverHit, visible: true, followLerp);
            }
            else
            {
                // 不在白板：使用與射線一致的末端（自己計算）
                Vector3 origin = ray.transform.position;
                Vector3 dir    = ray.transform.forward;
                Vector3 end;

                if (ray.TryGetCurrentUIRaycastResult(out RaycastResult uiHit))
                {
                    end = keepFullLengthOnUI
                        ? origin + dir * ray.maxRaycastDistance         // 保持全長
                        : uiHit.worldPosition;                           // 截斷到 UI 命中點
                }
                else
                {
                    // 沒打到任何東西 → 固定全長
                    end = origin + dir * ray.maxRaycastDistance;
                }

                Pose p = new Pose(end, Quaternion.LookRotation(-dir));
                UpdateHoverDotPoseLerped(p, true, followLerp);
            }
        }
        else
        {
            SetDotVisible(false);
        }
    }

    // ================== Hover Dot ==================
    void EnsureDot()
    {
        if (!showHoverDot || dot != null) return;

        if (hoverDotPrefab)
        {
            dot = Instantiate(hoverDotPrefab).transform;
            dot.localScale = Vector3.one * hoverDotSize;
            dot.gameObject.SetActive(false);
        }
        else
        {
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

    void UpdateHoverDotImmediate(RaycastHit hit, bool visible)
    {
        if (!showHoverDot || dot == null) return;
        dot.position = hit.point + hit.normal * surfaceOffset;
        if (whiteboard)
            dot.rotation = Quaternion.LookRotation(-hit.normal, whiteboard.transform.up);
        dot.localScale = Vector3.one * hoverDotSize;
        if (dotMat) dotMat.SetColor("_BaseColor", hoverDotColor);
        SetDotVisible(visible);
    }

    void UpdateHoverDotLerped(RaycastHit hit, bool visible, float t)
    {
        if (!showHoverDot || dot == null) return;

        Vector3 targetPos = hit.point + hit.normal * surfaceOffset;
        dot.position = Vector3.Lerp(dot.position, targetPos, Mathf.Clamp01(t));

        if (whiteboard)
        {
            Quaternion desired = Quaternion.LookRotation(-hit.normal, whiteboard.transform.up);
            dot.rotation = Quaternion.Slerp(dot.rotation, desired, Mathf.Clamp01(t));
        }

        dot.localScale = Vector3.one * hoverDotSize;
        if (dotMat) dotMat.SetColor("_BaseColor", hoverDotColor);
        SetDotVisible(visible);
    }

    void UpdateHoverDotPoseLerped(Pose pose, bool visible, float t)
    {
        if (!showHoverDot || dot == null) return;
        dot.position = Vector3.Lerp(dot.position, pose.position, Mathf.Clamp01(t));
        dot.rotation = Quaternion.Slerp(dot.rotation, pose.rotation, Mathf.Clamp01(t));
        dot.localScale = Vector3.one * hoverDotSize;
        if (dotMat) dotMat.SetColor("_BaseColor", hoverDotColor);
        SetDotVisible(visible);
    }

    void SetDotVisible(bool v)
    {
        if (dot && dot.gameObject.activeSelf != v)
            dot.gameObject.SetActive(v);
    }
}
