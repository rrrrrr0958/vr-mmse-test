using UnityEngine;

/// <summary>
/// 每層樓的導航節點資料（Inspector 填值即可）
/// </summary>
public class FloorNavNode : MonoBehaviour {
    [Header("標籤")]
    public string floorLabel = "F1";

    [Header("本層 Viewpoints")]
    public Transform forwardVP;
    public Transform leftVP;
    public Transform rightVP;

    [Header("跨場景")]
    public string upScene;    // 例如 "F2"
    public string downScene;  // 例如 "B1"
}
