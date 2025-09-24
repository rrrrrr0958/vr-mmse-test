using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[Serializable]
public class ScoreResponse {
    public string transcript;
    public int score;
    public Reason reasons;
}
[Serializable]
public class Reason {
    public bool has_subject_verb;
    public bool understandable;
}

public class AsrClient : MonoBehaviour
{
    [Header("Server")]
    public string serverUrl = "http://YOUR-IP:8000/score"; // ← 改成你的後端位址

    public IEnumerator PostWav(byte[] wavBytes, Action<ScoreResponse> onDone, Action<string> onError)
    {
        WWWForm form = new WWWForm();
        form.AddField("lang", "zh");
        form.AddBinaryData("audio", wavBytes, "audio.wav", "audio/wav");

        using (UnityWebRequest req = UnityWebRequest.Post(serverUrl, form))
        {
            req.timeout = 30;
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke(req.error);
                yield break;
            }
            try
            {
                var json = req.downloadHandler.text;
                var resp = JsonUtility.FromJson<ScoreResponse>(json);
                onDone?.Invoke(resp);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
            }
        }
    }
}
