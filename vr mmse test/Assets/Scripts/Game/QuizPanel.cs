using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

public class QuizPanel : MonoBehaviour
{
    public TextMeshProUGUI titleText;
    public Button[] optionButtons;
    public TextMeshProUGUI[] optionTexts;

    Action<int> _onPick;

    public void Show(string title, string[] options, Action<int> onPick)
    {
        titleText.text = title;
        _onPick = onPick;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            bool active = i < options.Length;
            optionButtons[i].gameObject.SetActive(active);
            optionTexts[i].transform.parent.gameObject.SetActive(active);
            if (active)
            {
                optionTexts[i].text = options[i];
                int idx = i;
                optionButtons[i].onClick.RemoveAllListeners();
                optionButtons[i].onClick.AddListener(() => _onPick?.Invoke(idx));
            }
        }
        gameObject.SetActive(true);
    }
}
