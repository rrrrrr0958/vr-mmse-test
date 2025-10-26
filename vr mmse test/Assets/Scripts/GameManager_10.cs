using UnityEngine;
using System.Collections.Generic;
using System.IO;
using UnityEngine.UI;
using TMPro;
using System;
using UnityEngine.SceneManagement;
using System.Collections;
using System.Linq;

[DefaultExecutionOrder(-50)]
public class GameManager_10 : MonoBehaviour
{
    private FirebaseManager_Firestore FirebaseManager;
    private string levelID;

    public static GameManager_10 instance;
    public List<string> clickedAnimalSequence = new List<string>();

    [Header("UI References")]
    public GameObject panel1;
    public GameObject confirmPanel;
    public TextMeshProUGUI resultText;

    [Header("Confirm UI Buttons (not used)")]
    public Button confirmButton;
    public Button retryButton;

    [Header("Animal Buttons (可留空)")]
    public List<Button> animalButtons = new List<Button>();

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip clickSound;

    [Header("Save Settings (相對路徑)")]
    public string saveFolder = "Game_10";

    private readonly HashSet<string> selectedSet = new HashSet<string>();
    private readonly Dictionary<Button, Color> originalColors = new Dictionary<Button, Color>();
    private string saveFilePath;
    private List<string> correctAnswers = new List<string>();
    private float startTime;
    private bool finalizedSave = false;

    // 新增：避免重複觸發跳關
    private bool hasProgressed = false;

