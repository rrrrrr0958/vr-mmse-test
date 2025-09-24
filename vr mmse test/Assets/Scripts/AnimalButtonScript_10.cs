using UnityEngine;
using UnityEngine.UI;

public class AnimalButtonScript : MonoBehaviour
{
    [Header("動物名稱設定")]
    public string animalName = "";
    
    private Button button;
    
    void Start()
    {
        // 獲取按鈕組件
        button = GetComponent<Button>();
        
        if (button != null)
        {
            // 綁定點擊事件
            button.onClick.AddListener(OnButtonClick);
            Debug.Log($"AnimalButtonScript: 已為 {animalName} 綁定點擊事件");
        }
        else
        {
            Debug.LogError($"AnimalButtonScript: 在 {gameObject.name} 上找不到 Button 組件！");
        }
        
        // 檢查動物名稱是否已設定
        if (string.IsNullOrEmpty(animalName))
        {
            Debug.LogWarning($"AnimalButtonScript: {gameObject.name} 的動物名稱未設定！");
        }
    }
    
    public void OnButtonClick()
    {
        Debug.Log($"AnimalButtonScript: {animalName} 按鈕被點擊");
        
        // 檢查 GameManagerMenu 是否存在
        if (GameManagerMenu.instance != null)
        {
            // 調用 GameManagerMenu 的點擊方法，傳入按鈕參考
            GameManagerMenu.instance.OnAnimalButtonClick(button, animalName);
            Debug.Log($"AnimalButtonScript: 已通知 GameManagerMenu 選擇了 {animalName}");
        }
        else
        {
            Debug.LogError("AnimalButtonScript: 找不到 GameManagerMenu.instance！");
        }
    }
    
    // 額外的公共方法，供外部腳本調用
    public void TriggerClick()
    {
        OnButtonClick();
    }
    
    // 獲取動物名稱的公共方法
    public string GetAnimalName()
    {
        return animalName;
    }
    
    // 可選：在 Inspector 中設定動物名稱時的驗證
    void OnValidate()
    {
        if (string.IsNullOrEmpty(animalName))
        {
            Debug.LogWarning($"請為 {gameObject.name} 設定動物名稱");
        }
    }
}