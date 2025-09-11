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
    public GameObject panel1;
    public GameObject confirmPanel;
    public TextMeshProUGUI resultText;

    [Header("Confirm UI Buttons")]
    public Button confirmButton;
    public Button retryButton;

    [Header("Animal Buttons (可留空)")]
    public List<Button> animalButtons = new List<Button>();

    private readonly HashSet<string> selectedSet = new HashSet<string>();
    private readonly Dictionary<Button, Color> originalColors = new Dictionary<Button, Color>();

    private string saveFilePath;
    private const string SAVE_FILE_NAME = "gamedata.json";
    private const string CUSTOM_DATA_FOLDER = @"C:\Users\alanchang\Desktop\unity project_team\vr-mmse-test\vr mmse test\Assets\Data";

    private float startTime;
    private float endTime;

    void Awake()
    {
        SetupCustomSavePath();
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            clickedAnimalSequence = new List<string>();
            selectedSet.Clear();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        startTime = Time.time;
        if (confirmPanel) confirmPanel.SetActive(false);
        if (resultText) resultText.gameObject.SetActive(false);

        // 若清單沒填，保守起見自動收集（包含 Inactive）
        if (animalButtons == null || animalButtons.Count == 0)
            animalButtons = new List<Button>(FindObjectsOfType<Button>(true));

        // 先把已知按鈕的原色快取起來
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

    private void SetupCustomSavePath()
    {
        try
        {
            if (!Directory.Exists(CUSTOM_DATA_FOLDER))
                Directory.CreateDirectory(CUSTOM_DATA_FOLDER);
            saveFilePath = Path.Combine(CUSTOM_DATA_FOLDER, SAVE_FILE_NAME);
        }
        catch
        {
            saveFilePath = Path.Combine(Application.persistentDataPath, SAVE_FILE_NAME);
        }
    }

    // ☆ 新增：第一次看到某按鈕就把原色快取起來
    private void EnsureOriginalColorCached(Button btn)
    {
        if (btn && btn.image && !originalColors.ContainsKey(btn))
            originalColors[btn] = btn.image.color;
    }

    // 主要入口（Button + 名稱）
    public void OnAnimalButtonClick(Button btn, string animalName)
    {
        if (string.IsNullOrEmpty(animalName))
            animalName = btn ? btn.name : "";

        EnsureOriginalColorCached(btn);

        if (selectedSet.Contains(animalName))
        {
            selectedSet.Remove(animalName);
            clickedAnimalSequence.Remove(animalName);

            if (btn && btn.image && originalColors.TryGetValue(btn, out var oc))
                btn.image.color = oc;              // ← 用快取色還原
        }
        else
        {
            if (selectedSet.Count >= 3) return;

            selectedSet.Add(animalName);
            clickedAnimalSequence.Add(animalName);

            if (btn && btn.image)
            {
                var c = btn.image.color;           // ← 以當前色為基礎，只改透明度
                c.a = 0.5f;
                btn.image.color = c;
            }
        }

        SaveToLocalFile();

        if (confirmPanel)
            confirmPanel.SetActive(selectedSet.Count == 3);
    }

    private Button FindButtonByAnimalName(string animalName)
    {
        foreach (var btn in animalButtons)
        {
            if (!btn) continue;
            var script = btn.GetComponent<AnimalButtonScript>();
            if (script && script.animalName == animalName) return btn;

            if (btn.name.Contains(animalName)) return btn;

            var tmp = btn.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp && tmp.text == animalName) return btn;

            var uiText = btn.GetComponentInChildren<Text>();
            if (uiText && uiText.text == animalName) return btn;
        }
        return null;
    }

    public void OnConfirm()
    {
        endTime = Time.time;
        float timeUsed = endTime - startTime;

        if (resultText)
        {
            resultText.gameObject.SetActive(true);
            resultText.text =
                $"你選擇的動物：{string.Join("、", clickedAnimalSequence)}\n" +
                $"選擇數量：{clickedAnimalSequence.Count} 個\n" +
                $"用時：{timeUsed:F2} 秒\n" +
                $"選擇完成！";
        }

        if (confirmPanel) confirmPanel.SetActive(false);
        if (panel1) panel1.SetActive(false);

        SaveWithTimestamp();
        ConvertGameDataToJson();

        //轉場
        SceneFlowManager.instance.LoadNextScene();
    }

    public void OnRetry()
    {
        selectedSet.Clear();
        clickedAnimalSequence.Clear();

        // ☆ 用已快取的鍵集合還原，避免清單漏項
        foreach (var kv in originalColors)
            if (kv.Key && kv.Key.image) kv.Key.image.color = kv.Value;

        if (confirmPanel) confirmPanel.SetActive(false);
        startTime = Time.time;
        SaveToLocalFile();
    }

    public string ConvertGameDataToJson()
    {
        GameDataMenu data = new GameDataMenu("Player001", clickedAnimalSequence);
        return JsonUtility.ToJson(data, true);
    }

    public void SaveToLocalFile()
    {
        try
        {
            File.WriteAllText(saveFilePath, ConvertGameDataToJson());
        }
        catch (System.Exception e)
        {
            Debug.LogError("保存到本地文件失敗：" + e.Message);
        }
    }

    public void LoadFromLocalFile()
    {
        try
        {
            if (!File.Exists(saveFilePath))
            {
                clickedAnimalSequence = new List<string>();
                selectedSet.Clear();
                return;
            }

            string json = File.ReadAllText(saveFilePath);
            GameDataMenu data = JsonUtility.FromJson<GameDataMenu>(json);

            clickedAnimalSequence = (data != null && data.selections != null)
                ? new List<string>(data.selections)
                : new List<string>();

            selectedSet.Clear();
            foreach (var a in clickedAnimalSequence) selectedSet.Add(a);
        }
        catch (System.Exception e)
        {
            Debug.LogError("從本地文件載入失敗：" + e.Message);
            clickedAnimalSequence = new List<string>();
            selectedSet.Clear();
        }
    }

    public void ClearAllData()
    {
        clickedAnimalSequence.Clear();
        selectedSet.Clear();
        try
        {
            if (File.Exists(saveFilePath)) File.Delete(saveFilePath);
        }
        catch (System.Exception e)
        {
            Debug.LogError("刪除本地文件失敗：" + e.Message);
        }
    }

    public void SaveWithTimestamp()
    {
        try
        {
            string timeStamp = System.DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
            string timestampFilePath = Path.Combine(CUSTOM_DATA_FOLDER, $"gamedata_{timeStamp}.json");
            File.WriteAllText(timestampFilePath, ConvertGameDataToJson());
        }
        catch (System.Exception e)
        {
            Debug.LogError("保存時間戳文件失敗：" + e.Message);
        }
    }

    public string GetSaveFilePath() => saveFilePath;
    public string GetDataFolder() => CUSTOM_DATA_FOLDER;

    public void ResetSelection()
    {
        selectedSet.Clear();
        clickedAnimalSequence.Clear();

        foreach (var kv in originalColors)
            if (kv.Key && kv.Key.image) kv.Key.image.color = kv.Value;

        Debug.Log("選擇已重置，可以重新選擇3個動物");
    }

    public bool IsSelectionComplete() => selectedSet.Count >= 3;
    public int GetRemainingSelections() => Mathf.Max(0, 3 - selectedSet.Count);

    public void OnSaveAndQuitButtonClicked()
    {
        SaveWithTimestamp();
    }

    public void OnManualSaveButtonClicked()
    {
        SaveToLocalFile();
        SaveWithTimestamp();
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveToLocalFile();
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus) SaveToLocalFile();
    }
}
