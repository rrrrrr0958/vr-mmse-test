using UnityEngine;

public class FloorNavNode : MonoBehaviour
{
    [Header("Meta")]
    public string floorLabel = "F1";   // F1/F2/F3

    [Header("Same-scene viewpoints")]
    public Transform forwardVP;        // 直走
    public Transform leftVP;           // 左轉
    public Transform rightVP;          // 右轉

    [Header("Cross-scene (stairs)")]
    public string upScene;             // F2/F3
    public string downScene;           // F1/F2
}
