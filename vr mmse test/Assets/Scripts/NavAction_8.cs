using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 輕量事件轉發器（保留相容性用）。
/// - 同場景移動 => 呼叫 PlayerRigMover.GoTo(targetVP)
/// - 跨場景切換 => 單純 LoadScene(sceneName)，後續流程交由 SessionController 處理
/// 建議：新專案請優先使用 NavPanel / FloorNavNode；本檔僅供舊 UI 綁定相容。
/// </summary>
[DisallowMultipleComponent]
public class NavAction : MonoBehaviour
{
    [Header("同場景移動")]
    [Tooltip("玩家 Rig（含 VR 頭高補償/淡入淡出）")]
    public PlayerRigMover mover;
    [Tooltip("要瞬移到的目標 Viewpoint")]
    public Transform targetVP;

    [Header("跨場景")]
    [Tooltip("要載入的場景名稱（例如 F2 / F3）。切換後由 SessionController 自動出題。")]
    public string sceneName;

    void Awake()
    {
        // 自動補綁 mover（避免忘記拖）
        if (!mover) mover = FindFirstObjectByType<PlayerRigMover>(FindObjectsInactive.Exclude);
    }

    /// <summary>同場景瞬移到 targetVP。</summary>
    public void GoToViewpoint()
    {
        if (!mover)
        {
            Debug.LogWarning($"[NavAction] mover 未指派（{name}），嘗試自動尋找。");
            mover = FindFirstObjectByType<PlayerRigMover>(FindObjectsInactive.Exclude);
            if (!mover)
            {
                Debug.LogError($"[NavAction] 場景中找不到 PlayerRigMover，無法 GoToViewpoint。");
                return;
            }
        }
        if (!targetVP)
        {
            Debug.LogError($"[NavAction] targetVP 未指派（{name}）。");
            return;
        }

        Debug.Log($"[NavAction] GoToViewpoint: {targetVP.name}");
        mover.GoTo(targetVP);
    }

    /// <summary>切換到指定場景。後續由 SessionController 在 sceneLoaded 後自動出題。</summary>
    public void LoadScene()
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogError($"[NavAction] sceneName 為空（{name}）。");
            return;
        }

        Debug.Log($"[NavAction] LoadScene → {sceneName}");
        // 不在此處做出題/鎖移動；交由 SessionController 的 sceneLoaded 勾子統一處理
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

#if UNITY_EDITOR
    // 方便在 Inspector 的三點選單測試
    [ContextMenu("TEST: GoToViewpoint (Play Mode)")]
    void _TestGo()
    {
        if (!Application.isPlaying) { Debug.Log("Enter Play Mode to test."); return; }
        GoToViewpoint();
    }
#endif
}
