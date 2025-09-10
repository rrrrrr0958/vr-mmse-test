using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEngine.UI;
using TMPro;

public class GameManagerMenu : MonoBehaviour
{
    public static GameManagerMenu instance;
    public List<string> clickedAnimalSequence = new List<string>();
    
    [Header("UI References")]
    public GameObject panel1;                 // 主要遊戲面板
    public GameObject confirmPanel;           // 確認面板
    public TextMeshProUGUI resultText;        // 結果顯示文字

    [Header("Confirm UI Buttons")]
    public Button confirmButton;              // 確認按鈕
    public Button retryButton;                // 重選按鈕

    [Header("Animal Buttons (可留空)")]
    public List<Button> animalButtons = new List<Button>(); // 用來重置顏色

    // 內部狀態
    private readonly HashSet<string> selectedSet = new HashSet<string>(); // 判斷是否已選
    private readonly Dictionary<Button, Color> originalColors = new Dictionary<Button, Color>();
    
    // 本地存儲相關變數
    private string saveFilePath;
    private const string SAVE_FILE_NAME = "gamedata.json";
    private const string CUSTOM_DATA_FOLDER = @"C:\Users\kook1\OneDrive\桌面\vr-mmse\vr-mmse-test\vr mmse test\Assets\Data";

    // 時間記錄
    private float startTime;
    private float endTime;

    void Awake()
    {
        // 設置自定義保存文件路徑
        SetupCustomSavePath();
        Debug.Log("保存文件路徑：" + saveFilePath);

        // 實現單例模式
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            
            // 每次遊戲都從新的空列表開始
            clickedAnimalSequence = new List<string>();
            selectedSet.Clear();
            Debug.Log("開始新的遊戲會話，使用空的動物序列");
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        startTime = Time.time;

        // 初始化 UI 狀態 - 隱藏確認面板和結果文字
        if (confirmPanel) confirmPanel.SetActive(false);
        if (resultText) resultText.gameObject.SetActive(false);

        // 記錄動物按鈕的原始顏色（不需要綁定事件，由 AnimalButtonScript 處理）
        foreach (var btn in animalButtons)
        {
            if (!btn) continue;
            if (btn.image && !originalColors.ContainsKey(btn))
                originalColors[btn] = btn.image.color;
        }

        // 設置按鈕事件
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

    // 設置自定義保存路徑
    private void SetupCustomSavePath()
    {
        try
        {
            // 確保目錄存在
            if (!Directory.Exists(CUSTOM_DATA_FOLDER))
            {
                Directory.CreateDirectory(CUSTOM_DATA_FOLDER);
                Debug.Log("創建數據目錄：" + CUSTOM_DATA_FOLDER);
            }
            
            // 設置完整的文件路徑
            saveFilePath = Path.Combine(CUSTOM_DATA_FOLDER, SAVE_FILE_NAME);
        }
        catch (System.Exception e)
        {
            Debug.LogError("設置自定義路徑失敗，使用默認路徑：" + e.Message);
            // 如果自定義路徑失敗，使用默認路徑作為備案
            saveFilePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        }
    }

    // 重載方法：接收按鈕參數（參考 GameManager 的方式）
    public void OnAnimalButtonClick(Button btn, string animalName)
    {
        Debug.Log($"=== OnAnimalButtonClick (with Button) 被調用！按鈕：{btn?.name}，動物：{animalName} ===");
        
        if (string.IsNullOrEmpty(animalName)) animalName = btn != null ? btn.name : "";

        if (selectedSet.Contains(animalName))
        {
            // 已選 → 取消
            selectedSet.Remove(animalName);
            clickedAnimalSequence.Remove(animalName);
            if (btn && btn.image && originalColors.TryGetValue(btn, out var oc)) 
            {
                btn.image.color = oc;
                Debug.Log($"恢復按鈕 {btn.name} 的原始顏色");
            }
        }
        else
        {
            if (selectedSet.Count >= 3) 
            {
                Debug.Log("已達選擇上限（3個），無法再選擇");
                return;
            }
            
            selectedSet.Add(animalName);
            clickedAnimalSequence.Add(animalName);

            if (btn && btn.image)
            {
                var oc = originalColors.ContainsKey(btn) ? originalColors[btn] : Color.white;
                oc.a = 0.5f;
                btn.image.color = oc;
                Debug.Log($"將按鈕 {btn.name} 設為選中顏色");
            }
        }

        // 每次點擊後自動保存到本地文件
        SaveToLocalFile();
        
        // 只有選滿「三個不同」才顯示確認面板
        if (confirmPanel) 
        {
            confirmPanel.SetActive(selectedSet.Count == 3);
            if (selectedSet.Count == 3)
            {
                Debug.Log("已選滿3個動物，顯示確認面板");
            }
        }
    }

    // 原有方法：只接收動物名稱（備用）
    public void OnAnimalButtonClick(string animalName)
    {
        Debug.Log("=== OnAnimalButtonClick 被調用！動物名稱：" + animalName + " ===");
        
        // 找到對應的按鈕（用於視覺效果）
        Button clickedButton = FindButtonByAnimalName(animalName);

        if (selectedSet.Contains(animalName))
        {
            // 已選 → 取消選擇
            selectedSet.Remove(animalName);
            clickedAnimalSequence.Remove(animalName);
            
            // 恢復按鈕原始顏色
            if (clickedButton && clickedButton.image && originalColors.TryGetValue(clickedButton, out var originalColor))
            {
                clickedButton.image.color = originalColor;
                Debug.Log($"恢復按鈕 {clickedButton.name} 的原始顏色");
            }
            
            Debug.Log($"取消選擇：{animalName}，剩餘：{clickedAnimalSequence.Count} 項");
        }
        else
        {
            // 檢查是否已達上限
            if (selectedSet.Count >= 3) 
            {
                Debug.Log("已達選擇上限（3個），無法再選擇");
                return;
            }
            
            // 新選擇
            selectedSet.Add(animalName);
            clickedAnimalSequence.Add(animalName);
            
            // 改變按鈕顏色表示已選中
            if (clickedButton && clickedButton.image)
            {
                var selectedColor = originalColors.ContainsKey(clickedButton) ? originalColors[clickedButton] : Color.white;
                selectedColor.a = 0.5f; // 設置半透明效果
                clickedButton.image.color = selectedColor;
                Debug.Log($"將按鈕 {clickedButton.name} 設為選中顏色");
            }
            else
            {
                Debug.LogWarning($"找不到動物 {animalName} 對應的按鈕，無法改變顏色");
            }
            
            Debug.Log($"新選擇：{animalName}，總計：{clickedAnimalSequence.Count} 項");
        }

        // 每次點擊後自動保存到本地文件
        SaveToLocalFile();
        
        // 只有選滿3個不同動物才顯示確認面板
        if (confirmPanel) 
        {
            confirmPanel.SetActive(selectedSet.Count == 3);
            if (selectedSet.Count == 3)
            {
                Debug.Log("已選滿3個動物，顯示確認面板");
            }
        }
    }

    // 根據動物名稱查找對應的按鈕
    private Button FindButtonByAnimalName(string animalName)
    {
        foreach (var btn in animalButtons)
        {
            if (!btn) continue;
            
            // 方法1：檢查按鈕上的 AnimalButtonScript
            AnimalButtonScript animalScript = btn.GetComponent<AnimalButtonScript>();
            if (animalScript && animalScript.animalName == animalName)
            {
                Debug.Log($"通過 AnimalButtonScript 找到按鈕：{btn.name} -> {animalName}");
                return btn;
            }
            
            // 方法2：檢查按鈕名稱
            if (btn.name.Contains(animalName))
            {
                Debug.Log($"通過按鈕名稱找到按鈕：{btn.name} -> {animalName}");
                return btn;
            }
            
            // 方法3：檢查子物件的文字
            TextMeshProUGUI tmpText = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmpText && tmpText.text == animalName)
            {
                Debug.Log($"通過 TextMeshProUGUI 找到按鈕：{btn.name} -> {animalName}");
                return btn;
            }
            
            UnityEngine.UI.Text uiText = btn.GetComponentInChildren<UnityEngine.UI.Text>();
            if (uiText && uiText.text == animalName)
            {
                Debug.Log($"通過 UI Text 找到按鈕：{btn.name} -> {animalName}");
                return btn;
            }
        }
        
        Debug.LogWarning($"找不到動物 {animalName} 對應的按鈕！請檢查 Animal Buttons 列表是否包含所有按鈕");
        return null;
    }

