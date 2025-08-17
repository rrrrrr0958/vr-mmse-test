using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;

public class YearPanelFixerEditor : EditorWindow
{
    [MenuItem("Tools/Fix Year Panel Layout")]
    public static void FixYearPanelLayout()
    {
        Debug.Log("開始修復年份面板排列...");
        
        // 使用更廣泛的搜尋方法
        GameObject panelYear = GameObject.Find("Panel_Year");
        
        if (panelYear == null)
        {
            // 如果直接搜尋找不到，嘗試在UI_Canvas下搜尋
            GameObject canvas = GameObject.Find("UI_Canvas");
            if (canvas != null)
            {
                Transform panelYearTransform = canvas.transform.Find("Panel_Year");
                if (panelYearTransform != null)
                {
                    panelYear = panelYearTransform.gameObject;
                }
            }
        }
        
        if (panelYear == null)
        {
            Debug.LogError("無法找到Panel_Year物件");
            return;
        }

        Debug.Log($"找到Panel_Year: {panelYear.name}");

        // 取得GridLayoutGroup組件
        GridLayoutGroup gridLayout = panelYear.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
        {
            Debug.LogError("Panel_Year 沒有GridLayoutGroup組件，正在添加...");
            gridLayout = panelYear.AddComponent<GridLayoutGroup>();
        }

        // 設置為橫列排列 (固定行數為1)
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        gridLayout.constraintCount = 1;
        
        // 設置間距和元素大小
        gridLayout.spacing = new Vector2(10f, 10f);
        gridLayout.cellSize = new Vector2(120f, 40f);
        gridLayout.childAlignment = TextAnchor.MiddleCenter;
        
        Debug.Log("Year Panel 已設置為橫列排列");
        Debug.Log($"Constraint: {gridLayout.constraint}");
        Debug.Log($"Constraint Count: {gridLayout.constraintCount}");
        
        // 強制重新計算佈局
        if (Application.isPlaying)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(panelYear.GetComponent<RectTransform>());
        }
        
        // 標記場景為已修改
        EditorUtility.SetDirty(panelYear);
        
        Debug.Log("年份按鈕佈局修復完成！");
    }
}
#endif