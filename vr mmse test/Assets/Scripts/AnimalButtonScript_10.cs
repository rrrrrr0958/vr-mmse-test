using UnityEngine;
using UnityEngine.UI;

public class AnimalButtonScript_10 : MonoBehaviour
{
    [Header("動物名稱設定")]
    public string animalName = "";

    private Button button;
    private GameManagerMenu_10 gmMenu;
    private GameManager_10 gmPlay;

    void Start()
    {
        button = GetComponent<Button>();

        if (button != null)
        {
            button.onClick.AddListener(OnButtonClick);
            Debug.Log($"[AnimalButtonScript_10] 已為 {animalName} 綁定點擊事件");
        }
        else
        {
            Debug.LogError($"[AnimalButtonScript_10] 在 {gameObject.name} 上找不到 Button 組件！");
        }

        // 檢查名稱
        if (string.IsNullOrEmpty(animalName))
        {
            Debug.LogWarning($"[AnimalButtonScript_10] {gameObject.name} 的動物名稱未設定！");
        }

        // 嘗試找 GameManagerMenu_10 或 GameManager_10
        gmMenu = GameManagerMenu_10.instance ?? FindObjectOfType<GameManagerMenu_10>(true);
        gmPlay = GameManager_10.instance ?? FindObjectOfType<GameManager_10>(true);

        if (gmMenu != null)
            Debug.Log("[AnimalButtonScript_10] 綁定到 GameManagerMenu_10");
        else if (gmPlay != null)
            Debug.Log("[AnimalButtonScript_10] 綁定到 GameManager_10");
        else
            Debug.LogError("[AnimalButtonScript_10] 找不到任何 GameManager。");
    }

    public void OnButtonClick()
    {
        Debug.Log($"[AnimalButtonScript_10] {animalName} 被點擊");

        if (gmMenu != null)
        {
            gmMenu.OnAnimalButtonClick(button, animalName);
            Debug.Log($"[AnimalButtonScript_10] 已通知 GameManagerMenu_10 選擇 {animalName}");
        }
        else if (gmPlay != null)
        {
            gmPlay.OnAnimalButtonClick(button, animalName);
            Debug.Log($"[AnimalButtonScript_10] 已通知 GameManager_10 選擇 {animalName}");
        }
        else
        {
            Debug.LogError("[AnimalButtonScript_10] ❌ 找不到可呼叫的 GameManagerMenu_10 或 GameManager_10。");
        }
    }

    public void TriggerClick()
    {
        OnButtonClick();
    }

    public string GetAnimalName()
    {
        return animalName;
    }

    void OnValidate()
    {
        if (string.IsNullOrEmpty(animalName))
        {
            Debug.LogWarning($"請為 {gameObject.name} 設定動物名稱");
        }
    }
}
