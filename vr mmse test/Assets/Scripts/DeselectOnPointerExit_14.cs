using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 滑鼠/指標離開按鈕時，取消目前的選取高亮（避免 Button 一直維持選取狀態）
/// </summary>
public class DeselectOnPointerExit : MonoBehaviour, IPointerExitHandler
{
    public void OnPointerExit(PointerEventData eventData)
    {
        // 沒有 EventSystem 就不處理
        if (EventSystem.current == null) return;

        // 只有在這個物件本來就是被選取時才清掉
        if (EventSystem.current.currentSelectedGameObject == gameObject)
        {
            EventSystem.current.SetSelectedGameObject(null);
        }
    }
}
