using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class YearPanelFixer : MonoBehaviour 
{
    [ContextMenu("Fix Year Panel Layout")]
    public static void FixYearPanelLayoutStatic()
    {
        Debug.Log("開始修復年份面板排列...");
        
        // 直接使用名稱搜尋
        GameObject[] allGameObjects = FindObjectsOfType<GameObject>();
        GameObject panelYear = null;
        
        foreach (GameObject obj in allGameObjects)
        {
            if (obj.name == "Panel_Year")
            {
                panelYear = obj;
                break;
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
        LayoutRebuilder.ForceRebuildLayoutImmediate(panelYear.GetComponent<RectTransform>());
        
        // 標記場景為已修改
        #if UNITY_EDITOR
        UnityEditor.EditorUtility.SetDirty(panelYear);
        #endif
    }
}