using UnityEngine;
using UnityEngine.SceneManagement;

public class NavAction : MonoBehaviour {
    [Header("同場景移動")]
    public PlayerRigMover mover;
    public Transform targetVP;

    [Header("跨場景")]
    public string sceneName;

    void Awake() {
        // 自動補綁 mover（避免忘記拖）
        if (!mover) mover = UnityEngine.Object.FindFirstObjectByType<PlayerRigMover>();
    }

    public void GoToViewpoint() {
        if (!mover) {
            Debug.LogError($"[NavAction] mover 為空（{name}）。請在場景內放一個掛 PlayerRigMover 的物件，或手動指定。");
            return;
        }
        if (!targetVP) {
            Debug.LogError($"[NavAction] targetVP 為空（{name}）。請把目標 Viewpoint 拖進來。");
            return;
        }

        Debug.Log($"[NavAction] GoToViewpoint from '{name}' → '{targetVP.name}'");
        mover.GoTo(targetVP);
    }

    public void LoadScene() {
        if (string.IsNullOrEmpty(sceneName)) {
            Debug.LogError($"[NavAction] sceneName 為空（{name}）。");
            return;
        }
        Debug.Log($"[NavAction] LoadScene '{sceneName}' (triggered by {name})");
        SceneManager.LoadScene(sceneName);
    }

    // 方便在 Inspector 的三點選單測試
    [ContextMenu("TEST: GoToViewpoint (Editor)")]
    void _TestGo() {
        if (!Application.isPlaying) { Debug.Log("Enter Play Mode to test."); return; }
        GoToViewpoint();
    }
}
