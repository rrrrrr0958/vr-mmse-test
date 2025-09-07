using UnityEngine;

/// <summary>
/// 可掛在「一組世界面板」上，透過 Inspector 綁 FloorNavNode 與 PlayerRigMover，
/// 讓四顆按鈕（直走/左/右/上下樓）呼叫這裡的 public 方法。
/// （若你已用 NavAction 逐顆按鈕設定，可以不使用本腳本）
/// </summary>
public class NavPanel : MonoBehaviour {
    public PlayerRigMover mover;
    public FloorNavNode node;

    void Reset() {
        if (!mover) mover = Object.FindFirstObjectByType<PlayerRigMover>();
        if (!node) node = Object.FindFirstObjectByType<FloorNavNode>();
    }

    public void GoForward() { if (node && node.forwardVP) mover?.GoTo(node.forwardVP); }
    public void GoLeft()    { if (node && node.leftVP)    mover?.GoTo(node.leftVP); }
    public void GoRight()   { if (node && node.rightVP)   mover?.GoTo(node.rightVP); }

    public void GoUp() {
        if (node && !string.IsNullOrEmpty(node.upScene))
            SceneLoader.Load(node.upScene);
    }
    public void GoDown() {
        if (node && !string.IsNullOrEmpty(node.downScene))
            SceneLoader.Load(node.downScene);
    }
}
