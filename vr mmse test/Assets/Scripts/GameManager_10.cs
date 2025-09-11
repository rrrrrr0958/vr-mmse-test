using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.IO;

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

    [Header("正確答案設定")]
    public bool loadFromPreviousScene = true;  // 是否從前場景載入正確答案
    public List<string> correctAnswerSequence = new List<string> { "兔子", "熊貓", "鹿" }; // 預設答案（備用）

    // 內部狀態
    private readonly List<string> clickedOrder = new List<string>();   // 保留點擊順序（不重覆）
    private readonly HashSet<string> selectedSet = new HashSet<string>(); // 判斷是否已選
    private readonly Dictionary<Button, Color> originalColors = new Dictionary<Button, Color>();

    // 資料載入相關
    private const string SAVE_FILE_NAME = "gamedata.json";
    private const string CUSTOM_DATA_FOLDER = @"C:\Users\kook1\OneDrive\桌面\vr-mmse\vr-mmse-test\vr mmse test\Assets\Data";
    private string saveFilePath;

    private float startTime;
    private float endTime;

    public IReadOnlyList<string> ClickedAnimalSequence => clickedOrder;

    void Awake()
    {
        if (instance == null) 
        {
            instance = this;
            
            // 設定檔案路徑
            saveFilePath = Path.Combine(CUSTOM_DATA_FOLDER, SAVE_FILE_NAME);
            Debug.Log($"🔧 Awake執行，saveFilePath = {saveFilePath}");
            
            // 如果設定要從前場景載入，就載入資料
            if (loadFromPreviousScene)
            {
                Debug.Log("📥 準備載入前場景資料...");
                LoadCorrectAnswerFromFile();
            }
            else
            {
                Debug.Log("❌ loadFromPreviousScene = false，跳過載入");
            }
        }
    }

    void Start()
    {
        startTime = Time.time;

        if (confirmPanel) confirmPanel.SetActive(false);
        if (resultText) resultText.gameObject.SetActive(false);

        // 顯示當前使用的正確答案
        Debug.Log($"當前正確答案序列：{string.Join("、", correctAnswerSequence)}");

        // ☆ 修正：在Start中明確快取按鈕的當前顏色作為原始顏色
        CacheOriginalColors();

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

    // ☆ 新增：在Start時明確快取所有按鈕的原始顏色
    private void CacheOriginalColors()
    {
        originalColors.Clear(); // 清除可能存在的舊快取
        
        foreach (var btn in animalButtons)
        {
            if (btn && btn.image)
            {
                // 直接快取當前在場景中的顏色作為原始顏色
                originalColors[btn] = btn.image.color;
                Debug.Log($"快取按鈕 {btn.name} 的原始顏色: {btn.image.color}");
            }
        }
    }

    // 從前場景的資料檔案載入正確答案
    private void LoadCorrectAnswerFromFile()
    {
        try
        {
            if (File.Exists(saveFilePath))
            {
                string json = File.ReadAllText(saveFilePath);
                Debug.Log($"📁 讀取到的JSON內容：{json}");
                
                // 解析JSON
                var data = JsonUtility.FromJson<GameDataFromFile>(json);
                
                if (data != null && data.selections != null && data.selections.Count > 0)
                {
                    // 使用前場景的選擇作為這場景的正確答案
                    correctAnswerSequence = new List<string>(data.selections);
                    Debug.Log($"✅ 成功從檔案載入正確答案：{string.Join("、", correctAnswerSequence)}");
                    Debug.Log($"正確答案數量：{correctAnswerSequence.Count}");
                }
                else
                {
                    Debug.LogWarning("⚠️ 檔案中的selections為空，使用預設正確答案");
                    Debug.Log($"使用預設答案：{string.Join("、", correctAnswerSequence)}");
                }
            }
            else
            {
                Debug.LogWarning($"⚠️ 找不到資料檔案：{saveFilePath}");
                Debug.Log($"使用預設答案：{string.Join("、", correctAnswerSequence)}");
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"❌ 載入資料失敗：{e.Message}");
            Debug.Log($"使用預設答案：{string.Join("、", correctAnswerSequence)}");
        }
    }

    // 手動重新載入正確答案（測試用）
    [ContextMenu("重新載入正確答案")]
    public void ReloadCorrectAnswer()
    {
        LoadCorrectAnswerFromFile();
    }

    // 由 AnimalButtonScript 呼叫
    public void OnAnimalButtonClick(Button btn, string animalName)
    {
        if (string.IsNullOrEmpty(animalName)) animalName = btn != null ? btn.name : "";

        // ☆ 確保按鈕有快取原始顏色，如果沒有就即時快取
        if (btn && btn.image && !originalColors.ContainsKey(btn))
        {
            originalColors[btn] = btn.image.color;
            Debug.Log($"即時快取按鈕 {btn.name} 的原始顏色: {btn.image.color}");
        }

        // ☆ 修正：檢查按鈕視覺狀態而非邏輯狀態來決定是否還原
        bool isButtonVisuallySelected = false;
        if (btn && btn.image && originalColors.TryGetValue(btn, out var originalColor))
        {
            // 比較當前顏色與原始顏色，如果透明度不同就認為是被選中狀態
            isButtonVisuallySelected = Mathf.Abs(btn.image.color.a - originalColor.a) > 0.1f;
        }

        if (isButtonVisuallySelected)
        {
            // 按鈕目前是選中狀態 → 還原顏色並從邏輯中移除
            if (selectedSet.Contains(animalName))
            {
                selectedSet.Remove(animalName);
                clickedOrder.Remove(animalName);
            }
            
            if (btn && btn.image && originalColors.TryGetValue(btn, out var oc)) 
            {
                btn.image.color = oc;
                Debug.Log($"還原按鈕：{btn.name} 到原始顏色 {oc}");
            }
        }
        else
        {
            // 按鈕目前是未選中狀態 → 選中它
            if (selectedSet.Count >= 3) return;       // 已達上限，不再加入
            
            selectedSet.Add(animalName);
            clickedOrder.Add(animalName);

            if (btn && btn.image && originalColors.TryGetValue(btn, out var oc))
            {
                // 以原始顏色為基礎，只修改透明度
                var selectedColor = oc;
                selectedColor.a = 0.5f;
                btn.image.color = selectedColor;
                Debug.Log($"選中按鈕：{btn.name} 從原始顏色 {oc} 變為選中顏色 {selectedColor}");
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
                $"正確率：{accuracy * 100f:F1}%  用時 {timeUsed:F2}s\n" +
                $"結果：{(allCorrect ? "完全正確！🎉" : "請再試試")}";
        }

        if (confirmPanel) confirmPanel.SetActive(false);
        if (panel1) panel1.SetActive(false);

        // 輸出詳細結果到 Console
        Debug.Log($"🎯 遊戲結果：");
        Debug.Log($"   玩家選擇：{string.Join("、", clickedOrder)}");
        Debug.Log($"   正確答案：{string.Join("、", correctAnswerSequence)}");
        Debug.Log($"   正確率：{accuracy * 100f:F1}%");
        Debug.Log($"   用時：{timeUsed:F2}秒");
        Debug.Log($"   結果：{(allCorrect ? "完全正確！" : "答錯了")}");

        // 若要輸出 JSON：
        ConvertGameDataToJson("Player001", accuracy, timeUsed);
        //轉場
        SceneFlowManager.instance.LoadNextScene();
    }

    public void OnRetry()
    {
        selectedSet.Clear();
        clickedOrder.Clear();

        // ☆ 修正：確保還原到正確的原始顏色
        foreach (var kv in originalColors)
        {
            if (kv.Key && kv.Key.image)
            {
                kv.Key.image.color = kv.Value;
                Debug.Log($"重選：還原按鈕 {kv.Key.name} 到原始顏色: {kv.Value}");
            }
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

// 用於讀取檔案的資料結構
[System.Serializable]
public class GameDataFromFile
{
    public string playerId;
    public string timestamp;
    public List<string> selections;
}