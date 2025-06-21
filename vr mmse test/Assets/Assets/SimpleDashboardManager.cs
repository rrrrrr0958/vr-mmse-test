using UnityEngine;
using UnityEngine.UI;

public class SimpleDashboardManager : MonoBehaviour
{
    private Transform playerTransform;
    private Canvas dashboardCanvas;
    private GameObject dashboardPanel;
    private Vector3 lastPlayerPosition;
    private float stopTimer = 0f;
    private bool isDashboardVisible = false;
    
    void Start()
    {
        Debug.Log("SimpleDashboardManager: Starting initialization");
        
        // 找到Player
        GameObject person = GameObject.Find("Person");
        if (person != null)
        {
            playerTransform = person.transform;
            lastPlayerPosition = playerTransform.position;
            Debug.Log("SimpleDashboardManager: Found Person at " + lastPlayerPosition);
        }
        else
        {
            Debug.LogError("SimpleDashboardManager: Person not found!");
        }
        
        // 找到Canvas
        dashboardCanvas = GameObject.Find("DashboardCanvas")?.GetComponent<Canvas>();
        if (dashboardCanvas != null)
        {
            dashboardCanvas.enabled = false;
            Debug.Log("SimpleDashboardManager: Found DashboardCanvas");
        }
        else
        {
            Debug.LogError("SimpleDashboardManager: DashboardCanvas not found!");
        }
        
        // 找到Panel
        dashboardPanel = GameObject.Find("DashboardPanel");
        if (dashboardPanel != null)
        {
            dashboardPanel.SetActive(false);
            Debug.Log("SimpleDashboardManager: Found DashboardPanel");
        }
        else
        {
            Debug.LogError("SimpleDashboardManager: DashboardPanel not found!");
        }
        
        Debug.Log("SimpleDashboardManager: Initialization complete");
    }
    
    void Update()
    {
        if (playerTransform == null) return;
        
        Vector3 currentPos = playerTransform.position;
        float distance = Vector3.Distance(currentPos, lastPlayerPosition);
        
        // 檢查是否停止移動
        if (distance < 0.01f)
        {
            stopTimer += Time.deltaTime;
            if (stopTimer > 2.0f && !isDashboardVisible)
            {
                Debug.Log("SimpleDashboardManager: Player stopped, showing dashboard");
                ShowDashboard();
            }
        }
        else
        {
            stopTimer = 0f;
            if (isDashboardVisible)
            {
                Debug.Log("SimpleDashboardManager: Player moving, hiding dashboard");
                HideDashboard();
            }
        }
        
        lastPlayerPosition = currentPos;
    }
    
    void ShowDashboard()
    {
        isDashboardVisible = true;
        
        if (dashboardCanvas != null)
        {
            dashboardCanvas.enabled = true;
            Debug.Log("SimpleDashboardManager: Canvas enabled");
        }
        
        if (dashboardPanel != null)
        {
            dashboardPanel.SetActive(true);
            Debug.Log("SimpleDashboardManager: Panel activated");
        }
    }
    
    void HideDashboard()
    {
        isDashboardVisible = false;
        
        if (dashboardCanvas != null)
        {
            dashboardCanvas.enabled = false;
        }
        
        if (dashboardPanel != null)
        {
            dashboardPanel.SetActive(false);
        }
    }
}