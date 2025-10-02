using UnityEngine;
using System.Collections.Generic;
using System.IO;

public class VRTracker : MonoBehaviour
{
    [Header("Tracking Targets")]
    public Transform head;
    public Transform leftHand;
    public Transform rightHand;

    private List<Vector3> headPositions = new List<Vector3>();
    private List<Vector3> leftHandPositions = new List<Vector3>();
    private List<Vector3> rightHandPositions = new List<Vector3>();

    private LineRenderer headLine;
    private LineRenderer leftHandLine;
    private LineRenderer rightHandLine;

    void Start()
    {
        // 建立三個 LineRenderer 分別畫頭部、左右手
        headLine = CreateLineRenderer(Color.blue, "HeadPath");
        leftHandLine = CreateLineRenderer(Color.green, "LeftHandPath");
        rightHandLine = CreateLineRenderer(Color.red, "RightHandPath");
    }

    void Update()
    {
        TrackPosition(head, headPositions, headLine);
        TrackPosition(leftHand, leftHandPositions, leftHandLine);
        TrackPosition(rightHand, rightHandPositions, rightHandLine);
    }

    private void TrackPosition(Transform target, List<Vector3> positions, LineRenderer line)
    {
        if (target == null) return;

        // 記錄當前座標
        positions.Add(target.position);

        // 更新 LineRenderer
        line.positionCount = positions.Count;
        line.SetPositions(positions.ToArray());
    }

    private LineRenderer CreateLineRenderer(Color color, string name)
    {
        GameObject lineObj = new GameObject(name);
        lineObj.transform.SetParent(transform); // 跟隨 Manager
        LineRenderer lr = lineObj.AddComponent<LineRenderer>();
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startWidth = 0.01f;
        lr.endWidth = 0.01f;
        lr.positionCount = 0;
        lr.startColor = color;
        lr.endColor = color;
        return lr;
    }

    // 可以在遊戲結束時呼叫，將路徑存檔
    public void SaveTrajectory(string fileName)
    {
        string folderPath = Application.dataPath + "/TrajectoryData"; 
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        string path = folderPath + "/" + fileName + ".csv";

        using (StreamWriter sw = new StreamWriter(path))
        {
            sw.WriteLine("Type,X,Y,Z");

            foreach (var pos in headPositions)
                sw.WriteLine($"Head,{pos.x},{pos.y},{pos.z}");

            foreach (var pos in leftHandPositions)
                sw.WriteLine($"LeftHand,{pos.x},{pos.y},{pos.z}");

            foreach (var pos in rightHandPositions)
                sw.WriteLine($"RightHand,{pos.x},{pos.y},{pos.z}");
        }

        Debug.Log("Trajectory saved at: " + path);
    }

}
