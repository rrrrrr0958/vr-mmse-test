using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 掛在世界按鈕上，提供兩種行為：同場景瞬移 / 跨場景載入
/// </summary>
public class NavAction : MonoBehaviour {
    [Header("同場景移動")]
    public PlayerRigMover mover;
    public Transform targetVP;

    [Header("跨場景")]
    public string sceneName;

    /// <summary>Button OnClick：同場景移動（瞬移）</summary>
    public void GoToViewpoint() {
        if (!mover) mover = Object.FindFirstObjectByType<PlayerRigMover>();
        if (mover && targetVP) mover.GoTo(targetVP);
        else Debug.LogWarning($"[NavAction] mover/targetVP 未設定在 {name}");
    }

    /// <summary>Button OnClick：載入場景</summary>
    public void LoadScene() {
        if (string.IsNullOrEmpty(sceneName)) return;
        SceneManager.LoadScene(sceneName);
    }
}
