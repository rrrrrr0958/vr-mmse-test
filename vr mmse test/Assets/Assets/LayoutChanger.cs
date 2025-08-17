using UnityEngine;
using UnityEngine.UI;

public class LayoutChanger : MonoBehaviour
{
    [ContextMenu("Change Panel_Year to Horizontal Layout")]
    public void ChangeToHorizontalLayout()
    {
        // Find Panel_Year in the scene
        GameObject panelYear = GameObject.Find("Panel_Year");
        
        if (panelYear == null)
        {
            // Try to find it in UI_Canvas
            GameObject uiCanvas = GameObject.Find("UI_Canvas");
            if (uiCanvas != null)
            {
                Transform panelYearTransform = uiCanvas.transform.Find("Panel_Year");
                if (panelYearTransform != null)
                {
                    panelYear = panelYearTransform.gameObject;
                }
            }
        }
        
        if (panelYear != null)
        {
            Debug.Log("Found Panel_Year: " + panelYear.name);
            
            // Remove VerticalLayoutGroup if it exists
            VerticalLayoutGroup verticalLayout = panelYear.GetComponent<VerticalLayoutGroup>();
            if (verticalLayout != null)
            {
                DestroyImmediate(verticalLayout);
                Debug.Log("Removed VerticalLayoutGroup");
            }
            
            // Add HorizontalLayoutGroup if it doesn't exist
            HorizontalLayoutGroup horizontalLayout = panelYear.GetComponent<HorizontalLayoutGroup>();
            if (horizontalLayout == null)
            {
                horizontalLayout = panelYear.AddComponent<HorizontalLayoutGroup>();
                Debug.Log("Added HorizontalLayoutGroup");
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
            
            Debug.Log("Successfully configured horizontal layout for Panel_Year");
        }
        else
        {
            Debug.LogError("Panel_Year not found in the scene!");
        }
    }
}
