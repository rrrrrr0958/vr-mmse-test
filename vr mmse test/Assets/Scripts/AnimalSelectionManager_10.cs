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

    private GameManager gm;  // ← 安全的快取

    void Awake()
    {
        // 先嘗試用單例
        gm = GameManager.instance;

        // 若單例還沒設好（載入順序等原因），就從場景抓
        if (gm == null)
        {
#if UNITY_2023_1_OR_NEWER
            gm = FindFirstObjectByType<GameManager>(FindObjectsInactive.Include);
#else
            gm = FindObjectOfType<GameManager>(true);
#endif
        }

        if (gm == null)
        {
            Debug.LogError("[AnimalSelectionManager] 找不到 GameManager，無法綁定按鈕事件。");
        }
    }

    void Start()
    {
        SetupButtonEvents();
    }

    void SetupButtonEvents()
    {
        if (gm == null) return; // 防呆

        if (rabbitButton)  { rabbitButton.onClick.RemoveAllListeners();  rabbitButton.onClick.AddListener(() => gm.OnAnimalButtonClick(rabbitButton,  rabbitButton.name)); }
        if (tigerButton)   { tigerButton.onClick.RemoveAllListeners();   tigerButton.onClick.AddListener(() => gm.OnAnimalButtonClick(tigerButton,   tigerButton.name)); }
        if (buffaloButton) { buffaloButton.onClick.RemoveAllListeners(); buffaloButton.onClick.AddListener(() => gm.OnAnimalButtonClick(buffaloButton,buffaloButton.name)); }
        if (pandaButton)   { pandaButton.onClick.RemoveAllListeners();   pandaButton.onClick.AddListener(() => gm.OnAnimalButtonClick(pandaButton,   pandaButton.name)); }
        if (deerButton)    { deerButton.onClick.RemoveAllListeners();    deerButton.onClick.AddListener(() => gm.OnAnimalButtonClick(deerButton,    deerButton.name)); }
        if (wolfButton)    { wolfButton.onClick.RemoveAllListeners();    wolfButton.onClick.AddListener(() => gm.OnAnimalButtonClick(wolfButton,    wolfButton.name)); }

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
