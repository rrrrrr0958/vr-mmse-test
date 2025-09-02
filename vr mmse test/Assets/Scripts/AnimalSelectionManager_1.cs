using UnityEngine;
using UnityEngine.UI;

public class AnimalSelectionManager : MonoBehaviour
{
    [Header("動物按鈕設定")]
    public Button rabbitButton;      // 兔子
    public Button tigerButton;       // 老虎
    public Button buffaloButton;     // 水牛
    public Button pandaButton;       // 熊貓
    public Button deerButton;        // 鹿
    public Button wolfButton;        // 狼

    void Start()
    {
        SetupButtonEvents();
    }

    void SetupButtonEvents()
    {
        if (rabbitButton)  rabbitButton.onClick.AddListener(() => GameManager.instance.OnAnimalButtonClick(rabbitButton,  rabbitButton.name));
        if (tigerButton)   tigerButton.onClick.AddListener(() => GameManager.instance.OnAnimalButtonClick(tigerButton,   tigerButton.name));
        if (buffaloButton) buffaloButton.onClick.AddListener(() => GameManager.instance.OnAnimalButtonClick(buffaloButton,buffaloButton.name));
        if (pandaButton)   pandaButton.onClick.AddListener(() => GameManager.instance.OnAnimalButtonClick(pandaButton,   pandaButton.name));
        if (deerButton)    deerButton.onClick.AddListener(() => GameManager.instance.OnAnimalButtonClick(deerButton,    deerButton.name));
        if (wolfButton)    wolfButton.onClick.AddListener(() => GameManager.instance.OnAnimalButtonClick(wolfButton,    wolfButton.name));

        Debug.Log("所有動物按鈕事件設置完成！");
    }

    void OnDestroy()
    {
        if (rabbitButton)  rabbitButton.onClick.RemoveAllListeners();
        if (tigerButton)   tigerButton.onClick.RemoveAllListeners();
        if (buffaloButton) buffaloButton.onClick.RemoveAllListeners();
        if (pandaButton)   pandaButton.onClick.RemoveAllListeners();
        if (deerButton)    deerButton.onClick.RemoveAllListeners();
        if (wolfButton)    wolfButton.onClick.RemoveAllListeners();
    }
}
