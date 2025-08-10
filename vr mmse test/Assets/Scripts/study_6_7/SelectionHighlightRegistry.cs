using UnityEngine;

public class SelectionHighlightRegistry : MonoBehaviour
{
    private static SelectionHighlighter _current;
    public  static SelectionHighlighter Current => _current;

    /// 取得唯一選中權（會先讓上一個熄掉）
    public static void Take(SelectionHighlighter next)
    {
        if (_current == next) return;
        if (_current) _current.ForceDeselect();
        _current = next;
    }

    /// 如果要主動清掉當前（很少用到）
    public static void Clear(SelectionHighlighter who)
    {
        if (_current == who) _current = null;
    }
}
