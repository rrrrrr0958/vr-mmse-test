using UnityEngine;

public static class CanvasGroupUtil
{
    public static void Show(CanvasGroup cg, bool on)
    {
        if (!cg) return;
        cg.alpha = on ? 1f : 0f;
        cg.interactable = on;
        cg.blocksRaycasts = on;
    }
}
