using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class GameManagerMenu : MonoBehaviour
{
    public static GameManagerMenu instance;
    public List<string> clickedAnimalSequence = new List<string>();
    
    // 本地存儲相關變數
    private string saveFilePath;
    private const string SAVE_FILE_NAME = "gamedata.json";
    private const string CUSTOM_DATA_FOLDER = @"C:\Users\USER\Desktop\vr-mmse-test\vr-mmse-test\vr mmse test\Assets\Data";

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
            Debug.Log("開始新的遊戲會話，使用空的動物序列");
        }
        else
        {
            Destroy(gameObject);
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

    public void OnAnimalButtonClick(string animalName)
    {
        Debug.Log("你點擊了：" + animalName);
        clickedAnimalSequence.Add(animalName);
        
        // 每次點擊後自動保存到本地文件
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
                    Debug.Log($"從本地文件載入數據成功！數量：{clickedAnimalSequence.Count} 項");
                    Debug.Log("載入的數據：" + string.Join(", ", clickedAnimalSequence));
                    Debug.Log("文件位置：" + saveFilePath);
                }
                else
                {
                    Debug.Log("本地文件存在但數據為空，使用新的列表");
                    clickedAnimalSequence = new List<string>();
                }
            }
            else
            {
                Debug.Log("本地文件不存在，使用新的列表");
                Debug.Log("預期文件位置：" + saveFilePath);
                clickedAnimalSequence = new List<string>();
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError("從本地文件載入失敗：" + e.Message);
            clickedAnimalSequence = new List<string>();
        }
    }

    // 清空本地數據
    public void ClearAllData()
    {
        clickedAnimalSequence.Clear();
        
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