using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class LevelCompleteButton : MonoBehaviour
{
    [Tooltip("這個按鈕對應的拼圖索引 (0-based)。")]
    public int pieceIndex = 0;

    [Tooltip("測試用：按一次收下一片（單場景測試）。")]
    public bool useNextMode = false;

    [Tooltip("可選：直接指定 UI Manager (非必要，如果沒有指定則由 PuzzleManager event 驅動 UI)。")]
    public PuzzleUIManager uiManager = null;

    Button _btn;

    private void Awake()
    {
        _btn = GetComponent<Button>();
    }

    private void OnEnable()
    {
        _btn.onClick.AddListener(OnClicked);
    }

    private void OnDisable()
    {
        _btn.onClick.RemoveListener(OnClicked);
    }

    private void OnClicked()
    {
        if (PuzzleManager.Instance == null)
        {
            Debug.LogError("[LevelCompleteButton] PuzzleManager 不存在，請先放置 PuzzleManager 到場景中。");
            return;
        }

        bool got = false;
        int indexGot = -1;
        if (useNextMode)
        {
            got = PuzzleManager.Instance.TryCollectNext();
            if (got) indexGot = Mathf.Max(0, PuzzleManager.Instance.CollectedCount - 1);
        }
        else
        {
            got = PuzzleManager.Instance.TryCollectPiece(pieceIndex);
            if (got) indexGot = pieceIndex;
        }

        if (!got)
        {
            Debug.Log($"[LevelCompleteButton] 沒有新增拼圖 (index={pieceIndex})。可能已收集過。");
            return;
        }

        // 選用：若你在 Inspector 指定了 uiManager，直接呼叫它顯示（方便測試）
        if (uiManager != null && indexGot >= 0)
        {
            uiManager.ShowReward(indexGot);
        }
        // 正常情況下：PuzzleManager 會觸發 OnPieceCollected，PuzzleUIManager 會自動顯示
    }
}
