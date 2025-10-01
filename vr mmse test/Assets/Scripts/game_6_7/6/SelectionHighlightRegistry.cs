using UnityEngine;

public static class SelectionHighlightRegistry
{
    public static SelectionHighlighter Current { get; private set; }

    public static void Take(SelectionHighlighter h)
    {
        if (Current == h) return;
        if (Current != null) Current.ForceDeselect();
        Current = h;
    }

    public static void Clear(SelectionHighlighter h)
    {
        if (Current == h) Current = null;
    }

    // ★ 新增：清掉全場所有白圈/藍圈
    public static void ClearAll()
    {
#if UNITY_2022_2_OR_NEWER
        var all = Object.FindObjectsByType<SelectionHighlighter>(FindObjectsSortMode.None);
#else
        var all = Object.FindObjectsOfType<SelectionHighlighter>(true);
#endif
        foreach (var h in all) h.ForceDeselect();
        Current = null;
    }
}
