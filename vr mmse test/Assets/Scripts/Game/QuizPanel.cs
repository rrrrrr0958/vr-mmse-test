using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

/// <summary>
/// 你的既有版本（固定上限按鈕）；已補 Awake/Hide/ShowFeedback，仍相容 Action<int>
/// </summary>
public class QuizPanel : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI titleText;
    public Button[] optionButtons;
    public TextMeshProUGUI[] optionTexts;
    public TextMeshProUGUI feedbackText; // 可選
    public Button closeButton;           // 可選

    Action<int> _onPick;

    void Awake() {
        if (feedbackText) feedbackText.text = "";
        if (closeButton) {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }
        gameObject.SetActive(false); // 預設不顯示
        if (optionButtons != null && optionTexts != null && optionButtons.Length != optionTexts.Length) {
            Debug.LogWarning($"[QuizPanel] optionButtons 與 optionTexts 長度不一致：{optionButtons.Length} vs {optionTexts.Length}");
        }
    }

    public void Show(string title, string[] options, Action<int> onPick)
    {
        if (titleText) titleText.text = title;
        if (feedbackText) feedbackText.text = "";

        _onPick = onPick;

        for (int i = 0; i < optionButtons.Length; i++)
        {
            bool active = (options != null) && i < options.Length;
            optionButtons[i].gameObject.SetActive(active);
            if (i < optionTexts.Length) optionTexts[i].gameObject.SetActive(active);

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

    public void ShowFeedback(bool correct) {
        if (feedbackText) feedbackText.text = correct ? "✅ 正確" : "❌ 錯誤";
    }

    public void Hide() {
        gameObject.SetActive(false);
    }
}
