using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System;

// 注意：這裡不再定義 RecognitionResponse，它會從你的 RecognitionResponse_3_4_5.cs 引用

public class AudioToServerSender : MonoBehaviour
{
    // 伺服器 URL 使用第一個參考程式碼的路由 /recognize_speech
    public string serverUrl = "http://localhost:5000/recognize_speech";

    [Header("UI & 邏輯連接")]
    public TextMeshProUGUI statusText; 
    public AnswerLogicManager answerManager;

    // 啟動傳送協程的方法
    public void SendAudioForRecognition(byte[] audioData, int questionSequenceIndex)
    {
        StartCoroutine(SendAudioToServer(audioData, questionSequenceIndex));
    }

    IEnumerator SendAudioToServer(byte[] audioData, int questionSequenceIndex)
    {
        if (statusText) statusText.text = "正在進行語音辨識...";

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", audioData, "recording.wav", "audio/wav");

        UnityWebRequest request = UnityWebRequest.Post(serverUrl, form);
        request.timeout = 30; // 設置超時時間
        yield return request.SendWebRequest();

        string userResponse = string.Empty;

        if (request.result == UnityWebRequest.Result.Success)
        {
            string jsonResponse = request.downloadHandler.text;
            Debug.Log("[AudioSender] 伺服器回應: " + jsonResponse);

            try
            {
                // 直接使用 RecognitionResponse 類別 (來自 RecognitionResponse_3_4_5.cs)
                RecognitionResponse response = JsonUtility.FromJson<RecognitionResponse>(jsonResponse);

                if (response != null && !string.IsNullOrEmpty(response.transcription))
                {
                    userResponse = response.transcription;
                    if (statusText) statusText.text = $"辨識結果：{userResponse}";

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
                // 檢查是否有錯誤欄位
                else if (response != null && !string.IsNullOrEmpty(response.error))
                {
                    if (statusText) statusText.text = $"辨識錯誤：{response.error}";
                    Debug.LogWarning($"[AudioSender] 伺服器回傳辨識錯誤: {response.error}");
                    userResponse = string.Empty;
                }
                else
                {
                    if (statusText) statusText.text = "辨識回傳解析失敗";
                    Debug.LogError("[AudioSender] 解析 JSON 失敗: 無效的結構或內容。");
                }
            }
            catch (System.Exception ex)
            {
                if (statusText) statusText.text = "辨識回傳解析失敗";
                Debug.LogError("[AudioSender] 解析 JSON 失敗: " + ex.Message);
            }
        }
        else
        {
            if (statusText) statusText.text = "語音辨識請求失敗";
            Debug.LogError($"[AudioSender] 語音辨識請求失敗: {request.error}. Response: {request.downloadHandler.text}");
        }

        // 這裡進行下一步答題邏輯
        Debug.Log($"[AudioSender] 傳輸結束。辨識結果: {userResponse}");
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
