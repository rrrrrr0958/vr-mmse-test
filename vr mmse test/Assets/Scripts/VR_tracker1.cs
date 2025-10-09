using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.XR;
using UnityEngine.SceneManagement;
using System;

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

    [Header("Server Settings")]
    public string serverUrl = "http://127.0.0.1:5002/upload_csv";

    private float startTime;
    private bool isRecording = false;

    void Awake()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        StartRecording();
    }

    void Update()
    {
        if (!isRecording) return;

        // 記錄右手
        TrackHand(rightHand, rightHandPositions, rightTriggerPressed, XRNode.RightHand);

        // 記錄左手
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

        // 可選：根據頭部前方向微調
        Vector3 forward = Camera.main.transform.forward;
        forward.y = 0;
        pos += forward.normalized * 0.05f;

        positions.Add(pos);

        // 觸發器狀態
        bool pressed = false;
        InputDevice device = InputDevices.GetDeviceAtXRNode(node);
        if (device.isValid)
            device.TryGetFeatureValue(CommonUsages.triggerButton, out pressed);

        triggers.Add(pressed ? 1 : 0);
    }

    /// <summary>
    /// 儲存並上傳 CSV，檔名自動時間戳記
    /// </summary>
    public void SaveAndUploadTrajectory()
    {
        string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fileName = $"draw_session_{timestamp}.csv";

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

        StartCoroutine(UploadCSV(csvData, fileName));

        // 清空緩存，準備下一次紀錄
        rightHandPositions.Clear();
        leftHandPositions.Clear();
        rightTriggerPressed.Clear();
        leftTriggerPressed.Clear();
        timestamps.Clear();
        isRecording = false;
    }

    private IEnumerator UploadCSV(string csvContent, string fileName)
    {
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(csvContent);
        UnityWebRequest request = new UnityWebRequest(serverUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "text/csv");
        request.SetRequestHeader("File-Name", fileName);

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
            Debug.Log($"CSV uploaded successfully! File: {fileName}");
        else
            Debug.LogError("Upload failed: " + request.error);
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
