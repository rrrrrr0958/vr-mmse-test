using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("UI References")]
    public GameObject panel1;                 // Panel_1
    public GameObject confirmPanel;           // Panel2_1
    public TextMeshProUGUI resultText;        // ResultText_1（TMP）

    [Header("Confirm UI Buttons")]
    public Button confirmButton;              // Panel2_1/確認
    public Button retryButton;                // Panel2_1/重選

    [Header("Animal Buttons (可留空)")]
    public List<Button> animalButtons = new List<Button>(); // 只用來重置顏色

    [Header("正確答案(順序)")]
    public List<string> correctAnswerSequence = new List<string> { "兔子", "熊貓", "鹿" };

    // 內部狀態
    private readonly List<string> clickedOrder = new List<string>();   // 保留點擊順序（不重覆）
    private readonly HashSet<string> selectedSet = new HashSet<string>(); // 判斷是否已選
    private readonly Dictionary<Button, Color> originalColors = new Dictionary<Button, Color>();

    private float startTime;
    private float endTime;

    public IReadOnlyList<string> ClickedAnimalSequence => clickedOrder;

    void Awake()
    {
        if (instance == null) instance = this;
    }

    void Start()
    {
        startTime = Time.time;

        if (confirmPanel) confirmPanel.SetActive(false);
        if (resultText) resultText.gameObject.SetActive(false);

        // 只用來記錄原色（不在這裡綁 onClick；交給 AnimalButtonScript）
        foreach (var btn in animalButtons)
        {
            if (!btn) continue;
            if (btn.image && !originalColors.ContainsKey(btn))
                originalColors[btn] = btn.image.color;
        }

        if (confirmButton)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(OnConfirm);
        }
        if (retryButton)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(OnRetry);
        }
    }

    // 由 AnimalButtonScript 呼叫
    public void OnAnimalButtonClick(Button btn, string animalName)
    {
        if (string.IsNullOrEmpty(animalName)) animalName = btn != null ? btn.name : "";

        if (selectedSet.Contains(animalName))
        {
            // 已選 → 取消
            selectedSet.Remove(animalName);
            clickedOrder.Remove(animalName);
            if (btn && btn.image && originalColors.TryGetValue(btn, out var oc)) btn.image.color = oc;
        }
        else
        {
            if (selectedSet.Count >= 3) return;       // 已達上限，不再加入
            selectedSet.Add(animalName);
            clickedOrder.Add(animalName);

            if (btn && btn.image)
            {
                var oc = originalColors.ContainsKey(btn) ? originalColors[btn] : Color.white;
                oc.a = 0.5f;
                btn.image.color = oc;
            }
        }

        // 只有選滿「三個不同」才顯示確認面板
        if (confirmPanel) confirmPanel.SetActive(selectedSet.Count == 3);
    }

    public void OnConfirm()
    {
        endTime = Time.time;
        float timeUsed = endTime - startTime;

        // 不計順序：用集合比對
        var correctSet = new HashSet<string>(correctAnswerSequence);
        int matches = 0;
        foreach (var name in clickedOrder)
        {
            if (correctSet.Contains(name)) matches++;
        }

        // 正確率（不計順序）
        float accuracy = correctSet.Count > 0 ? (float)matches / correctSet.Count : 0f;

        // 全對的條件：選到的「不同物件數」與正解數量相同，且每一個都屬於正解集合
        bool allCorrect = (selectedSet.Count == correctSet.Count) && (matches == correctSet.Count);


        if (resultText)
        {
            resultText.gameObject.SetActive(true);
            resultText.text =
                $"你選擇的順序：{string.Join("、", clickedOrder)}\n" +
                $"正確答案：{string.Join("、", correctAnswerSequence)}\n" +
                $"正確率：{accuracy * 100f:F1}%  用時 {timeUsed:F2}s";
        }

        if (confirmPanel) confirmPanel.SetActive(false);
        if (panel1) panel1.SetActive(false);

        // 若要輸出 JSON：
        // ConvertGameDataToJson("Player001", accuracy, timeUsed);
    }

    public void OnRetry()
    {
        selectedSet.Clear();
        clickedOrder.Clear();

        foreach (var btn in animalButtons)
        {
            if (btn && btn.image && originalColors.TryGetValue(btn, out var oc))
                btn.image.color = oc;
        }
        if (confirmPanel) confirmPanel.SetActive(false);
    }

    // （可選）輸出 JSON：若你在別處需要，保留這個
    public string ConvertGameDataToJson(string playerId = "Guest", float accuracy = 0f, float timeUsed = 0f)
    {
        var data = new GameData(
            playerId,
            new List<string>(clickedOrder),
            new List<string>(correctAnswerSequence),
            accuracy,
            timeUsed
        );
        string json = JsonUtility.ToJson(data, true);
        Debug.Log("📄 遊戲數據 JSON:\n" + json);
        return json;
    }
}
