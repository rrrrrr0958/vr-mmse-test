using UnityEngine;
using UnityEngine.UI;

public class ButtonClickTestSimple : MonoBehaviour
{
    void Start()
    {
        Debug.Log("=== 簡單按鈕點擊測試 ===");
        Debug.Log("請嘗試點擊任何動物按鈕！");
        Debug.Log("如果看到'選擇了動物'的訊息，表示按鈕已經可以正常點擊！");
    }
    
    void Update()
    {
        // 檢測任何滑鼠點擊
        if (Input.GetMouseButtonDown(0))
        {
            Debug.Log("偵測到滑鼠左鍵點擊!");
        }
    }
}