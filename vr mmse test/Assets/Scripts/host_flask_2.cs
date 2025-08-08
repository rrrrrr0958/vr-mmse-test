using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System.IO;

public class HostFlask2 : MonoBehaviour
{
    public TextMeshProUGUI statusText;

    public void SendFileToWhisper(string path)
    {
        StartCoroutine(SendToWhisper(path));
    }

    IEnumerator SendToWhisper(string path)
    {
        byte[] audioBytes = File.ReadAllBytes(path);
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioBytes, Path.GetFileName(path), "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post("http://localhost:5000/transcribe", form))
        {
            yield return www.SendWebRequest();

            if (www.result != UnityWebRequest.Result.Success)
            {
                statusText.text = "辨識錯誤: " + www.error;
            }
            else
            {
                string playerSpeech = www.downloadHandler.text;
                string targetSentence = "魚肉特價快來買";
                float score = CompareTexts(targetSentence, playerSpeech);
                statusText.text = $"辨識結果：{playerSpeech}\n相似度：{score * 100f:0.0}%";
            }
        }
    }

    float CompareTexts(string original, string player)
    {
        int distance = LevenshteinDistance(original, player);
        return 1f - (float)distance / Mathf.Max(original.Length, player.Length);
    }

    int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b.Length;
        if (string.IsNullOrEmpty(b)) return a.Length;

        int[,] costs = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++) costs[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) costs[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = (a[i - 1] == b[j - 1]) ? 0 : 1;
                costs[i, j] = Mathf.Min(
                    costs[i - 1, j] + 1,
                    Mathf.Min(costs[i, j - 1] + 1, costs[i - 1, j - 1] + cost)
                );
            }
        }

        return costs[a.Length, b.Length];
    }
}
