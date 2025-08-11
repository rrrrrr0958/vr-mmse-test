using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.IO;

public class HostFlask2 : MonoBehaviour
{
    public TextMeshProUGUI statusText;
    public string serverUrl = "http://localhost:5000/transcribe";
    public string targetSentence = "魚肉特價快來買";

    public void SendFileToWhisper(string path)
    {
        StartCoroutine(SendToWhisper(path));
    }

    IEnumerator SendToWhisper(string path)
    {
        if (!File.Exists(path))
        {
            statusText.text = "錯誤：錄音檔不存在";
            yield break;
        }

        byte[] audioBytes = File.ReadAllBytes(path);
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioBytes, Path.GetFileName(path), "audio/wav");
        form.AddField("target", targetSentence);

        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, form))
        {
            statusText.text = "上傳中…";
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                statusText.text = "辨識錯誤: " + www.error;
            }
            else
            {
                string json = www.downloadHandler.text;
                MyTranscriptionResult res = JsonUtility.FromJson<MyTranscriptionResult>(json);

                if (res == null)
                {
                    statusText.text = "回傳解析失敗: " + json;
                }
                else
                {
                    statusText.text = $"辨識：{res.spoken_text}\n" +
                                      $"正確率：{res.accuracy:0.00}%\n" +
                                      $"通過：{(res.passed ? "是" : "否")}";
                }
            }
        }
    }
}
