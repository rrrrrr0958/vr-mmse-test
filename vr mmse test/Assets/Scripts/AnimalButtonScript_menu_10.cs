using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class AnimalButtonScript_menu : MonoBehaviour
{
    [Header("動物名稱設定")]
    public string animalName = "默認動物";

    private Button button;

    void Awake()
    {
        button = GetComponent<Button>();
        if (string.IsNullOrEmpty(animalName))
            animalName = gameObject.name; // 預設用物件名當動物名
    }

    void OnEnable()
    {
        if (button != null)
            button.onClick.AddListener(OnButtonClick);
        else
            Debug.LogError($"[AnimalButtonScript_menu] {name} 找不到 Button 元件");
    }

    void OnDisable()
    {
        if (button != null)
            button.onClick.RemoveListener(OnButtonClick);
    }

    // 按鈕點擊時
    private void OnButtonClick()
    {
        if (GameManagerMenu.instance == null)
        {
            Debug.LogError("[AnimalButtonScript_menu] 找不到 GameManagerMenu.instance，確認場景中有掛 GameManagerMenu");
            return;
        }

        // 依照你現在的 GameManagerMenu 實作，傳入 Button + 名稱
        GameManagerMenu.instance.OnAnimalButtonClick(button, animalName);
        Debug.Log($"[AnimalButtonScript_menu] 已將 {animalName} 傳給 GameManagerMenu（含 Button）");
    }

    // 供外部動態改名
    public void SetAnimalName(string name) => animalName = name;
}
