using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DashboardManager : MonoBehaviour
{
    [Header("Dashboard Settings")]
    public Canvas dashboardCanvas;
    public GameObject dashboardPanel;
    public float showDelay = 1.0f;
    public float animationDuration = 0.5f;
    
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
    }
    
    void Update()
    {
        CheckPlayerMovement();
    }
    
    void InitializeDashboard()
    {
        if (dashboardPanel != null)
        {
            dashboardPanel.SetActive(false);
        }
        
        if (dashboardCanvas != null)
        {
            dashboardCanvas.enabled = false;
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
        }
    }
    
    void CheckPlayerMovement()
    {
        if (playerTransform == null) return;
        
        Vector3 currentPosition = playerTransform.position;
        float movementDistance = Vector3.Distance(currentPosition, lastPlayerPosition);
        
        isPlayerMoving = movementDistance > stopThreshold * Time.deltaTime;
        
        if (isPlayerMoving)
        {
            stopTimer = 0f;
            if (isDashboardVisible)
            {
                HideDashboard();
            }
        }
        else
        {
            stopTimer += Time.deltaTime;
            
            if (!isDashboardVisible && stopTimer >= showDelay)
            {
                ShowDashboard();
            }
        }
        
        lastPlayerPosition = currentPosition;
    }
    
    public void ShowDashboard()
    {
        if (isDashboardVisible) return;
        
        StartCoroutine(ShowDashboardCoroutine());
    }
    
    public void HideDashboard()
    {
        if (!isDashboardVisible) return;
        
        StartCoroutine(HideDashboardCoroutine());
    }
    
    IEnumerator ShowDashboardCoroutine()
    {
        isDashboardVisible = true;
        
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
            
            float elapsedTime = 0f;
            while (elapsedTime < animationDuration)
            {
                float progress = elapsedTime / animationDuration;
                canvasGroup.alpha = progress;
                elapsedTime += Time.deltaTime;
                yield return null;
            }
            
            canvasGroup.alpha = 1f;
        }
    }
    
    IEnumerator HideDashboardCoroutine()
    {
        if (dashboardPanel != null)
        {
            CanvasGroup canvasGroup = dashboardPanel.GetComponent<CanvasGroup>();
            if (canvasGroup != null)
            {
                float elapsedTime = 0f;
                while (elapsedTime < animationDuration)
                {
                    float progress = elapsedTime / animationDuration;
                    canvasGroup.alpha = 1f - progress;
                    elapsedTime += Time.deltaTime;
                    yield return null;
                }
            }
            
            dashboardPanel.SetActive(false);
        }
        
        if (dashboardCanvas != null)
        {
            dashboardCanvas.enabled = false;
        }
        
        isDashboardVisible = false;
    }
    
    void OnContinueClicked()
    {
        Debug.Log("繼續遊戲");
        HideDashboard();
    }
    
    void OnSettingsClicked()
    {
        Debug.Log("打開設定");
    }
    
    void OnExitClicked()
    {
        Debug.Log("退出遊戲");
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