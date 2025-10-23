using UnityEngine;
using UnityEngine.UI;

public class HideObjectOnClick : MonoBehaviour
{
    public Button targetButton;    // 指向你要監聽的按鈕
    public GameObject Chest; // 要被隱藏的物件
    public GameObject Button; // 要被隱藏的物件
    public GameObject Text; // 要被隱藏的物件
    public GameObject ScorePanel; // 要被隱藏的物件


    void Start()
    {
        // 綁定按鈕事件
        targetButton.onClick.AddListener(HideObject);

    }

    void HideObject()
    {
        Chest.SetActive(false);
        Button.SetActive(false);
        Text.SetActive(false);
        ScorePanel.SetActive(true);

    }

}
