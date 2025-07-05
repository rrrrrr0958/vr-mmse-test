using UnityEngine;
using UnityEditor;
using System.Collections;

public class AutoWalkTest : MonoBehaviour
{
    private AutoWalkToEntrance walkController;
    private Transform xrOrigin;
    private Camera mainCamera;
    private Vector3 initialPosition;
    private float testStartTime;
    private bool testRunning = false;
    
    [Header("Test Results")]
    public bool testPassed = false;
    public string testStatus = "Not Started";
    public float distanceToTarget = 0f;
    public float timeTaken = 0f;
    
    void Start()
    {
        StartCoroutine(RunTest());
    }
    
    IEnumerator RunTest()
    {
        yield return new WaitForSeconds(0.5f);
        
        Debug.Log("=== Starting Auto Walk Test ===");
        testStatus = "Initializing...";
        
        // 1. Check WalkController exists
        GameObject walkControllerObj = GameObject.Find("WalkController");
        if (walkControllerObj == null)
        {
            testStatus = "FAILED: WalkController not found";
            Debug.LogError(testStatus);
            yield break;
        }
        
        // 2. Check AutoWalkToEntrance script
        walkController = walkControllerObj.GetComponent<AutoWalkToEntrance>();
        if (walkController == null)
        {
            testStatus = "FAILED: AutoWalkToEntrance script not found";
            Debug.LogError(testStatus);
            yield break;
        }
        
        // 3. Check XR Origin
        GameObject xrOriginObj = GameObject.Find("XR Origin (XR Rig)");
        if (xrOriginObj != null)
        {
            xrOrigin = xrOriginObj.transform;
            mainCamera = xrOriginObj.GetComponentInChildren<Camera>();
            initialPosition = xrOrigin.position;
            Debug.Log("XR Origin found at position: " + initialPosition);
        }
        else
        {
            testStatus = "FAILED: XR Origin not found";
            Debug.LogError(testStatus);
            yield break;
        }
        
        if (mainCamera == null)
        {
            testStatus = "FAILED: No camera found in XR Origin";
            Debug.LogError(testStatus);
            yield break;
        }
        
        // 4. Check target object
        GameObject target = GameObject.Find("Zoo_Entrance_Illustra_0618121213_texture (1)");
        if (target == null)
        {
            testStatus = "FAILED: Target Zoo_Entrance not found";
            Debug.LogError(testStatus);
            yield break;
        }
        
        Vector3 targetPosition = new Vector3(target.transform.position.x, 0, target.transform.position.z);
        Debug.Log("Target position: " + targetPosition);
        
        // 5. Start test
        testStatus = "Test Running...";
        testRunning = true;
        testStartTime = Time.time;
        
        // Monitor movement
        float lastDistance = float.MaxValue;
        float noProgressTime = 0f;
        bool hasStartedMoving = false;
        Vector3 lastPosition = xrOrigin.position;
        
        while (testRunning)
        {
            Vector3 currentPos = new Vector3(xrOrigin.position.x, 0, xrOrigin.position.z);
            distanceToTarget = Vector3.Distance(currentPos, targetPosition);
            timeTaken = Time.time - testStartTime;
            
            // Check if started moving
            if (!hasStartedMoving && Vector3.Distance(xrOrigin.position, lastPosition) > 0.1f)
            {
                hasStartedMoving = true;
                Debug.Log("Movement detected!");
            }
            lastPosition = xrOrigin.position;
            
            // Check progress
            if (hasStartedMoving && distanceToTarget >= lastDistance - 0.01f)
            {
                noProgressTime += Time.deltaTime;
                if (noProgressTime > 3f)
                {
                    testStatus = "FAILED: No progress for 3 seconds";
                    testRunning = false;
                    break;
                }
            }
            else
            {
                noProgressTime = 0f;
            }
            lastDistance = distanceToTarget;
            
            // Check if arrived
            if (distanceToTarget < 2.5f)
            {
                testStatus = "SUCCESS: Reached Zoo Entrance!";
                testPassed = true;
                testRunning = false;
                break;
            }
            
            // Timeout check
            if (timeTaken > 30f)
            {
                testStatus = "FAILED: Timeout (30 seconds)";
                testRunning = false;
                break;
            }
            
            yield return null;
        }
        
        // Output test results
        Debug.Log("=== Test Results ===");
        Debug.Log("Status: " + testStatus);
        Debug.Log("Time Taken: " + timeTaken.ToString("F2") + " seconds");
        Debug.Log("Final Distance to Target: " + distanceToTarget.ToString("F2") + " meters");
        Debug.Log("Test Passed: " + testPassed);
        Debug.Log("Movement Started: " + hasStartedMoving);
        
        // Debugging suggestions
        if (!testPassed)
        {
            Debug.Log("\n=== Debugging Suggestions ===");
            if (!hasStartedMoving)
            {
                Debug.Log("- Character never started moving. Check AutoWalkToEntrance script.");
            }
            else if (distanceToTarget > 10f)
            {
                Debug.Log("- Character moved but didn't get close to target.");
            }
        }
    }
    
    void OnGUI()
    {
        GUI.Box(new Rect(10, 10, 300, 120), "Auto Walk Test");
        GUI.Label(new Rect(20, 30, 280, 20), "Status: " + testStatus);
        GUI.Label(new Rect(20, 50, 280, 20), "Distance to Target: " + distanceToTarget.ToString("F2") + "m");
        GUI.Label(new Rect(20, 70, 280, 20), "Time: " + timeTaken.ToString("F2") + "s");
        GUI.Label(new Rect(20, 90, 280, 20), "Test Passed: " + testPassed);
    }
}