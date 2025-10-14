using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 滑鼠/指標離開按鈕時，只有在「未被點選成黏住」的情況下才清除選取；
/// 一旦點選，維持 Selected 狀態（常亮）直到選到別的 UI 或被重置/停用。
/// </summary>
public class DeselectOnPointerExit : MonoBehaviour, IPointerExitHandler, IPointerClickHandler, IDeselectHandler
{
    [Tooltip("點選後是否保持選取狀態（常亮），直到選到別的 UI 才解除。")]
    public bool stickyAfterClick = true;

    private bool clickedSticky = false;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (!stickyAfterClick) return;

        clickedSticky = true;
        if (EventSystem.current != null)
        {
            // 明確指定為目前選取對象 → 觸發 Button 的 Selected 顏色
            EventSystem.current.SetSelectedGameObject(gameObject);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (EventSystem.current == null) return;

        // 未啟用黏住：沿用舊行為，滑出就清
        if (!stickyAfterClick)
        {
            if (EventSystem.current.currentSelectedGameObject == gameObject)
                EventSystem.current.SetSelectedGameObject(null);
            return;
        }

        // 啟用黏住：只有未被點選成黏住時，才在滑出時清
        if (!clickedSticky && EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }

    // 被其它 UI 取代選取時，取消黏住（下一次滑出就會清）
    public void OnDeselect(BaseEventData eventData)
    {
        clickedSticky = false;
    }

    private void OnDisable()
    {
        // 物件被關閉時清理，避免殘留為選取對象
        clickedSticky = false;
        if (EventSystem.current != null && EventSystem.current.currentSelectedGameObject == gameObject)
            EventSystem.current.SetSelectedGameObject(null);
    }
}
