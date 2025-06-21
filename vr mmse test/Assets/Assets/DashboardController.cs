using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DashboardController : MonoBehaviour
{
    [Header("References")]
    public Transform player;
    public Canvas canvas;
    public GameObject panel;
    public Text statusText;
    public Button continueButton;
    
    [Header("Settings")]
    public float stopDetectionTime = 2.0f;
    public float movementThreshold = 0.05f;
    
    private Vector3 lastPlayerPos;
    private float stopTimer;
    private bool isDashboardVisible;
    private bool hasFoundComponents;
    
    void Start()
    {
        Debug.Log("DashboardController: Starting...");
        FindComponents();
        InitializeDashboard();
    }
    
    void FindComponents()
    {
        // Find Player
        if (player == null)
        {
            GameObject personGO = GameObject.Find("Person");
            if (personGO != null)
            {
                player = personGO.transform;
                Debug.Log("DashboardController: Found Person");
            }
        }
        
        // Find Canvas
        if (canvas == null)
        {
            GameObject canvasGO = GameObject.Find("DashboardCanvas");
            if (canvasGO != null)
            {
                canvas = canvasGO.GetComponent<Canvas>();
                Debug.Log("DashboardController: Found Canvas");
            }
        }
        
        // Find Panel
        if (panel == null)
        {
            panel = GameObject.Find("DashboardPanel");
            if (panel != null)
            {
                Debug.Log("DashboardController: Found Panel");
            }
        }
        
        // Find StatusText
        if (statusText == null)
        {
            GameObject statusGO = GameObject.Find("StatusText");
            if (statusGO != null)
            {
                statusText = statusGO.GetComponent<Text>();
                Debug.Log("DashboardController: Found StatusText");
            }
        }
        
        // Find ContinueButton
        if (continueButton == null)
        {
            GameObject buttonGO = GameObject.Find("ContinueButton");
            if (buttonGO != null)
            {
                continueButton = buttonGO.GetComponent<Button>();
                Debug.Log("DashboardController: Found ContinueButton");
            }
        }
        
        hasFoundComponents = (player != null && canvas != null && panel != null);
        Debug.Log("DashboardController: Components found = " + hasFoundComponents);
    }
    
    void InitializeDashboard()
    {
        if (!hasFoundComponents) return;
        
        // Hide dashboard initially
        canvas.enabled = false;
        panel.SetActive(false);
        
        // Set initial position
        lastPlayerPos = player.position;
        stopTimer = 0f;
        isDashboardVisible = false;
        
        // Setup UI text and button
        if (statusText != null)
        {
            statusText.text = "！園區進度來到尾聲";
            statusText.fontSize = 24;
            statusText.alignment = TextAnchor.MiddleCenter;
            statusText.color = Color.white;
        }
        
        if (continueButton != null)
        {
            continueButton.onClick.RemoveAllListeners();
            continueButton.onClick.AddListener(() => {
                Debug.Log("Continue button clicked");
                HideDashboard();
            });
            
            // Set button text
            Text buttonText = continueButton.GetComponentInChildren<Text>();
            if (buttonText != null)
            {
                buttonText.text = "繼續探索";
                buttonText.fontSize = 18;
                buttonText.color = Color.white;
            }
        }
        
        Debug.Log("DashboardController: Initialized");
    }
    
    void Update()
    {
        if (!hasFoundComponents) return;
        
        CheckPlayerMovement();
    }
    
    void CheckPlayerMovement()
    {
        Vector3 currentPos = player.position;
        float moveDistance = Vector3.Distance(currentPos, lastPlayerPos);
        
        bool isMoving = moveDistance > movementThreshold;
        
        if (isMoving)
        {
            stopTimer = 0f;
            if (isDashboardVisible)
            {
                Debug.Log("DashboardController: Player moving - hiding dashboard");
                HideDashboard();
            }
        }
        else
        {
            stopTimer += Time.deltaTime;
            if (!isDashboardVisible && stopTimer >= stopDetectionTime)
            {
                Debug.Log("DashboardController: Player stopped for " + stopTimer + "s - showing dashboard");
                ShowDashboard();
            }
        }
        
        lastPlayerPos = currentPos;
    }
    
    public void ShowDashboard()
    {
        if (!hasFoundComponents || isDashboardVisible) return;
        
        Debug.Log("DashboardController: Showing dashboard");
        
        canvas.enabled = true;
        panel.SetActive(true);
        isDashboardVisible = true;
        
        Debug.Log("DashboardController: Dashboard is now visible");
    }
    
    public void HideDashboard()
    {
        if (!hasFoundComponents || !isDashboardVisible) return;
        
        Debug.Log("DashboardController: Hiding dashboard");
        
        canvas.enabled = false;
        panel.SetActive(false);
        isDashboardVisible = false;
    }
    
    // Test methods
    [ContextMenu("Force Show")]
    public void ForceShow()
    {
        ShowDashboard();
    }
    
    [ContextMenu("Force Hide")]
    public void ForceHide()
    {
        HideDashboard();
    }
}