    void Awake()
    {
        // ✅ 讓舊的 instance 自動銷毀
        if (instance != null && instance != this)
        {
            Debug.Log("[GM] 🔁 舊的 GameManager_10 已存在，銷毀舊版本");
            Destroy(instance.gameObject);
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        SetupRelativeSavePath();
        clickedAnimalSequence.Clear();
        selectedSet.Clear();

        // 確保事件只註冊一次
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void Start()
    {
        int round = GameSessionManager.Instance.GetNextRoundNumber("SampleScene_11");
        levelID = $"{"8"}_Round{round}";
        Debug.Log($"📊 Level Session ID: {levelID}");
        
        startTime = Time.time;
        StartCoroutine(WaitAndBindUI());
        LoadCorrectAnswersFromFile();
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        Debug.Log($"[GM] 場景載入完成：{scene.name} → 重新綁定 UI");
        StartCoroutine(WaitAndBindUI());
    }

    private IEnumerator WaitAndBindUI()
    {
        float timeout = 5f;

        // 只等待基本 UI 與文字（不再尋找/綁定 Confirm/Retry）
        while (timeout > 0)
        {
            panel1 = panel1 ?? GameObject.Find("Panel1");
            confirmPanel = confirmPanel ?? GameObject.Find("ConfirmPanel");
            resultText = resultText ?? GameObject.Find("ResultText")?.GetComponent<TextMeshProUGUI>();

            // 若動物按鈕列表未填，收集一次
            if (animalButtons == null || animalButtons.Count == 0)
            {
                var allBtns = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
                animalButtons = new List<Button>(allBtns);
            }

            if (animalButtons.Count > 0) break;

            timeout -= Time.deltaTime;
            yield return null;
        }

        // 保險重設 UI 狀態
        if (confirmPanel) confirmPanel.SetActive(false);
        if (resultText) resultText.gameObject.SetActive(false);

        foreach (var btn in animalButtons)
            EnsureOriginalColorCached(btn);
    }

    private void PlayClickSound()
    {
        if (audioSource && clickSound)
            audioSource.PlayOneShot(clickSound);
    }

    private void SetupRelativeSavePath()
    {
        try
        {
            string folderPath = Path.Combine(Application.dataPath, "Scripts", saveFolder);
            Directory.CreateDirectory(folderPath);
            saveFilePath = Path.Combine(folderPath, "gamedata.json");
        }
        catch
        {
            saveFilePath = Path.Combine(Application.persistentDataPath, "gamedata.json");
        }
        Debug.Log($"[GM] 儲存路徑：{saveFilePath}");
    }

    private void EnsureOriginalColorCached(Button btn)
    {
        if (btn && btn.image && !originalColors.ContainsKey(btn))
            originalColors[btn] = btn.image.color;
    }

    private void LoadCorrectAnswersFromFile()
    {
        try
        {
            if (!File.Exists(saveFilePath))
            {
                Debug.LogWarning("[GM] 沒有 gamedata.json，使用預設空答案");
                return;
            }

            string json = File.ReadAllText(saveFilePath);
            Debug.Log($"[GM] 📄 載入 JSON 原文：\n{json}");

            var data = JsonUtility.FromJson<GameMultiAttemptData>(json);

            if (data == null)
            {
                Debug.LogError("[GM] ❌ 反序列化失敗，JsonUtility 回傳 null");
                return;
            }

            if (data.correctAnswers == null)
            {
                Debug.LogError("[GM] ❌ data.correctAnswers 是 null");
                return;
            }

            if (data.correctAnswers.Count > 0)
            {
                correctAnswers = data.correctAnswers;
                Debug.Log($"✅ 載入正確答案：{string.Join("、", correctAnswers)}");
            }
            else
            {
                Debug.LogWarning("[GM] ⚠ 正確答案為空，請確認 JSON 檔內有 'correctAnswers'");
            }
        }
        catch (Exception e)
        {
            Debug.LogError("讀取正確答案失敗：" + e.Message);
        }
    }

    public void OnAnimalButtonClick(Button btn, string animalName)
    {
        if (hasProgressed) return; // 已跳關就不再處理

        PlayClickSound();

        if (string.IsNullOrEmpty(animalName))
            animalName = btn ? btn.name : "";

        EnsureOriginalColorCached(btn);

        if (selectedSet.Contains(animalName))
        {
            selectedSet.Remove(animalName);
            clickedAnimalSequence.Remove(animalName);
            if (btn && btn.image && originalColors.TryGetValue(btn, out var oc))
                btn.image.color = oc;
        }
        else
        {
            if (selectedSet.Count >= 3) return;
            selectedSet.Add(animalName);
            clickedAnimalSequence.Add(animalName);
            if (btn && btn.image)
            {
                var c = btn.image.color;
                c.a = 0.5f;
                btn.image.color = c;
            }
        }

        // ✅ 點滿 3 個：存檔 →（可選）存軌跡 → 跳下一關
        if (selectedSet.Count == 3 && !hasProgressed)
        {
            hasProgressed = true;

            SaveAttemptResult();
            finalizedSave = true;

            VRTracker tracker = FindFirstObjectByType<VRTracker>();
            if (tracker != null)
            {
                tracker.SaveTrajectoryToCsv();
            }
            else
            {
                Debug.LogWarning("[GM] 沒有找到 VRTracker 物件，無法保存軌跡。");
            }

            if (SceneFlowManager.instance != null)
                SceneFlowManager.instance.LoadNextScene();
            else
                Debug.LogError("[GM] SceneFlowManager.instance 為 null，無法切換場景！");
        }
    }

    // ⛔️ 依需求：不需要 OnConfirm / OnRetry，已移除其功能與綁定
    // （保留欄位但不使用，避免 Prefab 失參考）

    private void SaveAttemptResult()
    {
        try
        {
            if (clickedAnimalSequence.Count == 0)
            {
                Debug.Log("[GM] selections 為空，略過寫檔");
                return;
            }

            GameMultiAttemptData data;
            if (File.Exists(saveFilePath))
            {
                string json = File.ReadAllText(saveFilePath);
                data = JsonUtility.FromJson<GameMultiAttemptData>(json) ?? new GameMultiAttemptData();
            }
            else
            {
                data = new GameMultiAttemptData();
            }

            if (data.attempts == null) data.attempts = new List<GameAttempt>();

            // 清理字串
            List<string> cleanCorrect = new List<string>();
            foreach (var s in correctAnswers)
                if (!string.IsNullOrWhiteSpace(s))
                    cleanCorrect.Add(s.Trim().Replace("　", ""));

            List<string> cleanSelected = new List<string>();
            foreach (var s in clickedAnimalSequence)
                if (!string.IsNullOrWhiteSpace(s))
                    cleanSelected.Add(s.Trim().Replace("　", ""));

            var correctSet = new HashSet<string>(cleanCorrect);
            int correctCount = correctSet.Intersect(cleanSelected).Count();
            float accuracy = correctSet.Count > 0 ? (float)correctCount / correctSet.Count : 0f;

            float timeUsed = Time.time - startTime;
            int round = data.attempts.Count + 1;

            var attempt = new GameAttempt
            {
                round = round,
                selected = new List<string>(clickedAnimalSequence),
                correctCount = correctCount,
                timeUsed = timeUsed
            };
            data.attempts.Add(attempt);

            string updatedJson = JsonUtility.ToJson(data, true);

            // 本地存檔（保留你的路徑策略）
            Directory.CreateDirectory(Path.GetDirectoryName(saveFilePath));
            File.WriteAllText(saveFilePath, updatedJson);

            // ✅ Firebase 防呆：Instance/testId 檢查，上傳失敗不擋流程
            var fb = FirebaseManager_Firestore.Instance;
            if (fb == null)
            {
                Debug.LogWarning("[GM] FirebaseManager_Firestore.Instance 為 null，僅完成本地存檔");
            }
            else
            {
                try
                {
                    if (string.IsNullOrEmpty(fb.testId))
                    {
                        Debug.LogWarning("[GM] Firebase testId 為空，略過雲端上傳（已完成本地存檔）");
                    }
                    else
                    {
                        // 分段 try-catch，避免互相影響
                        try
                        {
                            fb.totalScore = fb.totalScore + correctCount;
                        }
                        catch (Exception scoreEx)
                        {
                            Debug.LogWarning($"[GM] 更新 totalScore 失敗：{scoreEx.Message}");
                        }

                        try
                        {
                            fb.SaveLevelData(fb.testId, levelID, correctCount);
                        }
                        catch (Exception saveEx)
                        {
                            Debug.LogWarning($"[GM] SaveLevelData 失敗：{saveEx.Message}");
                        }

                        try
                        {
                            byte[] updatedJsonBytes = System.Text.Encoding.UTF8.GetBytes(updatedJson);
                            var files = new Dictionary<string, byte[]> { { "記憶選擇_jsonData.json", updatedJsonBytes } };
                            fb.UploadFilesAndSaveUrls(fb.testId, levelID, files);
                        }
                        catch (Exception uploadEx)
                        {
                            Debug.LogWarning($"[GM] UploadFilesAndSaveUrls 失敗：{uploadEx.Message}");
                        }
                    }
                }
                catch (Exception outerEx)
                {
                    Debug.LogWarning($"[GM] Firebase 上傳流程例外：{outerEx.Message}");
                }
            }

            Debug.Log($"[GM] ✅ 已保存第 {round} 次作答");
            Debug.Log($"正確答案：{string.Join("、", cleanCorrect)}");
            Debug.Log($"玩家選擇：{string.Join("、", cleanSelected)}");
            Debug.Log($"答對 {correctCount}/{cleanCorrect.Count} 題，正確率：{accuracy * 100f:F1}%");
        }
        catch (Exception e)
        {
            Debug.LogError("保存作答失敗：" + e.Message);
        }
    }
}
