using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class AnimalButtonScript_menu_10 : MonoBehaviour
{
    [Header("動物名稱設定")]
    public string animalName = "默認動物";

    private Button button;
    private GameManagerMenu_10 gmMenu;
    private GameManager_10 gmPlay;

    void Awake()
    {
        button = GetComponent<Button>();
        if (string.IsNullOrEmpty(animalName))
            animalName = gameObject.name;
    }

    void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(OnButtonClick);
        else
            Debug.LogError($"[AnimalButtonScript_menu_10] {name} 找不到 Button 元件");

        // 嘗試找到 GameManager（兩種版本）
        gmMenu = GameManagerMenu_10.instance ?? FindObjectOfType<GameManagerMenu_10>(true);
        gmPlay = GameManager_10.instance ?? FindObjectOfType<GameManager_10>(true);

        if (gmMenu != null)
            Debug.Log($"[AnimalButtonScript_menu_10] 綁定到 GameManagerMenu_10");
        else if (gmPlay != null)
            Debug.Log($"[AnimalButtonScript_menu_10] 綁定到 GameManager_10");
        else
            Debug.LogWarning($"[AnimalButtonScript_menu_10] 尚未找到任何 GameManager（可能還在載入中）");
    }

    void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OnButtonClick);
    }

    private void OnButtonClick()
    {
        // 嘗試確認 Manager 是否存在
        if (gmMenu == null && gmPlay == null)
        {
            gmMenu = GameManagerMenu_10.instance ?? FindObjectOfType<GameManagerMenu_10>(true);
            gmPlay = GameManager_10.instance ?? FindObjectOfType<GameManager_10>(true);
        }

        if (gmMenu != null)
        {
            gmMenu.OnAnimalButtonClick(button, animalName);
            Debug.Log($"[AnimalButtonScript_menu_10] 傳給 GameManagerMenu_10：{animalName}");
        }
        else if (gmPlay != null)
        {
            gmPlay.OnAnimalButtonClick(button, animalName);
            Debug.Log($"[AnimalButtonScript_menu_10] 傳給 GameManager_10：{animalName}");
        }
        else
        {
            Debug.LogError("[AnimalButtonScript_menu_10] ❌ 找不到任何 GameManager_10 或 GameManagerMenu_10，請確認場景是否掛載正確");
        }
    }

    public void SetAnimalName(string name)
    {
        animalName = name;
    }
}
