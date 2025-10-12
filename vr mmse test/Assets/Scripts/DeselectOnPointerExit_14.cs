using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 【優化版】
/// 這個元件的功能是：當滑鼠指標離開它所在的 UI 物件時，
/// 檢查滑鼠左鍵是否仍處於按下的狀態。
/// 如果是，則判定為「拖曳取消」操作，並清除 EventSystem 中的「目前選定物件」。
/// 如果滑鼠左鍵已放開，則不做任何事，以保留正常點擊後應有的選定狀態。
/// </summary>
public class DeselectOnPointerExit : MonoBehaviour, IPointerExitHandler
{
    public void OnPointerExit(PointerEventData eventData)
    {
        // ▼▼▼ 這是唯一的修改處 ▼▼▼
        // Input.GetMouseButton(0) 會檢查滑鼠左鍵是否「當前正被按著」
        if (Input.GetMouseButton(0))
        {
            // 只有在滑鼠移開的同時，左鍵還按著（代表是拖曳操作），才清除選取
            if (EventSystem.current.currentSelectedGameObject == gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }
    }
}