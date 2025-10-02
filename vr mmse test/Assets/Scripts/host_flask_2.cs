using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System;

public class AudioToServerSender : MonoBehaviour
{
    // ⚠️ 請確保在 Inspector 中連結這些變數
    public string serverUrl = "http://127.0.0.1:5000/recognize_speech";
    public TextMeshProUGUI statusText;
    public AnswerLogicManager answerManager; // 這是確保比對邏輯能執行的關鍵！
    
    // 伺服器回傳的 JSON 結構（必須與 Python 端回傳的 {"transcription":"..."} 或 {"error":"..."} 相符）
    [System.Serializable]
    public class RecognitionResponse
    {
        public string transcription;
        public string error;
    }

    /// <summary>
    /// 公有方法：由 RecordingState2 呼叫，啟動音訊傳輸協程。
    /// </summary>
    /// <param name="audioData">WAV 格式的音訊原始位元組數組。</param>
    /// <param name="questionSequenceIndex">當前問題的索引。</param>
    public void SendAudioForRecognition(byte[] audioData, int questionSequenceIndex)
    {
        // 這是 RecordingState2 腳本正確呼叫的方法
        StartCoroutine(SendAudioToServer(audioData, questionSequenceIndex));
    }

    IEnumerator SendAudioToServer(byte[] audioData, int questionSequenceIndex)
    {
        // 預設為空字串。如果辨識失敗或結果為空，它將以空字串傳遞給 CheckAnswer。
        string userResponse = string.Empty;

        if (statusText) statusText.text = "正在進行語音辨識...";

        WWWForm form = new WWWForm();
        form.AddField("question_index", questionSequenceIndex.ToString());
        form.AddBinaryData("file", audioData, "recording.wav", "audio/wav");

        UnityWebRequest request = UnityWebRequest.Post(serverUrl, form);
        request.timeout = 30;
        yield return request.SendWebRequest();


        if (request.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = request.downloadHandler.text;
            Debug.Log("[AudioSender] 伺服器回應 (200 OK): " + jsonResponse);

            try
            {
                RecognitionResponse response = JsonUtility.FromJson<RecognitionResponse>(jsonResponse);

                if (response != null && !string.IsNullOrEmpty(response.transcription))
                {
                    // 辨識成功，設定回傳結果
                    userResponse = response.transcription;
                    if (statusText) statusText.text = $"辨識成功：{userResponse}";
                    // 🔹 呼叫答案檢查
                    if (answerManager != null)
                    {
                        answerManager.CheckAnswer(userResponse, questionSequenceIndex);

                        // --- 🔹 答題完成後自動轉場 ---
                        if (SceneFlowManager.instance != null)
                        {
                            // 這裡你可以選擇：不管答對/答錯都轉場
                            StartCoroutine(LoadNextSceneWithDelay(2f));

                            // 或者 → 只有答對才轉場（註解掉上面，改用這個）
                            // if (similarity >= 0.50f) StartCoroutine(LoadNextSceneWithDelay(2f));
                        }
                    }
                }
                else
                {
                    // 雖然請求成功 (200)，但 JSON 結構中沒有 transcription，可能是伺服器設計問題
                    if (statusText) statusText.text = "辨識回傳內容無效。";
                    Debug.LogWarning("[AudioSender] 伺服器回傳無效內容。");
                }
            }
            catch (System.Exception ex)
            {
                if (statusText) statusText.text = "解析回傳 JSON 失敗";
                Debug.LogError("[AudioSender] 解析 JSON 失敗: " + ex.Message);
            }
        }
        else // 請求失敗，可能是 400, 500, 或網路錯誤
        {
            // 處理 400 Bad Request (通常是辨識服務無法理解語音)
            string jsonResponse = request.downloadHandler.text;

            try
            {
                RecognitionResponse response = JsonUtility.FromJson<RecognitionResponse>(jsonResponse);

                // 伺服器回傳 400/500 但帶有 JSON 錯誤訊息
                if (response != null && !string.IsNullOrEmpty(response.error))
                {
                    if (statusText) statusText.text = $"辨識錯誤：{response.error}";
                    Debug.LogWarning($"[AudioSender] 伺服器回傳辨識錯誤: {response.error}");
                    // userResponse 保持為空字串
                }
                else
                {
                    // 網路或通訊協定錯誤
                    if (statusText) statusText.text = "語音辨識請求失敗";
                    Debug.LogError($"[AudioSender] 語音辨識請求失敗: {request.error}. Response: {jsonResponse}");
                }
            }
            catch
            {
                // 無法解析 JSON，可能是純文字錯誤訊息
                if (statusText) statusText.text = "語音辨識請求失敗";
                Debug.LogError($"[AudioSender] 語音辨識請求失敗: {request.error}. Response: {jsonResponse}");
            }
        }

        // 🚀 關鍵修復點：在協程結束時，統一呼叫答案比對邏輯
        Debug.Log($"[AudioSender] 傳輸結束。準備檢查答案。辨識結果: '{userResponse}'");

        if (answerManager != null)
        {
            // userResponse 會是辨識結果，或在失敗時為 string.Empty
            answerManager.CheckAnswer(userResponse, questionSequenceIndex);
        }
        else
        {
            Debug.LogError("[AudioSender] AnswerManager 未設定！無法檢查答案。");
        }
    }
     // --- 🔹 延遲換場方法 ---
    private IEnumerator LoadNextSceneWithDelay(float delay)
    {
    // if (statusText != null)
    // {
    //     statusText.text += "\n即將進入下一關...";
    // }
    yield return new WaitForSeconds(delay);
    SceneFlowManager.instance.LoadNextScene();
    }
}
