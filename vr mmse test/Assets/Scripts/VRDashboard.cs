using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class VRDashboard : MonoBehaviour
{
    public GameObject chartContainer;
    public GameObject gamesContainer;
    
    void Start()
    {
        CreateDashboardLayout();
    }
    
    void CreateDashboardLayout()
    {
        // Dashboard will be setup in Unity Editor
        Debug.Log("VR Dashboard Initialized");
    }
}