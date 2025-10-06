using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.XR.CoreUtils;   
/// <summary>
/// 管同場景 viewpoint 切換 & 上下樓（切場景）。
/// 保持與 NavPanel 既有的 GoXXX() 介面不變，內部改成呼叫 TransitionManager。
/// </summary>
public class FloorNavNode : MonoBehaviour
{
    [Header("同場景 Viewpoints")]
    public Transform vpForward;
    public Transform vpLeft;
    public Transform vpRight;

    [Header("上下樓對應場景名（需加入 Build Settings）")]
    public string sceneUp;     // 例如 "F2"
    public string sceneDown;   // 例如 "F1"

    [Header("轉場參數")]
    [Tooltip("左右/直走時，旋轉/位移所花時間（秒）")]
    public float rotateMoveDuration = 0.6f;
    [Tooltip("黑幕淡出/淡入（上下樓切場景）所花時間（秒）")]
    public float fadeOut = 0.4f, fadeIn = 0.4f;

    void Awake()
    {
        // 可選：若場景中還沒有 TransitionManager，自動建立一個（也可以手動放好）
        if (TransitionManager.I == null)
        {
            var go = new GameObject("[TransitionManager_Auto]");
            go.AddComponent<TransitionManager>();
        }

        // 自動掛 XR Origin（如果尚未指定）
        if (TransitionManager.I.xrOrigin == null)
        {
            var xr = FindFirstObjectByType<XROrigin>(FindObjectsInactive.Exclude);
            if (xr) TransitionManager.I.xrOrigin = xr.transform;
            else
            {
                // 如果你的 XR Origin 物件名不同，以下僅示意
                var xrRig = GameObject.Find("XR Origin (XR Rig)");
                if (xrRig) TransitionManager.I.xrOrigin = xrRig.transform;
            }
        }
    }

    // ========= NavPanel 既有介面（無參數、可直接綁 Button） =========
    public void GoForward() => _ = GoToViewpoint(vpForward);
    public void GoLeft()    => _ = GoToViewpoint(vpLeft);
    public void GoRight()   => _ = GoToViewpoint(vpRight);

    public void GoUp()      => _ = LoadFloor(sceneUp);
    public void GoDown()    => _ = LoadFloor(sceneDown);

    // ========= 內部實作 =========
    async Task GoToViewpoint(Transform target)
    {
        if (target == null)
        {
            Debug.LogWarning("[FloorNavNode] 目標 Viewpoint 未指定。");
            return;
        }

        await TransitionManager.I.RotateMoveTo(target, rotateMoveDuration);

        // 到達後 → 通知 SessionController（強型別呼叫，比 SendMessage 穩）
        var session = FindFirstObjectByType<SessionController>(FindObjectsInactive.Exclude);
        if (session != null)
        {
            session.OnArrivedAtViewpoint(target);
        }
        else
        {
            Debug.LogWarning("[FloorNavNode] 找不到 SessionController，無法出題/切換 UI。");
        }
    }

    async Task LoadFloor(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogWarning("[FloorNavNode] 場景名稱未指定。");
            return;
        }
        await TransitionManager.I.FadeSceneLoad(sceneName, fadeOut, fadeIn);
    }
}
