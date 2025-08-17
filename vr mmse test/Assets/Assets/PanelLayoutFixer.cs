using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class PanelLayoutFixer : MonoBehaviour
{
    void Start()
    {
        FixPanelYearLayout();
    }
    
    public void FixPanelYearLayout()
    {
        // Find UI_Canvas first
        GameObject uiCanvas = GameObject.Find("UI_Canvas");
        if (uiCanvas == null)
        {
            Debug.LogError("UI_Canvas not found!");
            return;
        }
        
        // Find Panel_Year as child of UI_Canvas
        Transform panelYearTransform = uiCanvas.transform.Find("Panel_Year");
        if (panelYearTransform == null)
        {
            Debug.LogError("Panel_Year not found as child of UI_Canvas!");
            return;
        }
        
        GameObject panelYear = panelYearTransform.gameObject;
        Debug.Log("Found Panel_Year: " + panelYear.name);
        
        // Remove VerticalLayoutGroup if it exists
        VerticalLayoutGroup verticalLayout = panelYear.GetComponent<VerticalLayoutGroup>();
        if (verticalLayout != null)
        {
            DestroyImmediate(verticalLayout);
            Debug.Log("Removed VerticalLayoutGroup from Panel_Year");
        }
        
        // Add HorizontalLayoutGroup if it doesn't exist
        HorizontalLayoutGroup horizontalLayout = panelYear.GetComponent<HorizontalLayoutGroup>();
        if (horizontalLayout == null)
        {
            horizontalLayout = panelYear.AddComponent<HorizontalLayoutGroup>();
            Debug.Log("Added HorizontalLayoutGroup to Panel_Year");
        }
        
        // Configure the horizontal layout
        horizontalLayout.spacing = 10f;
        horizontalLayout.childAlignment = TextAnchor.MiddleCenter;
        horizontalLayout.childScaleWidth = false;
        horizontalLayout.childScaleHeight = false;
        horizontalLayout.childControlWidth = false;
        horizontalLayout.childControlHeight = false;
        horizontalLayout.childForceExpandWidth = false;
        horizontalLayout.childForceExpandHeight = false;
        
        Debug.Log("Successfully configured horizontal layout for Panel_Year!");
        Debug.Log("Year buttons should now be arranged horizontally.");
    }
}
