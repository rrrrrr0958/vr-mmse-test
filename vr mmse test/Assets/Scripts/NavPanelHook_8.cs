// NavPanelHook.cs —— 掛在你的 NavPanel_8 上，拖入目標 Viewpoint / 樓層名
using UnityEngine;
using UnityEngine.UI;
using System.Threading.Tasks;

public class NavPanelHook : MonoBehaviour
{
    public Button ButtonLeft, ButtonRight, ButtonUp, ButtonDown, ButtonForward;
    public Transform ViewLeft, ViewRight, ViewForward;   // 指到場景裡各攤位的 viewpoint
    public string NextFloorScene;                        // 例如 "F2"
    public string PrevFloorScene;                        // 例如 "F1"

    void Awake()
    {
        if (ButtonLeft)   ButtonLeft.onClick.AddListener(async () => await TransitionManager.I.RotateMoveTo(ViewLeft,   0.6f));
        if (ButtonRight)  ButtonRight.onClick.AddListener(async () => await TransitionManager.I.RotateMoveTo(ViewRight,  0.6f));
        if (ButtonForward)ButtonForward.onClick.AddListener(async () => await TransitionManager.I.RotateMoveTo(ViewForward,0.6f));

        if (ButtonUp && !string.IsNullOrEmpty(NextFloorScene))
            ButtonUp.onClick.AddListener(async () => await TransitionManager.I.FadeSceneLoad(NextFloorScene, 0.4f, 0.4f));

        if (ButtonDown && !string.IsNullOrEmpty(PrevFloorScene))
            ButtonDown.onClick.AddListener(async () => await TransitionManager.I.FadeSceneLoad(PrevFloorScene, 0.4f, 0.4f));
    }
}
