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
        // Grip = Select
        interactable.selectEntered.AddListener(OnSelect);
        // Trigger = Activate
        interactable.activated.AddListener(OnActivated);
    }

    void OnDestroy()
    {
        if (!interactable) return;
        interactable.selectEntered.RemoveListener(OnSelect);
        interactable.activated.RemoveListener(OnActivated);
    }

    void OnSelect(SelectEnterEventArgs _)  { Submit(); }
    void OnActivated(ActivateEventArgs _)  { Submit(); }

// 只展示差異：把 Submit 改成 public
    public void Submit()
    {
        var qm = FindAnyObjectByType<QuizManager>();
        if (qm != null) qm.Submit(targetId);
    }

}
