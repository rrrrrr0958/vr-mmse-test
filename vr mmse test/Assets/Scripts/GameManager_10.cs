using System.Collections; // for IEnumerator / Coroutine
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Linq;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    [Header("UI References")]
    public GameObject panel1;
    public GameObject confirmPanel;
    public TextMeshProUGUI resultText;

    [Header("Confirm UI Buttons")]
    public Button confirmButton;
    public Button retryButton;

    [Header("Animal Buttons (可留空)")]
    public List<Button> animalButtons = new List<Button>();

    [Header("正確答案設定")]
    public bool loadFromPreviousScene = true;
    public List<string> correctAnswerSequence = new List<string> { "兔子", "熊貓", "鹿" };

    // 狀態
    private readonly List<string> clickedOrder = new List<string>();
    private readonly HashSet<string> selectedSet = new HashSet<string>();
    private readonly Dictionary<Button, Color> originalColors = new Dictionary<Button, Color>();

    // 檔案（照你指定的絕對路徑）
    private const string CUSTOM_DATA_FOLDER = @"C:\Users\alanchang\Desktop\unity project_team\vr-mmse-test\vr mmse test\Assets\Data";

    private float startTime;
    private float endTime;

    // ★ 提供給其它腳本使用
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

        if (loadFromPreviousScene)
            StartCoroutine(LoadAnswersNextFrame());

        foreach (var btn in animalButtons)
            EnsureOriginalColorCached(btn);

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

    private IEnumerator LoadAnswersNextFrame()
    {
        // 等一下，避免讀到上一場景尚未 flush 完成的舊檔
        yield return new WaitForSeconds(0.05f);
        LoadCorrectAnswerFromFile();
    }

    private void EnsureOriginalColorCached(Button btn)
    {
        if (btn && btn.image && !originalColors.ContainsKey(btn))
            originalColors[btn] = btn.image.color;
    }

    // 讀取最新備份檔（gamedata_*.json）
    private string GetLatestBackupFile()
    {
        if (!Directory.Exists(CUSTOM_DATA_FOLDER)) return null;
        var files = Directory.GetFiles(CUSTOM_DATA_FOLDER, "gamedata_*.json");
        if (files.Length == 0) return null;

        return files
            .OrderByDescending(f => File.GetCreationTime(f))
            .FirstOrDefault();
    }

    private void LoadCorrectAnswerFromFile()
    {
        try
        {
            string latestBackup = GetLatestBackupFile();
            if (string.IsNullOrEmpty(latestBackup))
            {
                Debug.LogWarning("⚠ 沒有找到任何備份檔，使用預設答案");
                return;
            }

            string json = File.ReadAllText(latestBackup);
            Debug.Log($"[GM] 從最新備份檔讀取：{latestBackup}\n內容={json}");

            var data = JsonUtility.FromJson<GameDataFromFile>(json);
            if (data != null && data.selections != null && data.selections.Length > 0)
            {
                correctAnswerSequence = new List<string>(data.selections);
                Debug.Log($"✅ 成功載入：{string.Join("、", correctAnswerSequence)}");
            }
            else
            {
                Debug.LogWarning("⚠ 備份檔 selections 為空，使用預設答案");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 載入備份檔失敗：{e.Message}");
        }
    }

    // 由 AnimalButtonScript 呼叫
    public void OnAnimalButtonClick(Button btn, string animalName)
    {
        if (string.IsNullOrEmpty(animalName)) animalName = btn != null ? btn.name : "";
        EnsureOriginalColorCached(btn);

        if (selectedSet.Contains(animalName))
        {
            selectedSet.Remove(animalName);
            clickedOrder.Remove(animalName);

            if (btn && btn.image && originalColors.TryGetValue(btn, out var oc))
                btn.image.color = oc; // 還原
        }
        else
        {
            if (selectedSet.Count >= 3) return;
            selectedSet.Add(animalName);
            clickedOrder.Add(animalName);

            if (btn && btn.image && originalColors.TryGetValue(btn, out var oc))
            {
                var darker = oc * 0.7f; // 變深
                darker.a = 1f;
                btn.image.color = darker;
            }
        }

        if (confirmPanel) confirmPanel.SetActive(selectedSet.Count == 3);
    }

    public void OnConfirm()
    {
        endTime = Time.time;
        float timeUsed = endTime - startTime;

        var correctSet = new HashSet<string>(correctAnswerSequence);
        int matches = 0;
        foreach (var name in clickedOrder)
            if (correctSet.Contains(name)) matches++;

        float accuracy = correctSet.Count > 0 ? (float)matches / correctSet.Count : 0f;
        bool allCorrect = (selectedSet.Count == correctSet.Count) && (matches == correctSet.Count);

        if (resultText)
        {
            resultText.gameObject.SetActive(true);
            resultText.text =
                $"你選擇的順序：{string.Join("、", clickedOrder)}\n" +
                $"正確答案：{string.Join("、", correctAnswerSequence)}\n" +
                $"正確率：{accuracy * 100f:F1}% 用時 {timeUsed:F2}s\n" +
                $"結果：{(allCorrect ? "完全正確！🎉" : "請再試試")}";
        }

        if (confirmPanel) confirmPanel.SetActive(false);
        if (panel1) panel1.SetActive(false);

        // 保留：其他腳本要用的 JSON 字串
        ConvertGameDataToJson("Player001", accuracy, timeUsed);

        SceneFlowManager.instance.LoadNextScene();
    }

    public void OnRetry()
    {
        selectedSet.Clear();
        clickedOrder.Clear();

        foreach (var kv in originalColors)
            if (kv.Key && kv.Key.image)
                kv.Key.image.color = kv.Value; // 還原正確顏色

        if (confirmPanel) confirmPanel.SetActive(false);
    }

    // 其它腳本（ResultManager_10 等）仍然可以呼叫
    public string ConvertGameDataToJson(string playerId = "Guest", float accuracy = 0f, float timeUsed = 0f)
    {
        var data = new GameData( // ← 使用你原本專案裡的 GameData 類別
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

// 讀檔用資料結構：用 string[] 避免 JsonUtility 的 List 反序列化問題
[System.Serializable]
public class GameDataFromFile
{
    public string playerId;
    public string timestamp;
    public string[] selections;
}
