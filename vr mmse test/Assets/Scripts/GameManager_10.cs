using System.Collections; // for IEnumerator / Coroutine
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;
using System.Linq;

[DefaultExecutionOrder(-100)]
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

    // ★ 原色快取（必用它來還原／變色）
    private readonly Dictionary<Button, Color> originalColors = new Dictionary<Button, Color>();

    // 檔案（照你指定的絕對路徑）
    private const string CUSTOM_DATA_FOLDER = @"C:\Users\alanchang\Desktop\unity project_team\vr-mmse-test\vr mmse test\Assets\Data";

    private float startTime;
    private float endTime;

    // 提供給其它腳本使用
    public IReadOnlyList<string> ClickedAnimalSequence => clickedOrder;

    void Awake()
    {
        // 單例
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;

        // 1) 若清單沒填，先在 Awake 早期就自動收集（包含 Inactive）
        if (animalButtons == null || animalButtons.Count == 0)
        {
#if UNITY_2023_1_OR_NEWER
            animalButtons = new List<Button>(
                FindObjectsByType<Button>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None
                )
            );
#else
            animalButtons = new List<Button>(FindObjectsOfType<Button>(true));
#endif
        }

        // 2) 在任何可能變色之前，把原色快取起來（只快取一次）
        foreach (var btn in animalButtons)
        {
            EnsureOriginalColorCached(btn);
        }
    }

    void Start()
    {
        startTime = Time.time;

        if (confirmPanel) confirmPanel.SetActive(false);
        if (resultText) resultText.gameObject.SetActive(false);

        if (loadFromPreviousScene)
            StartCoroutine(LoadAnswersNextFrame());

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
        if (!btn) return;

        // Button.targetGraphic 通常等於 btn.image（UGUI Button）
        var img = btn.image;
        if (img && !originalColors.ContainsKey(btn))
        {
            originalColors[btn] = img.color;   // ← 僅在第一次快取
        }
    }

    private void RestoreButtonColor(Button btn)
    {
        if (!btn) return;
        var img = btn.image;
        if (img && originalColors.TryGetValue(btn, out var oc))
        {
            img.color = oc; // ← 完整還原到當初快取的顏色
        }
    }

    private void TintButtonDarker(Button btn, float factor = 0.7f)
    {
        if (!btn) return;
        var img = btn.image;
        if (img && originalColors.TryGetValue(btn, out var oc))
        {
            // 一律以「原色」為基底變深，避免累積變暗/還原不全
            var darker = oc * factor;
            darker.a = oc.a; // 保留原本 alpha
            img.color = darker;
        }
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

    // 由 AnimalButtonScript / AnimalSelectionManager 呼叫
    public void OnAnimalButtonClick(Button btn, string animalName)
    {
        if (string.IsNullOrEmpty(animalName)) animalName = btn ? btn.name : "";
        EnsureOriginalColorCached(btn); // 不會覆蓋既有快取

        if (selectedSet.Contains(animalName))
        {
            selectedSet.Remove(animalName);
            clickedOrder.Remove(animalName);

            // ★ 完整還原
            RestoreButtonColor(btn);
        }
        else
        {
            if (selectedSet.Count >= 3) return;
            selectedSet.Add(animalName);
            clickedOrder.Add(animalName);

            // ★ 依原色變深（不會累積）
            TintButtonDarker(btn, 0.7f);
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
                $"結果：{(allCorrect ? "完全正確！" : "請再試試")}";
        }

        if (confirmPanel) confirmPanel.SetActive(false);
        if (panel1) panel1.SetActive(false);

        // 保留：其他腳本要用的 JSON 字串
        ConvertGameDataToJson("Player001", accuracy, timeUsed);

        // 若 SceneFlowManager 沒掛，避免 NRE
        if (SceneFlowManager.instance != null)
            SceneFlowManager.instance.LoadNextScene();
        else
            Debug.LogWarning("[GM] SceneFlowManager.instance 為 null，略過切換場景");
    }

    public void OnRetry()
    {
        selectedSet.Clear();
        clickedOrder.Clear();

        // ★ 保證全部按鈕回到原色
        foreach (var kv in originalColors)
            RestoreButtonColor(kv.Key);

        if (confirmPanel) confirmPanel.SetActive(false);
        if (resultText) resultText.gameObject.SetActive(false);
        if (panel1) panel1.SetActive(true);
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
