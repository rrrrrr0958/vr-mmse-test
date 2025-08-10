using UnityEngine;

public class SelectionHighlightRegistry : MonoBehaviour
{
    public static SelectionHighlighter Current;

    public static void Take(SelectionHighlighter h)
    {
        if (Current && Current != h) Current.ForceDeselect();
        Current = h;
    }
}
