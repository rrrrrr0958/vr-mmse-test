using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.SceneManagement;
using System.Collections;

[DefaultExecutionOrder(-50)]
public class GameManagerMenu_10 : MonoBehaviour
{
    // === 仍保留但不使用的欄位（避免 Prefab 失參考） ===
    [Header("UI References (not used)")]
    public GameObject panel1;
    public GameObject confirmPanel;
    public TextMeshProUGUI resultText;

    [Header("Confirm UI Buttons (not used)")]
    public Button confirmButton;
    public Button retryButton;

    // === 實際使用欄位 ===
    public static GameManagerMenu_10 instance;

    [Header("Animal Buttons (可留空)")]
    public List<Button> animalButtons = new List<Button>();

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip clickSound;

    [Header("Save Settings (相對路徑)")]
    public string saveFolder = "Game_10";

    public List<string> clickedAnimalSequence = new List<string>();

    private readonly HashSet<string> selectedSet = new HashSet<string>();
    private readonly Dictionary<Button, Color> originalColors = new Dictionary<Button, Color>();

    private string saveFilePath;
    private bool hasProgressed = false;   // 避免重複存檔與重複切場景
    private float startTime;

    void Awake()
    {
        SetupRelativeSavePath();

        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            clickedAnimalSequence.Clear();
            selectedSet.Clear();
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        startTime = Time.time;

        // 不再使用 confirmPanel / resultText
        if (confirmPanel) confirmPanel.SetActive(false);
        if (resultText) resultText.gameObject.SetActive(false);

        StartCoroutine(WaitAndBindUI());
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[Menu] 場景載入完成：{scene.name} → 重新綁定 UI");
        StartCoroutine(WaitAndBindUI());
    }

    private IEnumerator WaitAndBindUI()
    {
        float timeout = 5f;
        while (timeout > 0f)
        {
            bool allFound = TryFindUI();
            if (allFound) break;
            timeout -= Time.deltaTime;
            yield return null;
        }
    }

    private bool TryFindUI()
    {
        // 只處理動物按鈕；不再尋找/綁定 confirm / retry
        if (animalButtons == null || animalButtons.Count == 0)
        {
            var allBtns = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            animalButtons = new List<Button>(allBtns);
        }

        foreach (var btn in animalButtons)
            EnsureOriginalColorCached(btn);

        return true;
    }

    private void PlayClickSound()
    {
        if (audioSource && clickSound)
            audioSource.PlayOneShot(clickSound);
    }

    private void SetupRelativeSavePath()
    {
        // 依你的要求：不要改存檔路徑策略
        try
        {
            string folderPath = Path.Combine(Application.dataPath, "Scripts", saveFolder);
            Directory.CreateDirectory(folderPath);
            saveFilePath = Path.Combine(folderPath, "gamedata.json");
        }
        catch
        {
            // 原程式的 fallback 保留
            saveFilePath = Path.Combine(Application.persistentDataPath, "gamedata.json");
        }
        Debug.Log($"[Menu] 儲存路徑：{saveFilePath}");
    }

    private void EnsureOriginalColorCached(Button btn)
    {
        if (btn && btn.image && !originalColors.ContainsKey(btn))
            originalColors[btn] = btn.image.color;
    }

    // 供按鈕的 OnClick 傳入 (Button 自己, 顯示名稱/動物名)
    public void OnAnimalButtonClick(Button btn, string animalName)
    {
        if (hasProgressed) return; // 已經進入下一關就忽略後續點擊

        PlayClickSound();

        if (string.IsNullOrEmpty(animalName))
            animalName = btn ? btn.name : "";

        EnsureOriginalColorCached(btn);

        if (selectedSet.Contains(animalName))
        {
            // 允許反選
            selectedSet.Remove(animalName);
            clickedAnimalSequence.Remove(animalName);
            if (btn && btn.image && originalColors.TryGetValue(btn, out var oc))
                btn.image.color = oc;
        }
        else
        {
            if (selectedSet.Count >= 3) return; // 已到上限
            selectedSet.Add(animalName);
            clickedAnimalSequence.Add(animalName);
            if (btn && btn.image)
            {
                var c = btn.image.color;
                c.a = 0.5f;
                btn.image.color = c;
            }
        }

        // 每次點擊都嘗試存檔；失敗會在 Console 顯示細節
        bool saveOk = SaveToLocalFile();

        // 滿 3 個就立即前往下一關（不需要 Confirm/Retry）
        if (!hasProgressed && selectedSet.Count == 3)
        {
            // 再存一次，確保最終狀態寫入
            saveOk = SaveToLocalFile() && saveOk;

            hasProgressed = true; // 去抖動，避免重複切場景
            if (SceneFlowManager.instance != null)
            {
                Debug.Log($"[Menu] 已選滿 3 個，saveOk={saveOk} → LoadNextScene()");
                SceneFlowManager.instance.LoadNextScene();
            }
            else
            {
                Debug.LogError("[Menu] SceneFlowManager.instance 為 null，無法切換場景！");
            }
        }
    }

    /// <summary>
    /// 寫入本地檔案，並嘗試上傳 Firebase（若有初始化）
    /// </summary>
    /// <returns>是否成功將 JSON 寫入到本地檔案</returns>
    public bool SaveToLocalFile()
    {
        try
        {
            if (clickedAnimalSequence == null || clickedAnimalSequence.Count == 0)
            {
                Debug.Log("[Menu] selections 為空，略過寫檔");
                return false;
            }

            GameMultiAttemptData data = new GameMultiAttemptData
            {
                correctAnswers = new List<string>(clickedAnimalSequence),
                attempts = new List<GameAttempt>()
            };

            string json = JsonUtility.ToJson(data, true);

            // 確保目錄存在（避免偶發刪除）
            Directory.CreateDirectory(Path.GetDirectoryName(saveFilePath));
            File.WriteAllText(saveFilePath, json);
            Debug.Log($"[Menu] 寫入 {saveFilePath}\n{json}");

            // Firebase 上傳做防呆，避免 null 造成「保存失敗」
            var fb = FirebaseManager_Firestore.Instance;
            if (fb == null)
            {
                Debug.LogWarning("[Menu] FirebaseManager_Firestore.Instance 為 null，僅完成本地存檔");
                return true;
            }

            string testId = fb.testId;
            if (string.IsNullOrEmpty(testId))
            {
                Debug.LogWarning("[Menu] Firebase testId 為空，略過雲端上傳（已完成本地存檔）");
                return true;
            }

            string levelIndex = "8_Round0"; // 保持你原本的 key
            try
            {
                // 若 SaveLevelData 需要實際資料，請在其內部取用本地檔或調整實作；
                // 依你原本程式介面維持呼叫順序
                fb.SaveLevelData(testId, levelIndex, 0);

                byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
                var files = new Dictionary<string, byte[]> { { "記憶選擇_jsonData.json", jsonBytes } };
                fb.UploadFilesAndSaveUrls(testId, levelIndex, files);
            }
            catch (Exception exUp)
            {
                Debug.LogWarning($"[Menu] Firebase 上傳失敗（本地已存）：{exUp.Message}");
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogError("保存失敗（本地寫檔例外）：" + e);
            return false;
        }
    }
}
