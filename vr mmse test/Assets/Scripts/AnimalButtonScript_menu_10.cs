using UnityEngine;
using UnityEngine.UI;

public class AnimalButtonScript_menu : MonoBehaviour
{
    [Header("動物名稱設定")]
    public string animalName = "默認動物";

    private Button button;

    void Start()
    {
        button = GetComponent<Button>();
        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
            if (string.IsNullOrEmpty(animalName)) animalName = gameObject.name;
            Debug.Log($"已為 {animalName} 按鈕設定點擊事件 (Menu版本)");
        }
        else
        {
            Debug.LogError($"找不到 Button 組件在 {gameObject.name} 上");
        }
    }

    // 按鈕點擊時執行的方法
    public void OnButtonClick()
    {
        Debug.Log($"點擊了 {animalName} 按鈕！");
        
        // 只呼叫 GameManagerMenu
        if (GameManagerMenu.instance != null)
        {
            // 只傳入動物名稱（符合 GameManagerMenu 的簽名）
            GameManagerMenu.instance.OnAnimalButtonClick(animalName);
            Debug.Log($"已將 {animalName} 記錄到 GameManagerMenu");
        }
        else
        {
            Debug.LogError("找不到 GameManagerMenu 實例");
        }
    }

    public void SetAnimalName(string name) => animalName = name;

    void OnDestroy()
    {
        if (button != null) button.onClick.RemoveListener(OnButtonClick);
    }
}