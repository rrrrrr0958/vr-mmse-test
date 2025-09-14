// PuzzleUIManager.cs
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PuzzleUIManager : MonoBehaviour
{
    [Header("UI 元件")]
    public GameObject rewardPanel;    // RewardPanel (整個 Panel)
    public Image baseImageGray;       // 灰階底圖 (可以選填)
    public Image baseImageColor;      // 彩色底圖（alpha 0 -> 完成時漸顯）
    public Image[] pieceImages;       // 11 個拼圖 Image（在 Inspector 指派）
    public Button nextButton;

    [Header("動畫/位置設定")]
    public float appearDuration = 0.4f;
    public float distanceFromCamera = 2.0f;
    public string nextSceneName = ""; // 若空字串 -> 只關閉 Panel

    private void Awake()
    {
        if (rewardPanel == null) Debug.LogError("PuzzleUIManager: rewardPanel 未指定");
        rewardPanel.SetActive(false);

        // 一開始把所有片隱藏（或以 collected 狀態為準）
        if (pieceImages != null)
        {
            foreach (var img in pieceImages)
            {
                if (img != null) img.gameObject.SetActive(false);
            }
        }
        if (baseImageColor != null)
        {
            var c = baseImageColor.color;
            c.a = 0f;
            baseImageColor.color = c;
        }
    }

    private void OnEnable()
    {
        if (PuzzleManager.Instance != null)
            PuzzleManager.Instance.OnPieceCollected += OnPieceCollected;
    }

    private void OnDisable()
    {
        if (PuzzleManager.Instance != null)
            PuzzleManager.Instance.OnPieceCollected -= OnPieceCollected;
    }

    private void Start()
    {
        if (nextButton != null)
            nextButton.onClick.AddListener(OnNextClicked);
    }

    private void OnPieceCollected(int index)
    {
        // 當 PuzzleManager 收到新片時，顯示 Panel 並顯示該片
        ShowReward(index);
    }

    // 對外呼叫也可直接傳 index
    public void ShowReward(int index)
    {
        // 更新所有已收集片的顯示（保險）
        if (pieceImages != null)
        {
            for (int i = 0; i < pieceImages.Length; i++)
            {
                if (pieceImages[i] == null) continue;
                bool has = PuzzleManager.Instance != null && PuzzleManager.Instance.IsPieceCollected(i);
                pieceImages[i].gameObject.SetActive(has);
            }
        }

        // 若 index 有效，做該片的入場動畫
        if (index >= 0 && index < pieceImages.Length && pieceImages[index] != null)
        {
            // 若尚未 active (剛被收集) -> 做動畫
            pieceImages[index].gameObject.SetActive(true);
            StartCoroutine(AnimatePieceIn(pieceImages[index]));
        }

        // 顯示 Panel（並把 Panel 移到玩家視線前）
        rewardPanel.SetActive(true);
        PositionPanelInFrontOfCamera();

        // 若已收集完畢 -> 觸發完成流程
        if (PuzzleManager.Instance != null && PuzzleManager.Instance.IsComplete())
        {
            StartCoroutine(CompleteSequence());
        }
    }

    IEnumerator AnimatePieceIn(Image img)
    {
        // 預設：從小 + 透明 -> 放大 + 不透明
        float t = 0f;
        float dur = Mathf.Max(0.01f, appearDuration);

        Color targetColor = img.color;
        Color startColor = targetColor;
        startColor.a = 0f;
        img.color = startColor;

        Vector3 startScale = Vector3.one * 0.6f;
        Vector3 endScale = Vector3.one;

        img.transform.localScale = startScale;

        while (t < dur)
        {
            t += Time.deltaTime;
            float n = Mathf.Clamp01(t / dur);
            float ease = Mathf.SmoothStep(0f, 1f, n);
            img.color = new Color(targetColor.r, targetColor.g, targetColor.b, ease);
            img.transform.localScale = Vector3.Lerp(startScale, endScale, ease);
            yield return null;
        }
        img.color = targetColor;
        img.transform.localScale = endScale;
    }

    IEnumerator CompleteSequence()
    {
        // 例如：把 baseImageColor 漸顯 (彩色圖)，再播放粒子或音效
        if (baseImageColor != null)
        {
            float dur = 0.8f;
            float t = 0f;
            Color c = baseImageColor.color;
            while (t < dur)
            {
                t += Time.deltaTime;
                float n = Mathf.Clamp01(t / dur);
                baseImageColor.color = new Color(c.r, c.g, c.b, n);
                yield return null;
            }
            baseImageColor.color = new Color(c.r, c.g, c.b, 1f);
        }

        // （可在此處加音效或粒子系統）
        yield return null;
    }

    private void PositionPanelInFrontOfCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Transform camT = cam.transform;
        rewardPanel.transform.position = camT.position + camT.forward * distanceFromCamera;
        rewardPanel.transform.LookAt(camT.position);
        rewardPanel.transform.Rotate(0f, 180f, 0f); // 面向玩家
    }

    private void OnNextClicked()
    {
        rewardPanel.SetActive(false);

        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
        // 否則僅關閉 Panel（在單場景測試常用）
    }
}
