// AsrClient_13
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
        Action<string, float> onProgress = null) // 參數保留，但內部不呼叫
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
            // 不再回報任何進度狀態（onProgress 不會被呼叫）

            var op = req.SendWebRequest();
            while (!op.isDone)
            {
                // 純等待，不顯示「連線中/傳輸中/完成」
                yield return null;
            }

            if (req.result != UnityWebRequest.Result.Success)
            {
                string errorMsg = $"{req.responseCode} {req.error} {req.downloadHandler.text}";
                onError?.Invoke(errorMsg);
                yield break;
            }

            try
            {
                var json = req.downloadHandler.text;

                if (string.IsNullOrWhiteSpace(json))
                    throw new Exception("Received empty or whitespace JSON response from server.");

                var resp = JsonUtility.FromJson<GoogleASRResponse>(json);
                if (resp == null) { onError?.Invoke("Empty/Invalid JSON"); yield break; }
                if (!string.IsNullOrEmpty(resp.error)) { onError?.Invoke(resp.error); yield break; }

                // 正規化：若只有 transcription，補到 transcript，score 預設 0
                if (string.IsNullOrEmpty(resp.transcript) && !string.IsNullOrEmpty(resp.transcription))
                {
                    resp.transcript = resp.transcription;
                    if (resp.reasons == null) resp.reasons = new Reasons();
                }

                onDone?.Invoke(resp); // ✅ 成功時交給呼叫端顯示「錄音完成」
            }
            catch (Exception ex)
            {
                string errorMsg = $"JSON parse failed: {ex.Message}";
                onError?.Invoke(errorMsg);
            }
        }
    }
}
