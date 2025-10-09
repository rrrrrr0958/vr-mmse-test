using UnityEngine;

/// <summary>
/// 掛在攤販物件上，玩家進入觸發範圍後會出題。
/// 請確保：
/// 1) 玩家 XR Origin 有 Tag = "Player"
/// 2) 攤販物件上有 Collider，且 IsTrigger = true
/// 3) stallId 與 LocationDB 中的 viewpointName 一致
/// </summary>
[RequireComponent(typeof(Collider))]
public class StallTrigger : MonoBehaviour
{
    [Tooltip("對應 LocationDB.entries 中的 viewpointName")]
    public string stallId;

    [Tooltip("進入後要鎖移動的秒數（0 = 直到答題結束）")]
    public float lockSeconds = 0f;

    PlayerRigMover _mover;

    void Awake()
    {
        // 保證 Collider 為 Trigger
        var col = GetComponent<Collider>();
        if (col && !col.isTrigger) col.isTrigger = true;

        _mover = FindFirstObjectByType<PlayerRigMover>(FindObjectsInactive.Exclude);
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;

        Debug.Log($"[StallTrigger] Player entered stall trigger: {stallId}");

        if (_mover) _mover.allowMove = false;

        var session = FindFirstObjectByType<SessionController>(FindObjectsInactive.Exclude);
        if (session)
        {
            session.StartQuizByStallId(stallId);
        }
        else
        {
            Debug.LogWarning("[StallTrigger] 找不到 SessionController，無法出題。");
        }

        if (lockSeconds > 0 && _mover)
            Invoke(nameof(UnlockMove), lockSeconds);
    }

    void UnlockMove()
    {
        if (_mover) _mover.allowMove = true;
    }
}
