using UnityEngine;

public class DashboardTester : MonoBehaviour
{
    public DashboardManager dashboardManager;
    
    void Start()
    {
        if (dashboardManager == null)
        {
            dashboardManager = FindFirstObjectByType<DashboardManager>();
        }
    }
    
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.T))
        {
            Debug.Log("Testing dashboard visibility...");
            if (dashboardManager != null)
            {
                dashboardManager.ShowFinalDashboard(); // 修正方法名
            }
            else
            {
                Debug.LogError("DashboardManager not found!");
            }
        }
        
        if (Input.GetKeyDown(KeyCode.H))
        {
            Debug.Log("Hiding dashboard...");
            if (dashboardManager != null)
            {
                dashboardManager.HideDashboard();
            }
        }
        
        if (Input.GetKeyDown(KeyCode.R))
        {
            Debug.Log("Resetting dashboard...");
            if (dashboardManager != null)
            {
                dashboardManager.ResetDashboard();
            }
        }
    }
}