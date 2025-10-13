using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class AnimalSelectionManager_10 : MonoBehaviour
{
    [Header("動物按鈕設定")]
    public Button rabbitButton;
    public Button tigerButton;
    public Button buffaloButton;
    public Button pandaButton;
    public Button deerButton;
    public Button wolfButton;

    private GameManager_10 gmPlay;

    void Start()
    {
        StartCoroutine(WaitForGameManagerThenSetup());
    }

    IEnumerator WaitForGameManagerThenSetup()
    {
        float timeout = 2f;
        while (gmPlay == null && timeout > 0)
        {
            gmPlay = GameManager_10.instance ?? FindObjectOfType<GameManager_10>(true);
            timeout -= Time.deltaTime;
            yield return null;
        }

        if (gmPlay == null)
        {
            Debug.LogError("[AnimalSelectionManager_10] ❌ 找不到 GameManager_10。");
            yield break;
        }

        SetupButtonEvents();
    }

    void SetupButtonEvents()
    {
        Debug.Log("[AnimalSelectionManager_10] ✅ 綁定到 GameManager_10");

        if (rabbitButton)
        {
            rabbitButton.onClick.RemoveAllListeners();
            rabbitButton.onClick.AddListener(() => OnAnimalClick(rabbitButton));
        }
        if (tigerButton)
        {
            tigerButton.onClick.RemoveAllListeners();
            tigerButton.onClick.AddListener(() => OnAnimalClick(tigerButton));
        }
        if (buffaloButton)
        {
            buffaloButton.onClick.RemoveAllListeners();
            buffaloButton.onClick.AddListener(() => OnAnimalClick(buffaloButton));
        }
        if (pandaButton)
        {
            pandaButton.onClick.RemoveAllListeners();
            pandaButton.onClick.AddListener(() => OnAnimalClick(pandaButton));
        }
        if (deerButton)
        {
            deerButton.onClick.RemoveAllListeners();
            deerButton.onClick.AddListener(() => OnAnimalClick(deerButton));
        }
        if (wolfButton)
        {
            wolfButton.onClick.RemoveAllListeners();
            wolfButton.onClick.AddListener(() => OnAnimalClick(wolfButton));
        }
    }

    void OnAnimalClick(Button btn)
    {
        string name = btn ? btn.name : "unknown";
        if (gmPlay != null)
            gmPlay.OnAnimalButtonClick(btn, name);
        else
            Debug.LogError("[AnimalSelectionManager_10] ❌ 無法呼叫 OnAnimalButtonClick，找不到 GameManager_10。");
    }

    void OnDestroy()
    {
        if (rabbitButton) rabbitButton.onClick.RemoveAllListeners();
        if (tigerButton) tigerButton.onClick.RemoveAllListeners();
        if (buffaloButton) buffaloButton.onClick.RemoveAllListeners();
        if (pandaButton) pandaButton.onClick.RemoveAllListeners();
        if (deerButton) deerButton.onClick.RemoveAllListeners();
        if (wolfButton) wolfButton.onClick.RemoveAllListeners();
    }
}
