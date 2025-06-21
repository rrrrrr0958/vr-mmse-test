using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DashboardManager : MonoBehaviour
{
    [Header("Dashboard Settings")]
    public Canvas dashboardCanvas;
    public GameObject dashboardPanel;
    public float showDelay = 0.5f; // 減少延遲時間
    public float animationDuration = 0.5f;
    
    [Header("Dashboard Content")]
    public Text statusText;
    public Button continueButton;
    public Button settingsButton;
    public Button exitButton;
    
    [Header("Player Reference")]
    public Transform playerTransform;
    
    private bool isDashboardVisible = false;
    private bool hasShownDashboard = false; // 新增：確保只顯示一次
    private ZooExit.PersonWalkToGate personWalkScript;
    private float stopTimer = 0f;
    
    void Start()
    {
        InitializeDashboard();
        
        if (playerTransform != null)
        {
            // 獲取PersonWalkToGate腳本引用
            personWalkScript = playerTransform.GetComponent<ZooExit.PersonWalkToGate>();
        }
    }
    
    void Update()
    {
        CheckPlayerStatus();
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
            statusText.text = "恭喜完成動物園參觀！";
        }
    }
    
    void CheckPlayerStatus()
    {
        if (hasShownDashboard || playerTransform == null || personWalkScript == null) 
            return;
        
        // 檢查玩家是否已經停止走路
        bool isPlayerWalking = personWalkScript.isWalking;
        
        if (!isPlayerWalking)
        {
            stopTimer += Time.deltaTime;
            
            if (stopTimer >= showDelay)
            {
                Debug.Log("Player has reached destination - showing final dashboard");
                ShowFinalDashboard();
                hasShownDashboard = true; // 確保只顯示一次
            }
        }
        else
        {
            stopTimer = 0f; // 重置計時器
        }
    }
    
    public void ShowFinalDashboard()
    {
        if (isDashboardVisible) return;
        
        Debug.Log("Showing final dashboard - will stay visible");
        StartCoroutine(ShowDashboardCoroutine());
    }
    
    // 向後兼容性別名
    public void ShowDashboard()
    {
        ShowFinalDashboard();
    }
    
    // 移除HideDashboard的自動調用，只保留手動隱藏功能
    public void HideDashboard()
    {
        if (!isDashboardVisible) return;
        
        Debug.Log("Manually hiding dashboard");
        StartCoroutine(HideDashboardCoroutine());
        hasShownDashboard = false; // 允許重新顯示
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
            Debug.Log("Dashboard is now fully visible and will remain visible");
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
        // 不再自動隱藏dashboard，由玩家決定
        if (statusText != null)
        {
            statusText.text = "感謝您的參觀！";
        }
    }
    
    void OnSettingsClicked()
    {
        Debug.Log("打開設定");
        if (statusText != null)
        {
            statusText.text = "設定選項";
        }
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
    
    // 新增：重置功能，如果需要重新開始
    public void ResetDashboard()
    {
        hasShownDashboard = false;
        stopTimer = 0f;
        HideDashboard();
    }
}