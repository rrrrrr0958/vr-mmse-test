using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable))]
public class SelectableTarget_6 : MonoBehaviour
{
    [Tooltip("此物件的ID：camera / cheese / sausage / bowl / meat / ...")]
    public string targetId;

    UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;

    void Awake()
    {
        interactable = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();

        // 這兩個事件任何一個觸發，都視為「選定」並送出最終答案
        interactable.selectEntered.AddListener(OnSelect);
        interactable.activated.AddListener(OnActivated);
    }

    void OnDestroy()
    {
        if (!interactable) return;
        interactable.selectEntered.RemoveListener(OnSelect);
        interactable.activated.RemoveListener(OnActivated);
    }

    void OnSelect(SelectEnterEventArgs _)   => Submit();
    void OnActivated(ActivateEventArgs  _)  => Submit();

    /// <summary>
    /// 最終送出答案：交給 QuizManager
    /// </summary>
    public void Submit()
    {
#if UNITY_2022_2_OR_NEWER
        var qm = FindFirstObjectByType<QuizManager_6>();
#else
        var qm = FindObjectOfType<QuizManager_6>();
#endif
        if (qm == null)
        {
            Debug.LogWarning("[SelectableTarget] 場景裡找不到 QuizManager。");
            return;
        }

        // ✅ 改成問 QuizManager 是否可互動
        if (!qm.CanInteract())
            return;

        qm.Submit(targetId);
    }
}
