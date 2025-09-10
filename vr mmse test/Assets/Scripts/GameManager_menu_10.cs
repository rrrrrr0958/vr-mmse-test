using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using System.IO;

public class GameManagerMenu : MonoBehaviour
{
    public static GameManagerMenu instance;
    
    [Header("UI References")]
    public GameObject confirmPanel;           // Panel2_1
    public TextMeshProUGUI resultText;        // ResultText_1（TMP）

    [Header("Confirm UI Buttons")]
    public Button confirmButton;              // Panel2_1/確認
    public Button retryButton;                // Panel2_1/重選

    [Header("Animal Buttons (可留空)")]
    public List<Button> animalButtons = new List<Button>(); // 用來重置顏色

    public List<string> clickedAnimalSequence = new List<string>();
    
    // 內部狀態
    private readonly HashSet<string> selectedSet = new HashSet<string>(); // 判斷是否已選
    private readonly Dictionary<Button, Color> originalColors = new Dictionary<Button, Color>();
    
    // 本地存儲相關變數
    private string saveFilePath;
    private const string SAVE_FILE_NAME = "gamedata.json";
    private const string CUSTOM_DATA_FOLDER = @"C:\Users\alanchang\Desktop\unity project_team\vr-mmse-test\vr mmse test\Assets\Data";

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

        // 初始化UI狀態
        if (confirmPanel) confirmPanel.SetActive(false);
        if (resultText) resultText.gameObject.SetActive(false);

        // 記錄動物按鈕的原始顏色
        foreach (var btn in animalButtons)
        {
            if (!btn) continue;
            if (btn.image && !originalColors.ContainsKey(btn))
                originalColors[btn] = btn.image.color;
        }

        // 設置確認和重選按鈕的事件
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

    // 動物按鈕點擊事件 - 更新版本
    public void OnAnimalButtonClick(Button btn, string animalName)
    {
        if (string.IsNullOrEmpty(animalName)) animalName = btn != null ? btn.name : "";

        Debug.Log("你點擊了：" + animalName);

        if (selectedSet.Contains(animalName))
        {
            // 已選 → 取消
            selectedSet.Remove(animalName);
            clickedAnimalSequence.Remove(animalName);
            if (btn && btn.image && originalColors.TryGetValue(btn, out var oc)) 
                btn.image.color = oc;
            
            Debug.Log($"取消選擇：{animalName}，剩餘選擇：{string.Join("、", clickedAnimalSequence)}");
        }
        else
        {
            if (selectedSet.Count >= 3) return;       // 已達上限，不再加入
            selectedSet.Add(animalName);
            clickedAnimalSequence.Add(animalName);

            if (btn && btn.image)
            {
                var oc = originalColors.ContainsKey(btn) ? originalColors[btn] : Color.white;
                oc.a = 0.5f;  // 設置透明度表示已選
                btn.image.color = oc;
            }
            
            Debug.Log($"選擇：{animalName}，目前選擇：{string.Join("、", clickedAnimalSequence)}");
        }

        // 只有選滿「三個不同」才顯示確認面板
        if (confirmPanel) 
        {
            bool shouldShowPanel = selectedSet.Count == 3;
            confirmPanel.SetActive(shouldShowPanel);
            
            if (shouldShowPanel)
            {
                Debug.Log("已選滿3個動物，顯示確認面板");
            }
        }

        // 每次點擊後自動保存到本地文件
        SaveToLocalFile();
    }

    // 舊版本的點擊方法保持兼容
    public void OnAnimalButtonClick(string animalName)
    {
        OnAnimalButtonClick(null, animalName);
    }

    // 確認按鈕事件
    public void OnConfirm()
    {
        endTime = Time.time;
        float timeUsed = endTime - startTime;

        if (resultText)
        {
            resultText.gameObject.SetActive(true);
            resultText.text =
                $"你選擇的動物順序：{string.Join("、", clickedAnimalSequence)}\n" +
                $"選擇數量：{clickedAnimalSequence.Count}/3\n" +
                $"用時：{timeUsed:F2}秒\n" +
                $"選擇完成！🎉";
        }

        if (confirmPanel) confirmPanel.SetActive(false);

        // 輸出詳細結果到 Console
        Debug.Log($"🎯 選擇完成：");
        Debug.Log($"   選擇順序：{string.Join("、", clickedAnimalSequence)}");
        Debug.Log($"   用時：{timeUsed:F2}秒");

        // 保存最終結果
        SaveToLocalFile();
        SaveWithTimestamp();
    }

    // 重選按鈕事件
    public void OnRetry()
    {
        selectedSet.Clear();
        clickedAnimalSequence.Clear();

        // 恢復所有按鈕顏色
        foreach (var btn in animalButtons)
        {
            if (btn && btn.image && originalColors.TryGetValue(btn, out var oc))
                btn.image.color = oc;
        }
        
        if (confirmPanel) confirmPanel.SetActive(false);
        if (resultText) resultText.gameObject.SetActive(false);
        
        // 重置計時
        startTime = Time.time;
        
        Debug.Log("重新選擇，數據已清空");
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
                    
                    // 同步更新 selectedSet
                    selectedSet.Clear();
                    foreach (var animal in clickedAnimalSequence)
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
        
        // 恢復所有按鈕顏色
        foreach (var btn in animalButtons)
        {
            if (btn && btn.image && originalColors.TryGetValue(btn, out var oc))
                btn.image.color = oc;
        }
        
        if (confirmPanel) confirmPanel.SetActive(false);
        if (resultText) resultText.gameObject.SetActive(false);
        
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
        OnRetry(); // 使用統一的重置邏輯
    }

    // 檢查是否已選滿
    public bool IsSelectionComplete()
    {
        return clickedAnimalSequence.Count >= 3;
    }

    // 取得剩餘可選數量
    public int GetRemainingSelections()
    {
        return Mathf.Max(0, 3 - clickedAnimalSequence.Count);
    }

    // 保存並退出（只使用本地存儲）
    public void OnSaveAndQuitButtonClicked()
    {
        // 創建時間戳備份
        SaveWithTimestamp();
        Debug.Log("數據已保存完成！");
        
        // 如果需要清空數據，可以調用 ClearAllData()
        // ClearAllData();
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