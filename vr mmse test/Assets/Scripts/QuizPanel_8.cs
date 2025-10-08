using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System;

/// <summary>
/// 世界空間三選/多選題面板（HUD）。建議掛在 XR Origin 子物件，並用 PlaceHudInFront 放到相機前方。
/// 支援 CanvasGroup（建議）或退回 SetActive。
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

    [Header("Display")]
    [Tooltip("若存在則用 CanvasGroup 控制顯示/互動；否則使用 SetActive 切換。")]
    public CanvasGroup canvasGroup;

    public int MaxOptions => optionButtons != null ? optionButtons.Length : 0;

    Action<int> _onPick;

    void Awake()
    {
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (feedbackText) feedbackText.text = "";

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide);
        }

        // 起始隱藏
        SetVisible(false);
    }

    public void Show(string title, string[] options, Action<int> onPick)
    {
        _onPick = onPick;

        if (titleText) titleText.text = title ?? "";
        if (feedbackText) feedbackText.text = "";

        int cap = Mathf.Min(MaxOptions, options != null ? options.Length : 0);

        // 綁定按鈕 / 文字
        for (int i = 0; i < MaxOptions; i++)
        {
            bool active = i < cap;

            if (optionButtons != null && i < optionButtons.Length && optionButtons[i] != null)
            {
                optionButtons[i].gameObject.SetActive(active);
                optionButtons[i].interactable = active;

                optionButtons[i].onClick.RemoveAllListeners();
                if (active)
                {
                    int idx = i;
                    optionButtons[i].onClick.AddListener(() => _onPick?.Invoke(idx));
                }
            }

            if (optionTexts != null && i < optionTexts.Length && optionTexts[i] != null)
            {
                optionTexts[i].gameObject.SetActive(active);
                if (active) optionTexts[i].text = options[i];
            }
        }

        SetVisible(true);
    }

    public void ShowFeedback(bool correct)
    {
        if (feedbackText) feedbackText.text = correct ? "✅ 正確" : "❌ 錯誤";
    }

    public void Hide()
    {
        if (optionButtons != null)
        {
            foreach (var btn in optionButtons)
            {
                if (!btn) continue;
                btn.onClick.RemoveAllListeners();
                btn.interactable = false;
            }
        }

        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.onClick.AddListener(Hide); // 保持下次可用
        }

        _onPick = null;
        SetVisible(false);
    }

    public void PlaceHudInFront(Transform cam, float distance = 1.4f, float yOffset = 0f)
    {
        if (!cam) return;
        var fwd = cam.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 1e-6f) fwd = cam.forward;
        fwd.Normalize();

        transform.position = cam.position + fwd * distance + Vector3.up * yOffset;
        transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
    }

    // ===== 顯示控制（優先用 CanvasGroup） =====
    void SetVisible(bool visible)
    {
        if (canvasGroup)
        {
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = visible;
            canvasGroup.interactable = visible;
        }
        else
        {
            gameObject.SetActive(visible);
        }
    }
}
    