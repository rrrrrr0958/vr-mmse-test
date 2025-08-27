using UnityEngine;
using UnityEngine.UI;

public class NavPanel : MonoBehaviour
{
    public Button upBtn, downBtn, leftBtn, rightBtn, forwardBtn;

    PlayerRigMover _mover;
    FloorNavNode _node;

    void Start()
    {
        _mover = FindObjectOfType<PlayerRigMover>();
        _node  = FindObjectOfType<FloorNavNode>();

        SetupSceneBtn(upBtn,   _node?.upScene,   () => SceneLoader.Load(_node.upScene));
        SetupSceneBtn(downBtn, _node?.downScene, () => SceneLoader.Load(_node.downScene));

        SetupMoveBtn(forwardBtn, _node?.forwardVP);
        SetupMoveBtn(leftBtn,    _node?.leftVP);
        SetupMoveBtn(rightBtn,   _node?.rightVP);
    }

    void SetupSceneBtn(Button btn, string scene, System.Action action)
    {
        if (!btn) return;
        bool active = !string.IsNullOrEmpty(scene);
        btn.gameObject.SetActive(active);
        if (active)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(()=> action?.Invoke());
        }
    }

    void SetupMoveBtn(Button btn, Transform vp)
    {
        if (!btn) return;
        bool active = vp != null;
        btn.gameObject.SetActive(active);
        if (active)
        {
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(() =>
            {
                if (_mover != null) _mover.MoveTo(vp);
            });
        }
    }
}
