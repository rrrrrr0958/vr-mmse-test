using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class PanelLayoutManager : MonoBehaviour
{
    [Header("執行時自動修改Panel_Year為橫列")]
    public bool executeOnStart = true;
    
    void Start()
    {
        if (executeOnStart)
        {
            StartCoroutine(ChangeLayoutAfterDelay());
        }
    }
    
    System.Collections.IEnumerator ChangeLayoutAfterDelay()
    {
        yield return new WaitForSeconds(0.1f); // 等待場景完全載入
        
        ChangeYearPanelToHorizontal();
    }
    
    [ContextMenu("Change Panel_Year to Horizontal")]
    public void ChangeYearPanelToHorizontal()
    {
        Debug.Log("開始尋找Panel_Year...");
        
        // 方法1: 直接使用GameObject.Find
        GameObject panelYear = GameObject.Find("Panel_Year");
        
        // 方法2: 如果找不到，從所有Canvas中尋找
        if (panelYear == null)
        {
            Canvas[] allCanvas = FindObjectsOfType<Canvas>();
            foreach (Canvas canvas in allCanvas)
            {
                Transform found = canvas.transform.Find("Panel_Year");
                if (found != null)
                {
                    panelYear = found.gameObject;
                    break;
                }
                
                // 遞歸搜尋子物件
                found = FindChildRecursive(canvas.transform, "Panel_Year");
                if (found != null)
                {
                    panelYear = found.gameObject;
                    break;
                }
            }
        }
        
        // 方法3: 使用Resources.FindObjectsOfTypeAll找到非活動物件
        if (panelYear == null)
        {
            GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
            foreach (GameObject obj in allObjects)
            {
                if (obj.name == "Panel_Year" && obj.scene.isLoaded)
                {
                    panelYear = obj;
                    break;
                }
            }
        }
        
        if (panelYear != null)
        {
            Debug.Log($"找到Panel_Year! Active: {panelYear.activeSelf}");
            
            // 移除VerticalLayoutGroup
            VerticalLayoutGroup verticalLayout = panelYear.GetComponent<VerticalLayoutGroup>();
            if (verticalLayout != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(verticalLayout);
                }
                else
                {
                    DestroyImmediate(verticalLayout);
                }
                Debug.Log("✓ 已移除VerticalLayoutGroup");
            }
            else
            {
                Debug.Log("沒有找到VerticalLayoutGroup組件");
            }
            
            // 添加HorizontalLayoutGroup
            HorizontalLayoutGroup horizontalLayout = panelYear.GetComponent<HorizontalLayoutGroup>();
            if (horizontalLayout == null)
            {
                horizontalLayout = panelYear.AddComponent<HorizontalLayoutGroup>();
            }
            
            // 設定屬性
            horizontalLayout.spacing = 10;
            horizontalLayout.childAlignment = TextAnchor.MiddleCenter;
            horizontalLayout.childScaleWidth = false;
            horizontalLayout.childScaleHeight = false;
            horizontalLayout.childControlWidth = false;
            horizontalLayout.childControlHeight = false;
            horizontalLayout.childForceExpandWidth = false;
            horizontalLayout.childForceExpandHeight = false;
            
            Debug.Log("✓ 已添加並配置HorizontalLayoutGroup");
            Debug.Log("🎉 年份按鈕現在是橫列排列了！");
            
            // 強制重新計算布局
            LayoutRebuilder.MarkLayoutForRebuild(panelYear.GetComponent<RectTransform>());
        }
        else
        {
            Debug.LogError("❌ 找不到Panel_Year物件！");
        }
    }
    
    Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name == childName)
            {
                return child;
            }
            
            Transform found = FindChildRecursive(child, childName);
            if (found != null)
            {
                return found;
            }
        }
        return null;
    }
}