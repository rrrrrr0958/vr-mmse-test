using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class AsrClient : MonoBehaviour
{
    [Header("Flask endpoint")]
    
    public string serverUrl = "http://127.0.0.1:5003/score";

    [Serializable] public class Reasons { public bool has_subject_verb; public bool understandable; }

    // 同時支援 /score（transcript、score、reasons）與 /recognize_speech（transcription）
    [Serializable]
    public class GoogleASRResponse
    {
        public string transcript;       // for /score
        public string transcription;    // for /recognize_speech
        public int score;               // for /score
        public Reasons reasons;         // for /score
        public string error;            // 錯誤訊息（若有）

        // 統一取得文字
        public string Text => !string.IsNullOrEmpty(transcript) ? transcript : transcription;
    }

    public IEnumerator UploadWav(
        byte[] wavBytes,
        Action<GoogleASRResponse> onDone,
        Action<string> onError,
        Action<string, float> onProgress = null)
    {
        if (string.IsNullOrEmpty(serverUrl))
        {
            onError?.Invoke("Server URL is empty");
            yield break;
        }

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavBytes, "record.wav", "audio/wav");

        using (UnityWebRequest req = UnityWebRequest.Post(serverUrl, form))
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            onProgress?.Invoke("連線中", 0f);

            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                float p = req.uploadProgress > 0f ? req.uploadProgress :
                          req.downloadProgress > 0f ? req.downloadProgress : 0.05f;
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
                var resp = JsonUtility.FromJson<GoogleASRResponse>(json);
                if (resp == null) { onError?.Invoke("Empty/Invalid JSON"); yield break; }
                if (!string.IsNullOrEmpty(resp.error)) { onError?.Invoke(resp.error); yield break; }

                // 正規化：若只有 transcription，補到 transcript，score 預設 0
                if (string.IsNullOrEmpty(resp.transcript) && !string.IsNullOrEmpty(resp.transcription))
                {
                    resp.transcript = resp.transcription;
                    if (resp.reasons == null) resp.reasons = new Reasons();
                    // 保留 score=0（純辨識端點沒有評分）
                }

                onDone?.Invoke(resp);
            }
            catch (Exception ex)
            {
                onError?.Invoke($"JSON parse failed: {ex.Message}");
            }
        }
    }
}
