using UnityEngine;
using UnityEngine.UI;

public class AnimalButtonScript : MonoBehaviour
{
    [Header("動物名稱設定")]
    public string animalName = "默認動物";
    
    private Button button;
    
    void Start()
    {
        // 獲取此GameObject上的Button組件
        button = GetComponent<Button>();
        
        if (button != null)
        {
            // 為按鈕添加點擊事件監聽器
            button.onClick.AddListener(OnButtonClick);
            Debug.Log($"已為 {animalName} 按鈕設定點擊事件");
        }
        else
        {
            Debug.LogError($"找不到Button組件在 {gameObject.name} 上");
        }
    }
    
    // 按鈕點擊時執行的方法
    public void OnButtonClick()
    {
        Debug.Log($"點擊了 {animalName} 按鈕！");
        
        // 檢查GameManager是否存在並呼叫其方法
        if (GameManager.instance != null)
        {
            GameManager.instance.OnAnimalButtonClick(animalName);
            Debug.Log($"已將 {animalName} 記錄到GameManager");
        }
        else
        {
            Debug.LogError("找不到GameManager實例");
        }
    }
    
    // 可以從Unity Inspector中設定動物名稱
    public void SetAnimalName(string name)
    {
        animalName = name;
    }
    
    void OnDestroy()
    {
        // 清理事件監聽器
        if (button != null)
        {
            button.onClick.RemoveListener(OnButtonClick);
        }
    }
}