    // 確認選擇
    public void OnConfirm()
    {
        endTime = Time.time;
        float timeUsed = endTime - startTime;

        Debug.Log($"確認選擇！選擇的動物：{string.Join("、", clickedAnimalSequence)}");
        Debug.Log($"用時：{timeUsed:F2}秒");

        // 顯示結果
        if (resultText)
        {
            resultText.gameObject.SetActive(true);
            resultText.text = 
                $"你選擇的動物：{string.Join("、", clickedAnimalSequence)}\n" +
                $"選擇數量：{clickedAnimalSequence.Count} 個\n" +
                $"用時：{timeUsed:F2} 秒\n" +
                $"選擇完成！";
        }

        // 隱藏確認面板和主要面板
        if (confirmPanel) confirmPanel.SetActive(false);
        if (panel1) panel1.SetActive(false);

        // 創建帶時間戳的最終保存
        SaveWithTimestamp();
        
        // 輸出最終 JSON 數據
        ConvertGameDataToJson();
    }

    // 重新選擇
    public void OnRetry()
    {
        Debug.Log("重新選擇動物");
        
        // 清空所有選擇
        selectedSet.Clear();
        clickedAnimalSequence.Clear();

        // 恢復所有按鈕的原始顏色
        foreach (var btn in animalButtons)
        {
            if (btn && btn.image && originalColors.TryGetValue(btn, out var originalColor))
                btn.image.color = originalColor;
        }
        
        // 隱藏確認面板
        if (confirmPanel) confirmPanel.SetActive(false);
        
        // 重置開始時間
        startTime = Time.time;
        
        // 清空本地保存的數據
        SaveToLocalFile();
    }

