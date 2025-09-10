using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

[RequireComponent(typeof(UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable))]
public class SelectableTarget : MonoBehaviour
{
    [Tooltip("此物件的ID：camera / bigPlant / lamp / yellowBall / whiteBottle / ...")]
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
    /// 最終送出答案：交給 QuizManager → GameDirector.LockAndAdvance()
    /// </summary>
    public void Submit()
    {
        // 若已鎖定或不在 Game1，直接忽略
        if (GameDirector.Instance != null && !GameDirector.Instance.CanInteractGame1())
            return;

#if UNITY_2022_2_OR_NEWER
        var qm = FindFirstObjectByType<QuizManager>();
#else
        var qm = FindObjectOfType<QuizManager>();
#endif
        if (qm != null)
        {
            qm.Submit(targetId);
        }
        else
        {
            Debug.LogWarning("[SelectableTarget] 場景裡找不到 QuizManager。");
        }
    }
}
