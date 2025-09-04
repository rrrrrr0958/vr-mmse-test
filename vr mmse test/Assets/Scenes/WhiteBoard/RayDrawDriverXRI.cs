using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor))]
public class RayDrawDriverXRI : MonoBehaviour
{
    public WhiteBoard whiteboard;
    public int brushIndex = 0;                 // 用哪支筆（對應 WhiteBoard.brushes）
    [Tooltip("扳機/Activate 動作（XRI Default Input Actions → Activate）")]
    public InputActionProperty drawAction;

    UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor ray;

    void Awake() { ray = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>(); }
    void OnEnable()  { if (drawAction.action != null) drawAction.action.Enable(); }
    void OnDisable() { if (drawAction.action != null) drawAction.action.Disable(); }

    void Update()
    {
        if (!whiteboard) return;

        bool pressed = drawAction.action != null && drawAction.action.IsPressed();

        if (pressed && ray.TryGetCurrent3DRaycastHit(out RaycastHit hit))
        {
            // 只有命中這塊白板才畫
            if (hit.collider && hit.collider.gameObject == whiteboard.gameObject)
            {
                // rotationZDeg=0：射線畫筆不需要旋轉，也可依需求帶控制器 Z 角
                whiteboard.StrokeFromHit(hit, brushIndex, 0f);
                return;
            }
        }

        // 沒按或沒命中 → 結束筆劃
        whiteboard.EndStroke(brushIndex);
    }
}