    public string ConvertGameDataToJson()
    {
        GameDataMenu data = new GameDataMenu("Player001", clickedAnimalSequence);
        string json = JsonUtility.ToJson(data, true);
        Debug.Log("遊戲數據 JSON：\n" + json);
        return json;
    }

    // 保存數據到本地文件
    public void SaveToLocalFile()
    {
        try
        {
            string json = ConvertGameDataToJson();
            File.WriteAllText(saveFilePath, json);
            Debug.Log($"數據已保存到：{saveFilePath}");
            Debug.Log($"保存數量：{clickedAnimalSequence.Count} 項");
        }
        catch (System.Exception e)
        {
            Debug.LogError("保存到本地文件失敗：" + e.Message);
        }
    }

    // 從本地文件載入數據
    public void LoadFromLocalFile()
    {
        try
        {
            if (File.Exists(saveFilePath))
            {
                string json = File.ReadAllText(saveFilePath);
                GameDataMenu data = JsonUtility.FromJson<GameDataMenu>(json);
                
                if (data != null && data.selections != null)
                {
                    clickedAnimalSequence = new List<string>(data.selections);
                    
                    // 同步 selectedSet
                    selectedSet.Clear();
                    foreach (string animal in clickedAnimalSequence)
                    {
                        selectedSet.Add(animal);
                    }
                    
                    Debug.Log($"從本地文件載入數據成功！數量：{clickedAnimalSequence.Count} 項");
                    Debug.Log("載入的數據：" + string.Join(", ", clickedAnimalSequence));
                    Debug.Log("文件位置：" + saveFilePath);
                }
                else
                {
                    Debug.Log("本地文件存在但數據為空，使用新的列表");
                    clickedAnimalSequence = new List<string>();
                    selectedSet.Clear();
                }
            }
            else
            {
                Debug.Log("本地文件不存在，使用新的列表");
                Debug.Log("預期文件位置：" + saveFilePath);
                clickedAnimalSequence = new List<string>();
                selectedSet.Clear();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("從本地文件載入失敗：" + e.Message);
            clickedAnimalSequence = new List<string>();
            selectedSet.Clear();
        }
    }

    // 清空本地數據
    public void ClearAllData()
    {
        clickedAnimalSequence.Clear();
        selectedSet.Clear();
        
        try
        {
            if (File.Exists(saveFilePath))
            {
                File.Delete(saveFilePath);
                Debug.Log("本地數據已完全清空！");
                Debug.Log("刪除文件：" + saveFilePath);
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("刪除本地文件失敗：" + e.Message);
        }
    }

    // 保存數據到帶時間戳的文件（用於備份）
    public void SaveWithTimestamp()
    {
        try
        {
            string timeStamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string timestampFileName = $"gamedata_{timeStamp}.json";
            string timestampFilePath = Path.Combine(CUSTOM_DATA_FOLDER, timestampFileName);
            
            string json = ConvertGameDataToJson();
            File.WriteAllText(timestampFilePath, json);
            Debug.Log($"帶時間戳的備份已保存到：{timestampFilePath}");
        }
        catch (System.Exception e)
        {
            Debug.LogError("保存時間戳文件失敗：" + e.Message);
        }
    }

    public string GetSaveFilePath()
    {
        return saveFilePath;
    }

    public string GetDataFolder()
    {
        return CUSTOM_DATA_FOLDER;
    }

    // 手動重置選擇（可用於重新開始）
    public void ResetSelection()
    {
        selectedSet.Clear();
        clickedAnimalSequence.Clear();
        
        // 恢復所有按鈕顏色
        foreach (var btn in animalButtons)
        {
            if (btn && btn.image && originalColors.TryGetValue(btn, out var originalColor))
                btn.image.color = originalColor;
        }
        
        Debug.Log("選擇已重置，可以重新選擇3個動物");
    }

    // 檢查是否已選滿
    public bool IsSelectionComplete()
    {
        return selectedSet.Count >= 3;
    }

    // 取得剩餘可選數量
    public int GetRemainingSelections()
    {
        return Mathf.Max(0, 3 - selectedSet.Count);
    }

    // 保存並退出（只使用本地存儲）
    public void OnSaveAndQuitButtonClicked()
    {
        // 創建時間戳備份
        SaveWithTimestamp();
        Debug.Log("數據已保存完成！");
    }

    // 手動保存按鈕
    public void OnManualSaveButtonClicked()
    {
        SaveToLocalFile();
        SaveWithTimestamp();
        Debug.Log("手動保存完成！");
    }

    // 應用程式暫停時自動保存
    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            SaveToLocalFile();
            Debug.Log("應用程式暫停，自動保存數據");
        }
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            SaveToLocalFile();
            Debug.Log("應用程式失去焦點，自動保存數據");
        }
    }
}