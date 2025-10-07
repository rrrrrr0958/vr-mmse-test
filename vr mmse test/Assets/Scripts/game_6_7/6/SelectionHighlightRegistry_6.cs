using UnityEngine;

public static class SelectionHighlightRegistry
{
    // ① 「已被選中的唯一白圈」(你原本就有)
    public static SelectionHighlighter_6 Current { get; private set; }

    public static void Take(SelectionHighlighter_6 h)
    {
        if (Current == h) return;
        if (Current != null) Current.ForceDeselect();
        Current = h;
    }

    public static void Clear(SelectionHighlighter_6 h)
    {
        if (Current == h) Current = null;
    }

    // ★ 清掉全場所有白圈/藍圈（你原本就有）
    public static void ClearAll()
    {
#if UNITY_2022_2_OR_NEWER
        var all = Object.FindObjectsByType<SelectionHighlighter_6>(FindObjectsSortMode.None);
#else
        var all = Object.FindObjectsOfType<SelectionHighlighter_6>(true);
#endif
        foreach (var h in all) h.ForceDeselect();
        Current = null;
        HoverOwner = null; // 也把 hover 鎖釋放
    }

    // ② 新增：全域「Hover 鎖」
    //    任一物件 hover 成功後，直到它 hoverExited 釋放為止，其他物件不能 hover。
    public static SelectionHighlighter_6 HoverOwner { get; private set; }

    // 嘗試取得 Hover 鎖；取到回傳 true，否則 false
    public static bool TryAcquireHover(SelectionHighlighter_6 h)
    {
        if (HoverOwner == null || HoverOwner == h)
        {
            HoverOwner = h;
            return true;
        }
        return false;
    }

    // 釋放 Hover 鎖（只有持有者能釋放）
    public static void ReleaseHover(SelectionHighlighter_6 h)
    {
        if (HoverOwner == h) HoverOwner = null;
    }

    // 幫助判斷：對某個 highlighter 來說，目前是否被別人鎖住
    public static bool IsHoverLockedFor(SelectionHighlighter_6 h)
        => HoverOwner != null && HoverOwner != h;
}
