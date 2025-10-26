using UnityEngine;
using System.Collections.Generic;
using UnityEngine.XR;
using UnityEngine.SceneManagement;
using System;
using System.IO;

public class VRTracker1 : MonoBehaviour
{
    [Header("Tracking Targets")]
    public Transform rightHand;
    public Transform leftHand;

    private List<Vector3> rightHandPositions = new List<Vector3>();
    private List<Vector3> leftHandPositions = new List<Vector3>();
    private List<float> timestamps = new List<float>();
    private List<int> rightTriggerPressed = new List<int>();
    private List<int> leftTriggerPressed = new List<int>();

    [Header("Save Settings")]
    // 建議設成同一個被 Python 讀得到的資料夾，例如：C:\mmse\results
    public string saveFolder = "csv_results";

    private float startTime;
    private bool isRecording = false;

    void Awake() => SceneManager.sceneLoaded += OnSceneLoaded;
    void OnDestroy() => SceneManager.sceneLoaded -= OnSceneLoaded;

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => StartRecording();

    void Update()
    {
        if (!isRecording) return;
        TrackHand(rightHand, rightHandPositions, rightTriggerPressed, XRNode.RightHand);
        TrackHand(leftHand, leftHandPositions, leftTriggerPressed, XRNode.LeftHand);
        timestamps.Add(Time.time - startTime);
    }

    public void StartRecording()
    {
        if (isRecording) return;
        isRecording = true;
        startTime = Time.time;
        Debug.Log("Hand tracking started.");
    }

    private void TrackHand(Transform hand, List<Vector3> positions, List<int> triggers, XRNode node)
    {
        if (hand == null) return;
        Vector3 pos = hand.position;

        // 可選微調：往頭部前方向位移 5cm
        Vector3 forward = Camera.main ? Camera.main.transform.forward : Vector3.forward;
        forward.y = 0;
        pos += forward.normalized * 0.05f;

        positions.Add(pos);

        bool pressed = false;
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid) device.TryGetFeatureValue(CommonUsages.triggerButton, out pressed);
        triggers.Add(pressed ? 1 : 0);
    }

    /// <summary>手動或流程結束時呼叫，將目前緩衝寫入 CSV 檔</summary>
    public string SaveTrajectoryToCsv()
    {
        // ✅ 若是相對路徑，轉成絕對實際路徑
        string folderPath = Path.Combine(Application.dataPath, "Scripts", saveFolder);
        Directory.CreateDirectory(folderPath);

        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"draw_session_{timestamp}.csv";
        string filePath = Path.Combine(folderPath, fileName);

        string csvData = "Type,X,Y,Z,Time,TriggerPressed\n";
        int count = timestamps.Count;
        for (int i = 0; i < count; i++)
        {
            if (i < rightHandPositions.Count)
            {
                var pos = rightHandPositions[i];
                csvData += $"RightHand,{pos.x},{pos.y},{pos.z},{timestamps[i]},{rightTriggerPressed[i]}\n";
            }

            if (i < leftHandPositions.Count)
            {
                var pos = leftHandPositions[i];
                csvData += $"LeftHand,{pos.x},{pos.y},{pos.z},{timestamps[i]},{leftTriggerPressed[i]}\n";
            }
        }

        File.WriteAllText(filePath, csvData);
        Debug.Log($"✅ CSV saved at: {filePath}");

        // 清空緩衝
        rightHandPositions.Clear();
        leftHandPositions.Clear();
        rightTriggerPressed.Clear();
        leftTriggerPressed.Clear();
        timestamps.Clear();
        isRecording = false;

        // ✅ 回傳 CSV 檔案的完整路徑
        return filePath;
    }
    public void ClearCurrentCSVData()
    {
        rightHandPositions.Clear();
        leftHandPositions.Clear();
        rightTriggerPressed.Clear();
        leftTriggerPressed.Clear();
        timestamps.Clear();
        Debug.Log("Current CSV tracking data cleared."); 
    }
}



