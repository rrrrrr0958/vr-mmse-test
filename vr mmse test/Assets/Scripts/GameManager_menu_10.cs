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
    public static GameManagerMenu_10 instance;
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

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip clickSound;

    [Header("Save Settings (相對路徑)")]
    public string saveFolder = "Game_10";

    private readonly HashSet<string> selectedSet = new HashSet<string>();
    private readonly Dictionary<Button, Color> originalColors = new Dictionary<Button, Color>();
    private string saveFilePath;
    private bool finalizedSave = false;
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

        while (timeout > 0)
        {
            bool allFound = TryFindUI();
            if (allFound) break;

            timeout -= Time.deltaTime;
            yield return null;
        }

        if (confirmButton)
        {
            confirmButton.onClick.RemoveAllListeners();
            confirmButton.onClick.AddListener(() => { PlayClickSound(); OnConfirm(); });
            Debug.Log("[Menu] ✅ ConfirmButton 綁定成功");
        }
        else
            Debug.LogWarning("[Menu] ⚠ ConfirmButton 未找到");

        if (retryButton)
        {
            retryButton.onClick.RemoveAllListeners();
            retryButton.onClick.AddListener(() => { PlayClickSound(); OnRetry(); });
            Debug.Log("[Menu] ✅ RetryButton 綁定成功");
        }
        else
            Debug.LogWarning("[Menu] ⚠ RetryButton 未找到");
    }

    private bool TryFindUI()
    {
        bool foundAny = false;

        if (confirmButton == null)
            confirmButton = GameObject.Find("ConfirmButton")?.GetComponent<Button>();

        if (retryButton == null)
            retryButton = GameObject.Find("RetryButton")?.GetComponent<Button>();

        if (animalButtons == null || animalButtons.Count == 0)
        {
            var allBtns = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            animalButtons = new List<Button>(allBtns);
        }

        foreach (var btn in animalButtons)
            EnsureOriginalColorCached(btn);

        return (confirmButton != null && retryButton != null);
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
        Debug.Log($"[Menu] 儲存路徑：{saveFilePath}");
    }

    private void EnsureOriginalColorCached(Button btn)
    {
        if (btn && btn.image && !originalColors.ContainsKey(btn))
            originalColors[btn] = btn.image.color;
    }

    public void OnAnimalButtonClick(Button btn, string animalName)
    {
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

        SaveToLocalFile();
        if (confirmPanel)
            confirmPanel.SetActive(selectedSet.Count == 3);
    }

    public void OnConfirm()
    {
        if (confirmPanel) confirmPanel.SetActive(false);
        if (panel1) panel1.SetActive(false);
        SaveToLocalFile();
        finalizedSave = true;

        if (SceneFlowManager.instance != null)
            SceneFlowManager.instance.LoadNextScene();
    }

    public void OnRetry()
    {
        selectedSet.Clear();
        clickedAnimalSequence.Clear();

        foreach (var kv in originalColors)
            if (kv.Key && kv.Key.image) kv.Key.image.color = kv.Value;

        if (confirmPanel) confirmPanel.SetActive(false);
    }

    public void SaveToLocalFile()
    {
        try
        {
            if (clickedAnimalSequence.Count == 0)
            {
                Debug.Log("[Menu] selections 為空，略過寫檔");
                return;
            }

            GameMultiAttemptData data = new GameMultiAttemptData
            {
                correctAnswers = new List<string>(clickedAnimalSequence),
                attempts = new List<GameAttempt>()
            };

            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(saveFilePath, json);
            Debug.Log($"[Menu] 寫入 {saveFilePath}\n{json}");
        }
        catch (Exception e)
        {
            Debug.LogError("保存失敗：" + e.Message);
        }
    }
}
