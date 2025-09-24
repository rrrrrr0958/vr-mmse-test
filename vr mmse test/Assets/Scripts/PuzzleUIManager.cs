using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class PuzzleUIManager : MonoBehaviour
{
    [Header("UI 元件")]
    public GameObject rewardPanel;    // 指向 RewardPanel root (預設 inactive)
    public Image baseImageGray;       // 灰階底圖 (optional)
    public Image baseImageColor;      // 彩色底圖 (alpha 0 -> 完成時漸顯)
    public Image[] pieceImages;       // 指派 11 個 Piece Image (Inspector)
    public Button nextButton;

    [Header("動畫與位置")]
    public float appearDuration = 0.35f;
    public float distanceFromCamera = 2.0f;
    public string nextSceneName = "";
    

    private void Awake()
    {
        if (rewardPanel == null) Debug.LogError("PuzzleUIManager: rewardPanel 未指定!");
        rewardPanel.SetActive(false);

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
        Debug.Log($"[PuzzleUIManager] OnPieceCollected {index}");
        ShowReward(index);
    }

    public void ShowReward(int index)
    {
        // 1) 先更新所有已收集片的顯示 (保險)
        if (pieceImages != null)
        {
            for (int i = 0; i < pieceImages.Length; i++)
            {
                var img = pieceImages[i];
                if (img == null) continue;
                bool has = PuzzleManager.Instance != null && PuzzleManager.Instance.IsPieceCollected(i);
                img.gameObject.SetActive(has);
                // 確保顯示的時候 alpha = 1 並 scale = 1（防止殘留）
                if (has)
                {
                    Color col = img.color;
                    col.a = 1f;
                    img.color = col;
                    img.transform.localScale = Vector3.one;
                }
            }
        }

        // 2) 單片動畫 (如果 index 有效)
        if (index >= 0 && index < pieceImages.Length && pieceImages[index] != null)
        {
            // 若尚未 active -> 做動畫
            pieceImages[index].gameObject.SetActive(true);
            StartCoroutine(AnimatePieceIn(pieceImages[index]));
            // 確保該片在所有子物件最上層(避免被底圖蓋住)
            pieceImages[index].transform.SetAsLastSibling();
        }

        // 3) 顯示 Panel 並把它放到玩家前面
        rewardPanel.SetActive(true);
        PositionPanelInFrontOfCamera();

        // 4) 若已收集完 -> 完成流程
        if (PuzzleManager.Instance != null && PuzzleManager.Instance.IsComplete())
        {
            StartCoroutine(CompleteSequence());
        }
    }

    IEnumerator AnimatePieceIn(Image img)
    {
        float t = 0f;
        float dur = Mathf.Max(0.01f, appearDuration);

        Color targetColor = img.color;
        Color startColor = targetColor; startColor.a = 0f;
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
        // 漸顯彩色底圖
        if (baseImageColor != null)
        {
            float dur = 0.9f;
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
        // 這裡可以加音效或粒子系統
        yield return null;
    }

    private void PositionPanelInFrontOfCamera()
    {
        var cam = Camera.main;
        if (cam == null) return;
        Transform camT = cam.transform;
        rewardPanel.transform.position = camT.position + camT.forward * distanceFromCamera;
        rewardPanel.transform.rotation = Quaternion.LookRotation(rewardPanel.transform.position - camT.position); // 面對玩家
    }

    // --- debug helper (在 Inspector 呼叫)
    public void DebugLogPieces()
    {
        if (pieceImages == null) { Debug.Log("pieceImages null"); return; }
        for (int i = 0; i < pieceImages.Length; i++)
        {
            var p = pieceImages[i];
            if (p == null) Debug.Log($"piece {i} = null");
            else Debug.Log($"piece {i} active={p.gameObject.activeSelf} sprite={(p.sprite==null? "null": p.sprite.name)} colorAlpha={p.color.a} scale={p.transform.localScale}");
        }
    }

    private void OnNextClicked()
    {
        rewardPanel.SetActive(false);
        if (!string.IsNullOrEmpty(nextSceneName))
        {
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
