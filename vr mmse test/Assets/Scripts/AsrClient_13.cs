using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

[Serializable] public class ScoreReasons { public bool has_subject_verb; public bool understandable; }
[Serializable] public class ScoreResponse { public string transcript; public int score; public ScoreReasons reasons; }
// for /recognize_speech only:
// [Serializable] class WrapTrans { public string transcription; }

public class AsrClient : MonoBehaviour
{
    [Header("Flask endpoint")]
    public string serverUrl = "http://192.168.1.100:5000/score"; // 換成你的IP

    public IEnumerator UploadWav(byte[] wavBytes,
        Action<ScoreResponse> onDone,
        Action<string> onError,
        Action<string,float> onProgress = null)
    {
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavBytes, "record.wav", "audio/wav");

        using (var req = UnityWebRequest.Post(serverUrl, form))
        {
            req.timeout = 30;
            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                float p = req.uploadProgress > 0 ? req.uploadProgress :
                          req.downloadProgress > 0 ? req.downloadProgress : 0f;
                onProgress?.Invoke("傳輸中", Mathf.Clamp01(p));
                yield return null;
            }
            onProgress?.Invoke("完成", 1f);

            if (req.result != UnityWebRequest.Result.Success)
            {
                onError?.Invoke($"{req.responseCode} {req.error} {req.downloadHandler.text}");
                yield break;
            }

            try
            {
                var json = req.downloadHandler.text;
                var resp = JsonUtility.FromJson<ScoreResponse>(json);

                // 如果你打的是 /recognize_speech（只回 transcription CL），改用：
                // var wrap = JsonUtility.FromJson<WrapTrans>(json);
                // var resp = new ScoreResponse { transcript = wrap.transcription, score = 0, reasons = new ScoreReasons() };

                onDone?.Invoke(resp);
            }
            catch (Exception ex)
            {
                onError?.Invoke(ex.Message);
            }
        }
    }
}
