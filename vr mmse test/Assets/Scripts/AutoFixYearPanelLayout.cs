using UnityEngine;
using UnityEngine.UI;

public class AutoFixYearPanelLayout : MonoBehaviour
{
    [Header("自動修復年份面板佈局")]
    [SerializeField] private bool fixOnStart = true;
    
    void Start()
    {
        if (fixOnStart)
        {
            FixYearPanelLayout();
        }
    }
    
    public void FixYearPanelLayout()
    {
        Debug.Log("開始修復年份面板排列...");
        
        // 嘗試多種方法找到Panel_Year
        GameObject panelYear = FindPanelYear();
        
        if (panelYear == null)
        {
            Debug.LogError("無法找到Panel_Year物件");
            return;
        }

        Debug.Log($"找到Panel_Year: {panelYear.name}");

        // 取得或添加GridLayoutGroup組件
        GridLayoutGroup gridLayout = panelYear.GetComponent<GridLayoutGroup>();
        if (gridLayout == null)
        {
            Debug.Log("Panel_Year 沒有GridLayoutGroup組件，正在添加...");
            gridLayout = panelYear.AddComponent<GridLayoutGroup>();
        }

        // 設置為橫列排列 (固定行數為1)
        gridLayout.constraint = GridLayoutGroup.Constraint.FixedRowCount;
        gridLayout.constraintCount = 1;
        
        // 設置間距和元素大小
        gridLayout.spacing = new Vector2(15f, 10f);
        gridLayout.cellSize = new Vector2(140f, 50f);
        gridLayout.childAlignment = TextAnchor.MiddleCenter;
        gridLayout.startCorner = GridLayoutGroup.Corner.UpperLeft;
        gridLayout.startAxis = GridLayoutGroup.Axis.Horizontal;
        
        Debug.Log("Year Panel 已設置為橫列排列");
        Debug.Log($"Constraint: {gridLayout.constraint}");
        Debug.Log($"Constraint Count: {gridLayout.constraintCount}");
        
        // 延遲執行強制重新計算佈局，確保組件完全初始化
        StartCoroutine(ForceLayoutRebuildDelayed(panelYear));
    }
    
    private GameObject FindPanelYear()
    {
        // 方法1: 直接搜尋
        GameObject panelYear = GameObject.Find("Panel_Year");
        if (panelYear != null) return panelYear;
        
        // 方法2: 在UI_Canvas下搜尋
        GameObject canvas = GameObject.Find("UI_Canvas");
        if (canvas != null)
        {
            Transform panelYearTransform = canvas.transform.Find("Panel_Year");
            if (panelYearTransform != null)
            {
                return panelYearTransform.gameObject;
            }
            
            // 遞歸搜尋所有子物件
            panelYearTransform = FindChildRecursive(canvas.transform, "Panel_Year");
            if (panelYearTransform != null)
            {
                return panelYearTransform.gameObject;
            }
        }
        
        // 方法3: 搜尋所有GameObject
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        foreach (GameObject obj in allObjects)
        {
            if (obj.name == "Panel_Year" && obj.scene.name != null)
            {
                return obj;
            }
        }
        
        return null;
    }
    
    private Transform FindChildRecursive(Transform parent, string childName)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
            {
                return child;
            }
            
            Transform result = FindChildRecursive(child, childName);
            if (result != null)
            {
                return result;
            }
        }
        return null;
    }
    
    private System.Collections.IEnumerator ForceLayoutRebuildDelayed(GameObject panelYear)
    {
        yield return new WaitForEndOfFrame();
        
        RectTransform rectTransform = panelYear.GetComponent<RectTransform>();
        if (rectTransform != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(rectTransform);
            Debug.Log("佈局重新計算完成");
        }
    }
}