using UnityEngine;

public class NavAction : MonoBehaviour
{
    [Header("Same-scene teleport")]
    public PlayerRigMover mover;   // 拖 PlayerRig（上面有 PlayerRigMover）
    public Transform targetVP;     // 拖目標 Viewpoint（例如 VP_Bakery）

    [Header("Cross-scene load")]
    public string sceneName;       // 要切換的場景名（例如 "F2"）

    public void GoToViewpoint()
    {
        if (mover != null && targetVP != null) mover.MoveTo(targetVP);
    }

    public void LoadScene()
    {
        if (!string.IsNullOrEmpty(sceneName)) SceneLoader.Load(sceneName);
    }
}
