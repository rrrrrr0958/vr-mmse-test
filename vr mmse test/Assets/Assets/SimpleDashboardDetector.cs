using UnityEngine;

public class SimpleDashboardDetector : MonoBehaviour
{
    public Transform personTransform;
    public DashboardManager dashboardManager;
    
    private ZooExit.PersonWalkToGate personScript;
    private bool hasTriggered = false;
    
    void Start()
    {
        if (personTransform == null)
        {
            personTransform = GameObject.Find("Person").transform;
        }
        
        if (dashboardManager == null)
        {
            dashboardManager = FindFirstObjectByType<DashboardManager>();
        }
        
        if (personTransform != null)
        {
            personScript = personTransform.GetComponent<ZooExit.PersonWalkToGate>();
        }
        
        Debug.Log("SimpleDashboardDetector initialized");
        Debug.Log("Person found: " + (personTransform != null));
        Debug.Log("Person script found: " + (personScript != null));
        Debug.Log("Dashboard manager found: " + (dashboardManager != null));
    }
    
    void Update()
    {
        if (hasTriggered || personScript == null || dashboardManager == null) return;
        
        // 簡單檢測：如果Person不在走路狀態
        if (!personScript.isWalking)
        {
            Debug.Log("Person stopped walking - triggering dashboard");
            dashboardManager.ShowFinalDashboard();
            hasTriggered = true;
        }
        
        // Debug info每秒一次
        if (Time.frameCount % 60 == 0)
        {
            Debug.Log("Person isWalking: " + personScript.isWalking);
        }
    }
}