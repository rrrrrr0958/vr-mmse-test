using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.IO;

public class HostFlask2 : MonoBehaviour
{
    public TextMeshProUGUI statusText;
    public string serverUrl = "http://localhost:5000/transcribe";
    [HideInInspector] public string targetSentence;

    [Header("自動換場設定")]
    public float autoAdvanceDelay = 2f;  // 換場前延遲秒數

    public void SendFileToWhisper(string path)
    {
        StartCoroutine(SendToWhisper(path));
    }

    IEnumerator SendToWhisper(string path)
    {
        if (!File.Exists(path))
        {
            if (statusText) statusText.text = "錯誤：錄音檔不存在";
            yield break;
        }

        byte[] audioBytes = File.ReadAllBytes(path);
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioBytes, Path.GetFileName(path), "audio/wav");
        form.AddField("target", targetSentence);

        using (UnityWebRequest www = UnityWebRequest.Post(serverUrl, form))
        {
            if (statusText) statusText.text = "上傳中…";
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                if (statusText) statusText.text = "辨識錯誤: " + www.error;
            }
            else
            {
                string json = www.downloadHandler.text;
                MyTranscriptionResult res = JsonUtility.FromJson<MyTranscriptionResult>(json);

                if (res == null)
                {
                    if (statusText) statusText.text = "回傳解析失敗: " + json;
                }
                else
                {
                    if (statusText)
                    {
                        statusText.text = $"題目：{targetSentence}\n" +
                                          $"辨識：{res.spoken_text}\n" +
                                          $"正確率：{res.accuracy:0.00}%\n" +
                                          $"通過：{(res.passed ? "是" : "否")}";
                    }
                }
            }

            // ★★★ 無論成功或失敗，這裡都會進入換場 ★★★
            StartCoroutine(AdvanceNextAfterDelay(autoAdvanceDelay));
        }
    }

    private IEnumerator AdvanceNextAfterDelay(float delay)
    {
        if (delay > 0f) yield return new WaitForSeconds(delay);

        if (SceneFlowManager.instance != null)
        {
            SceneFlowManager.instance.LoadNextScene();
        }
        else
        {
            Debug.LogError("[HostFlask2] SceneFlowManager.instance 為 null，請確認第一個場景有放 SceneFlowManager 物件");
        }
    }
}
