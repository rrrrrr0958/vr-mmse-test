using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 世界空間導覽面板：把 UI 按鈕事件綁到這支腳本的公開方法即可。
/// 內部不做業務邏輯，全部委派給 FloorNavNode（瞬移/上下樓）。
/// - GoForward / GoLeft / GoRight：委派到 FloorNavNode 對應方法
/// - GoUp / GoDown：委派到 FloorNavNode 切場景（SessionController 會在載入後自動出題）
/// </summary>
public class NavPanel : MonoBehaviour
{
    [Header("Refs (可不填，會自動尋找)")]
    public PlayerRigMover mover;   // 可為空，實際不直接使用；保留給你在 Inspector 觀察
    public FloorNavNode node;

    void Awake()
    {
        WireRefs();
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    void OnSceneLoaded(Scene s, LoadSceneMode mode)
    {
        // 跨場景後重新抓引用，避免引用到前一場景的物件
        WireRefs();
    }

    void WireRefs()
    {
        if (!node)
            node = FindFirstObjectByType<FloorNavNode>(FindObjectsInactive.Exclude);

        if (!mover)
            mover = FindFirstObjectByType<PlayerRigMover>(FindObjectsInactive.Exclude);

        if (!node)
            Debug.LogWarning("[NavPanel] 找不到 FloorNavNode，導覽按鈕將不生效。");
        if (!mover)
            Debug.LogWarning("[NavPanel] 找不到 PlayerRigMover（資訊用）。瞬移仍可由 FloorNavNode 自行尋找。");
    }

    // ===== UI 綁定用：方向瞬移 =====
    public void GoForward()  { if (!EnsureNode()) return; node.GoForward(); }
    public void GoLeft()     { if (!EnsureNode()) return; node.GoLeft(); }
    public void GoRight()    { if (!EnsureNode()) return; node.GoRight(); }

    // ===== UI 綁定用：上下樓（切場景）=====
    public void GoUp()       { if (!EnsureNode()) return; node.GoUp(); }
    public void GoDown()     { if (!EnsureNode()) return; node.GoDown(); }

    bool EnsureNode()
    {
        if (node) return true;
        WireRefs();
        return node != null;
    }

    // 讓你在 Inspector 上點擊一鍵重找引用（可選）
    [ContextMenu("Rewire Refs")]
    void RewireContextMenu() => WireRefs();

    // 仍保留 Reset 以便在首次加到物件上時自動找一次
    void Reset()
    {
        mover = FindFirstObjectByType<PlayerRigMover>(FindObjectsInactive.Exclude);
        node  = FindFirstObjectByType<FloorNavNode>(FindObjectsInactive.Exclude);
    }
}
