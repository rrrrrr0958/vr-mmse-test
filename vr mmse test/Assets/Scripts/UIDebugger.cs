using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class UIDebugger : MonoBehaviour
{
    void Start()
    {
        Debug.Log("=== UI 系統診斷開始 ===");
        
        // 檢查 EventSystem
        CheckEventSystem();
        
        // 檢查 Canvas
        CheckCanvas();
        
        // 檢查按鈕設置
        CheckButtons();
        
        // 檢查圖層設置
        CheckLayers();
    }
    
    void CheckEventSystem()
    {
        EventSystem eventSystem = FindObjectOfType<EventSystem>();
        if (eventSystem != null)
        {
            Debug.Log("✅ 找到 EventSystem: " + eventSystem.name);
            Debug.Log("EventSystem 啟用狀態: " + eventSystem.enabled);
        }
        else
        {
            Debug.LogError("❌ 找不到 EventSystem! 這是按鈕無法點擊的主要原因!");
        }
        
        GraphicRaycaster[] raycasters = FindObjectsOfType<GraphicRaycaster>();
        Debug.Log($"找到 {raycasters.Length} 個 GraphicRaycaster");
    }
    
    void CheckCanvas()
    {
        Canvas[] canvases = FindObjectsOfType<Canvas>();
        Debug.Log($"找到 {canvases.Length} 個 Canvas");
        
        foreach (Canvas canvas in canvases)
        {
            Debug.Log($"Canvas: {canvas.name}");
            Debug.Log($"  - 渲染模式: {canvas.renderMode}");
            Debug.Log($"  - 啟用狀態: {canvas.enabled}");
            Debug.Log($"  - 排序順序: {canvas.sortingOrder}");
            
            GraphicRaycaster raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster != null)
            {
                Debug.Log($"  - GraphicRaycaster: {raycaster.enabled}");
            }
            else
            {
                Debug.LogWarning($"  - ❌ Canvas {canvas.name} 沒有 GraphicRaycaster!");
            }
        }
    }
    
    void CheckButtons()
    {
        Button[] buttons = FindObjectsOfType<Button>();
        Debug.Log($"找到 {buttons.Length} 個按鈕");
        
        foreach (Button button in buttons)
        {
            Debug.Log($"按鈕: {button.name}");
            Debug.Log($"  - 啟用狀態: {button.enabled}");
            Debug.Log($"  - 可互動: {button.interactable}");
            Debug.Log($"  - GameObject 啟用: {button.gameObject.activeInHierarchy}");
            Debug.Log($"  - OnClick 事件數量: {button.onClick.GetPersistentEventCount()}");
            
            // 檢查是否有阻擋的物件
            RectTransform rectTransform = button.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Debug.Log($"  - RectTransform: {rectTransform.rect}");
            }
        }
    }
    
    void CheckLayers()
    {
        Debug.Log("=== 圖層檢查 ===");
        
        Button[] buttons = FindObjectsOfType<Button>();
        foreach (Button button in buttons)
        {
            Debug.Log($"按鈕 {button.name} 在圖層: {LayerMask.LayerToName(button.gameObject.layer)}");
        }
    }
    
    void Update()
    {
        // 檢測滑鼠點擊
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("偵測到滑鼠點擊!");
            
            // 檢查滑鼠位置
            Vector2 mousePos = Input.mousePosition;
            Debug.Log($"滑鼠位置: {mousePos}");
            
            // 使用 Raycast 檢查點擊到什麼
            PointerEventData eventData = new PointerEventData(EventSystem.current);
            eventData.position = mousePos;
            
            var results = new System.Collections.Generic.List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);
            
            Debug.Log($"Raycast 結果數量: {results.Count}");
            foreach (var result in results)
            {
                Debug.Log($"  - 點擊到: {result.gameObject.name}");
            }
        }
    }
}