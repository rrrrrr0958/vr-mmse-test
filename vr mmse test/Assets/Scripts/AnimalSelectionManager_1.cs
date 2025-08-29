using UnityEngine;
using UnityEngine.UI;

public class AnimalSelectionManager : MonoBehaviour
{
    [Header("動物按鈕設定")]
    public Button rabbitButton;      // 兔子按鈕
    public Button tigerButton;       // 老虎按鈕
    public Button buffaloButton;     // 水牛按鈕
    public Button pandaButton;       // 熊貓按鈕
    public Button deerButton;        // 鹿按鈕
    public Button wolfButton;        // 狼按鈕

    void Start()
    {
        // 為每個按鈕設置點擊事件
        SetupButtonEvents();
    }

    void SetupButtonEvents()
    {
        // 兔子按鈕
        if (rabbitButton != null)
        {
            rabbitButton.onClick.AddListener(() => OnAnimalSelected("兔子"));
        }

        // 老虎按鈕
        if (tigerButton != null)
        {
            tigerButton.onClick.AddListener(() => OnAnimalSelected("老虎"));
        }

        // 水牛按鈕
        if (buffaloButton != null)
        {
            buffaloButton.onClick.AddListener(() => OnAnimalSelected("水牛"));
        }

        // 熊貓按鈕
        if (pandaButton != null)
        {
            pandaButton.onClick.AddListener(() => OnAnimalSelected("熊貓"));
        }

        // 鹿按鈕
        if (deerButton != null)
        {
            deerButton.onClick.AddListener(() => OnAnimalSelected("鹿"));
        }

        // 狼按鈕
        if (wolfButton != null)
        {
            wolfButton.onClick.AddListener(() => OnAnimalSelected("狼"));
        }

        Debug.Log("所有動物按鈕事件設置完成！");
    }

    // 動物選擇處理方法
    public void OnAnimalSelected(string animalName)
    {
        Debug.Log($"選擇了動物: {animalName}");
        
        // 記錄到 GameManager
        if (GameManager.instance != null)
        {
            GameManager.instance.OnAnimalButtonClick(animalName);
            Debug.Log($"已將 {animalName} 記錄到 GameManager");
        }
        else
        {
            Debug.LogError("找不到 GameManager 實例！");
        }

        // 觸發選擇特效
        OnAnimalSelectedEffect(animalName);
    }

    // 動物選擇後的視覺效果
    private void OnAnimalSelectedEffect(string animalName)
    {
        Debug.Log($"觸發 {animalName} 的選擇特效");
        
        // 這裡可以添加特效，例如：
        // - 按鈕縮放動畫
        // - 粒子效果
        // - 聲音回饋
    }

    void OnDestroy()
    {
        // 清理事件監聽器
        if (rabbitButton != null) rabbitButton.onClick.RemoveAllListeners();
        if (tigerButton != null) tigerButton.onClick.RemoveAllListeners();
        if (buffaloButton != null) buffaloButton.onClick.RemoveAllListeners();
        if (pandaButton != null) pandaButton.onClick.RemoveAllListeners();
        if (deerButton != null) deerButton.onClick.RemoveAllListeners();
        if (wolfButton != null) wolfButton.onClick.RemoveAllListeners();
    }
}