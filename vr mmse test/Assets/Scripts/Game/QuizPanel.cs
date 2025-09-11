using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

/// <summary>
/// 世界空間三選（或多選）題面板。VR 友善：僅由 XR UI Input Module 處理點擊。
/// 與 SessionController 配合：提供 MaxOptions，並在 Show/Hide 時安全綁/解事件。
/// </summary>
public class QuizPanel : MonoBehaviour
{
    [Header("UI")]
    public TextMeshProUGUI titleText;
    [Tooltip("每個選項對應一顆按鈕")]
    public Button[] optionButtons;
    [Tooltip("與 optionButtons 對齊的文字標籤")]
    public TextMeshProUGUI[] optionTexts;
    [Tooltip("可選：顯示正確/錯誤的回饋")]
    public TextMeshProUGUI feedbackText; // 可選
    [Tooltip("可選：關閉面板用")]
    public Button closeButton;           // 可選

    /// <summary>面板可提供的最大選項數（由按鈕數量決定）。</summary>
    public int MaxOptions => optionButtons != null ? optionButtons.Length : 0;

    Action<int> _onPick;

    void Awake()
    {
        // 基本檢查
        if (optionButtons == null || optionButtons.Length == 0)
            Debug.LogWarning("[QuizPanel] 請指派 optionButtons（至少 1 顆）。");

        if (optionTexts != null && optionButtons != null && optionButtons.Length != optionTexts.Length)
            Debug.LogWarning($"[QuizPanel] optionButtons 與 optionTexts 長度不一致：{optionButtons.Length} vs {optionTexts?.Length ?? 0}");

        if (feedbackText) feedbackText.text = "";

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        gameObject.SetActive(false); // 預設不顯示
    }

    /// <summary>顯示題目與選項。options 長度會被裁切到 MaxOptions。</summary>
    public void Show(string title, string[] options, Action<int> onPick)
    {
        _onPick = onPick;

        if (titleText) titleText.text = title ?? "";
        if (feedbackText) feedbackText.text = "";

        int cap = Mathf.Min(MaxOptions, options != null ? options.Length : 0);

        // 綁定按鈕
        for (int i = 0; i < MaxOptions; i++)
        {
            bool active = i < cap;

            // Button 可見/可點
            if (optionButtons != null && i < optionButtons.Length && optionButtons[i] != null)
            {
                optionButtons[i].gameObject.SetActive(active);
                optionButtons[i].interactable = active;

                // 清理舊事件再綁新事件
                optionButtons[i].onClick.RemoveAllListeners();
                if (active)
                {
                    int idx = i;
                    optionButtons[i].onClick.AddListener(() => _onPick?.Invoke(idx));
                }
            }

            // 對應文字
            if (optionTexts != null && i < optionTexts.Length && optionTexts[i] != null)
            {
                optionTexts[i].gameObject.SetActive(active);
                if (active) optionTexts[i].text = options[i];
            }
        }

        gameObject.SetActive(true);
    }

    /// <summary>顯示正確/錯誤回饋（可選）。</summary>
    public void ShowFeedback(bool correct)
    {
        if (feedbackText) feedbackText.text = correct ? "✅ 正確" : "❌ 錯誤";
    }

    /// <summary>隱藏面板並解除所有按鈕事件。</summary>
    public void Hide()
    {
        // 解除事件 & 禁用互動，避免在淡出/轉場期間誤觸
        if (optionButtons != null)
        {
            foreach (var btn in optionButtons)
            {
                if (!btn) continue;
                btn.onClick.RemoveAllListeners();
                btn.interactable = false;
            }
        }

        if (closeButton) closeButton.onClick.RemoveListener(Hide);

        gameObject.SetActive(false);
        _onPick = null;

        // 重新掛回 close 事件（若有）
        if (closeButton) closeButton.onClick.AddListener(Hide);
    }
}
