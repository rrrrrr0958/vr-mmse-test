// LevelCompleteButton.cs
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class LevelCompleteButton : MonoBehaviour
{
    public int pieceIndex = 0; // 該按鈕對應第幾片 (0-based)
    public bool useNextMode = false; // 若 true -> 按一次收集下一片（單場景測試用）
    private Button btn;

    private void Start()
    {
        btn = GetComponent<Button>();
        btn.onClick.AddListener(OnClicked);
    }

    private void OnClicked()
    {
        if (PuzzleManager.Instance == null)
        {
            Debug.LogWarning("No PuzzleManager instance in scene.");
            return;
        }

        bool got = false;
        if (useNextMode)
        {
            got = PuzzleManager.Instance.TryCollectNext();
            if (!got)
            {
                Debug.Log("All pieces already collected.");
            }
        }
        else
        {
            got = PuzzleManager.Instance.TryCollectPiece(pieceIndex);
            if (!got)
            {
                Debug.Log($"Piece {pieceIndex} already collected or invalid.");
            }
        }

        // 嘗試立即讓 UI 顯示（PuzzleManager 會觸發事件，UI 會自動顯示）。
        // 但為了保險，如果 UI 尚未存在，我們也可嘗試直接尋找並呼叫 ShowReward
        var ui = FindObjectOfType<PuzzleUIManager>();
        if (ui != null)
        {
            if (useNextMode)
            {
                // 找到剛收集到的 index (用 CollectedCount-1)
                int lastIndex = Mathf.Max(0, PuzzleManager.Instance.CollectedCount - 1);
                ui.ShowReward(lastIndex);
            }
            else
            {
                ui.ShowReward(pieceIndex);
            }
        }
    }
}
