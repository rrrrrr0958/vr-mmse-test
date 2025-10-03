using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Collections;
using UnityEngine.XR;
using UnityEngine.SceneManagement;

public class VRTracker : MonoBehaviour
{
    [Header("Tracking Target")]
    public Transform rightHand;

    private List<Vector3> rightHandPositions = new List<Vector3>();
    private List<float> timestamps = new List<float>();
    private List<int> triggerPressed = new List<int>();

    [Header("Server Settings")]
    public string serverUrl = "http://127.0.0.1:5001/upload_csv";

    private float startTime;
    private bool isRecording = false;

    void Awake()
    {
        // 訂閱場景加載完成事件
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        // 場景加載完成自動開始紀錄
        StartRecording();
    }

    void Update()
    {
        if (!isRecording) return;

        TrackRightHand(rightHand);

        timestamps.Add(Time.time - startTime);
        triggerPressed.Add(CheckTriggerPressed() ? 1 : 0);
    }

    public void StartRecording()
    {
        if (isRecording) return;

        isRecording = true;
        startTime = Time.time;
        Debug.Log("Right hand tracking started.");
    }

    private void TrackRightHand(Transform target)
    {
        if (target == null) return;

        Vector3 pos = target.position;

        // 玩家正面視角，rotation=(0,180,0)
        // 可以加微偏移讓頭部/手部小動作更明顯
        Vector3 forward = Camera.main.transform.forward;
        forward.y = 0;
        pos += forward.normalized * 0.05f;

        rightHandPositions.Add(pos);
    }

    private bool CheckTriggerPressed()
    {
        bool rightTrigger = false;
        InputDevice rightDevice = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        if (rightDevice.isValid)
            rightDevice.TryGetFeatureValue(CommonUsages.triggerButton, out rightTrigger);
        return rightTrigger;
    }

    public void SaveAndUploadTrajectory(string fileName)
    {
        string csvData = "Type,X,Y,Z,Time,TriggerPressed\n";

        for (int i = 0; i < rightHandPositions.Count; i++)
        {
            var pos = rightHandPositions[i];
            csvData += $"RightHand,{pos.x},{pos.y},{pos.z},{timestamps[i]},{triggerPressed[i]}\n";
        }

        StartCoroutine(UploadCSV(csvData, fileName));
    }

    private IEnumerator UploadCSV(string csvContent, string fileName)
    {
        byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(csvContent);
        UnityWebRequest request = new UnityWebRequest(serverUrl, "POST");
        request.uploadHandler = new UploadHandlerRaw(bodyRaw);
        request.downloadHandler = new DownloadHandlerBuffer();
        request.SetRequestHeader("Content-Type", "text/csv");
        request.SetRequestHeader("File-Name", fileName + ".csv");

        yield return request.SendWebRequest();

        if (request.result == UnityWebRequest.Result.Success)
            Debug.Log("CSV uploaded successfully!");
        else
            Debug.LogError("Upload failed: " + request.error);
    }
}
