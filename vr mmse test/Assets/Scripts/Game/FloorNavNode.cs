using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 每層樓的導航節點與動作集：
/// - 本層三個 Viewpoint（前/左/右）：呼叫 GoForward/GoLeft/GoRight 來瞬移
/// - 跨場景（上/下樓）：呼叫 GoUp/GoDown 切換樓層
/// 可直接綁在 UI Button、世界空間按鈕或 Trigger 的 UnityEvent。
/// </summary>
public class FloorNavNode : MonoBehaviour
{
    [Header("標籤")]
    [Tooltip("顯示用樓層標籤（例如 F1/F2/F3）")]
    public string floorLabel = "F1";

    [Header("本層 Viewpoints")]
    public Transform forwardVP;
    public Transform leftVP;
    public Transform rightVP;

    [Header("跨場景")]
    [Tooltip("往上樓要載入的場景名（例如 F2）")]
    public string upScene;    // 例如 "F2"
    [Tooltip("往下樓要載入的場景名（例如 F1 或 B1）")]
    public string downScene;  // 例如 "B1"

    PlayerRigMover _mover;

    void Awake()
    {
        _mover = FindFirstObjectByType<PlayerRigMover>(FindObjectsInactive.Exclude);
        if (!_mover)
            Debug.LogWarning("[FloorNavNode] 找不到 PlayerRigMover，GoForward/Left/Right 將不會生效（請確認場景有 XR Origin 並掛上 PlayerRigMover）。");
    }

    // ============ 對外動作（給 UI/Trigger 綁定） ============
    public void GoForward() => TeleportTo(forwardVP, "forward");
    public void GoLeft()    => TeleportTo(leftVP,    "left");
    public void GoRight()   => TeleportTo(rightVP,   "right");

    public void GoUp()      => RequestFloor(upScene,   "up");
    public void GoDown()    => RequestFloor(downScene, "down");

    // ============ 內部實作 ============
    void TeleportTo(Transform vp, string dirLabel)
    {
        if (!vp)
        {
            Debug.LogWarning($"[FloorNavNode] {dirLabel}VP 未指派，無法瞬移。");
            return;
        }

        // 跨場景後引用可能失效，重新抓
        if (!_mover)
            _mover = FindFirstObjectByType<PlayerRigMover>(FindObjectsInactive.Exclude);
        if (!_mover)
        {
            Debug.LogWarning("[FloorNavNode] 場景中沒有 PlayerRigMover。");
            return;
        }

        // 若題目仍開啟，先收起；並確保解鎖 allowMove
        var session = FindFirstObjectByType<SessionController>(FindObjectsInactive.Exclude);
        if (session && session.quizPanel && session.quizPanel.gameObject.activeInHierarchy)
            session.quizPanel.Hide();

        if (!_mover.allowMove)
            Debug.Log("[FloorNavNode] allowMove=false → 自動解鎖後再瞬移。");
        _mover.allowMove = true;

        Debug.Log($"[FloorNavNode] Teleport {dirLabel} → {vp.name}");
        _mover.GoTo(vp); // 這裡應該會看到 [Mover] GoTo ... 的後續 log
    }

    void RequestFloor(string sceneName, string dir)
    {
        if (string.IsNullOrWhiteSpace(sceneName))
        {
            Debug.LogWarning($"[FloorNavNode] {dir}Scene 未設定，忽略切換。");
            return;
        }

        // 直接切場景；SessionController 會在 sceneLoaded 自動出題
        Debug.Log($"[FloorNavNode] LoadScene → {sceneName}");
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
    }

    // ============ 編輯器可視化 ============
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        if (forwardVP) { Gizmos.DrawLine(transform.position, forwardVP.position); DrawSphere(forwardVP.position); }
        Gizmos.color = Color.yellow;
        if (leftVP)    { Gizmos.DrawLine(transform.position, leftVP.position);    DrawSphere(leftVP.position); }
        Gizmos.color = Color.magenta;
        if (rightVP)   { Gizmos.DrawLine(transform.position, rightVP.position);   DrawSphere(rightVP.position); }
    }

    void DrawSphere(Vector3 pos)
    {
        const float r = 0.15f;
        Gizmos.DrawWireSphere(pos, r);
    }
}
