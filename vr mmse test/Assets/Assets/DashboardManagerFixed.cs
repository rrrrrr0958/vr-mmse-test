using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DashboardManagerFixed : MonoBehaviour
{
    [Header("Dashboard Settings")]
    public Canvas dashboardCanvas;
    public GameObject dashboardPanel;
    public float showDelay = 1.0f;
    public float animationDuration = 0.5f;
    public bool keepDashboardVisible = true;
    
    [Header("Dashboard Content")]
    public Text statusText;
    public Button continueButton;
    public Button settingsButton;
    public Button exitButton;
    
    [Header("Player Reference")]
    public Transform playerTransform;
    public float stopThreshold = 0.1f;
    
    private bool isDashboardVisible = false;
    private bool isPlayerMoving = false;
    private Vector3 lastPlayerPosition;
    private float stopTimer = 0f;
    
    void Start()
    {
        InitializeDashboard();
        
        if (playerTransform != null)
        {
            lastPlayerPosition = playerTransform.position;
        }
        
        // 強制顯示並保持 Dashboard 可見
        Invoke("ForceShowDashboard", 0.1f);
    }
    
    void Update()
    {
        // 不檢查玩家移動，保持 Dashboard 始終可見
    }
    
    void InitializeDashboard()
    {
        if (dashboardCanvas != null)
        {
            dashboardCanvas.enabled = true;
        }
        
        if (dashboardPanel != null)
        {
            dashboardPanel.SetActive(true);
        }
        
        if (continueButton != null)
        {
            continueButton.onClick.AddListener(OnContinueClicked);
        }
        
        if (settingsButton != null)
        {
            settingsButton.onClick.AddListener(OnSettingsClicked);
        }
        
        if (exitButton != null)
        {
            exitButton.onClick.AddListener(OnExitClicked);
        }
        
        if (statusText != null)
        {
            statusText.text = "歡迎來到動物園！";
            statusText.color = Color.white;
            statusText.fontSize = 36;
            statusText.alignment = TextAnchor.MiddleCenter;
        }
    }
    
    void ForceShowDashboard()
    {
        if (dashboardCanvas != null)
        {
            dashboardCanvas.enabled = true;
        }
        
        if (dashboardPanel != null)
        {
            dashboardPanel.SetActive(true);
            
            CanvasGroup canvasGroup = dashboardPanel.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                canvasGroup = dashboardPanel.AddComponent<CanvasGroup>();
            }
            canvasGroup.alpha = 1f;
        }
        
        isDashboardVisible = true;
        Debug.Log("Dashboard forced to be visible and should stay visible");
    }
    
    void OnContinueClicked()
    {
        Debug.Log("Continue Game - Dashboard will remain visible");
    }
    
    void OnSettingsClicked()
    {
        Debug.Log("Open Settings");
    }
    
    void OnExitClicked()
    {
        Debug.Log("Exit Game");
        Application.Quit();
    }
    
    public void SetStatusText(string text)
    {
        if (statusText != null)
        {
            statusText.text = text;
        }
    }
}
