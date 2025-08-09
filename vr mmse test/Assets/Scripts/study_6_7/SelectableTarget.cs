using UnityEngine;


public class SelectableTarget : MonoBehaviour
{
    public string targetId = "unset";

    // 先留空；等你要接判斷時再綁到 Select Entered
    public void OnSelect(UnityEngine.XR.Interaction.Toolkit.Interactors.XRBaseInteractor _)
    {
        // 之後：FindObjectOfType<QuizManager>()?.Submit(targetId);
    }
}
