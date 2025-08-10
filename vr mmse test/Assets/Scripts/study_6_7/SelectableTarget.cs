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
        interactable.selectEntered.AddListener(OnSelect);   // Grip
        interactable.activated.AddListener(OnActivated);    // Trigger
    }

    void OnDestroy()
    {
        if (!interactable) return;
        interactable.selectEntered.RemoveListener(OnSelect);
        interactable.activated.RemoveListener(OnActivated);
    }

    void OnSelect(SelectEnterEventArgs _)  { Submit(); }
    void OnActivated(ActivateEventArgs _)  { Submit(); }

    public void Submit()
    {
        var qm = FindAnyObjectByType<QuizManager>();
        if (qm != null) qm.SubmitCandidate(targetId, gameObject.name);
    }
}
