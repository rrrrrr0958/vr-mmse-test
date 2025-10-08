using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using TMPro;
using System;

public class AudioToServerSender : MonoBehaviour
{
    public string serverUrl = "http://localhost:5000/recognize_speech";
    public TextMeshProUGUI statusText;
    public AnswerLogicManager answerManager;

    public void SendAudioForRecognition(byte[] audioData, int questionSequenceIndex)
    {
        StartCoroutine(SendAudioToServer(audioData, questionSequenceIndex));
    }

    IEnumerator SendAudioToServer(byte[] audioData, int questionSequenceIndex)
    {
        string userResponse = string.Empty;

        if (statusText) statusText.text = "正在進行語音辨識...";

        WWWForm form = new WWWForm();
        form.AddField("question_index", questionSequenceIndex.ToString());
        form.AddBinaryData("file", audioData, "recording.wav", "audio/wav");

        UnityWebRequest request = UnityWebRequest.Post(serverUrl, form);
        request.timeout = 30;
        yield return request.SendWebRequest();

        try
        {
            if (request.result == UnityWebRequest.Result.Success)
            {
                string jsonResponse = request.downloadHandler.text;
                Debug.Log("[AudioSender] 伺服器回應 (200 OK): " + jsonResponse);

                try
                {
                    RecognitionResponse response = JsonUtility.FromJson<RecognitionResponse>(jsonResponse);

                    if (response != null && !string.IsNullOrEmpty(response.transcription))
                    {
                        userResponse = response.transcription;
                        Debug.Log($"[AudioSender] 辨識成功：{userResponse}");
                    }
                    else
                    {
                        Debug.LogWarning("[AudioSender] 伺服器回傳無效內容。");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogError("[AudioSender] 解析 JSON 失敗: " + ex.Message);
                }
            }
            else
            {
                string jsonResponse = request.downloadHandler.text;
                try
                {
                    RecognitionResponse response = JsonUtility.FromJson<RecognitionResponse>(jsonResponse);
                    if (response != null && !string.IsNullOrEmpty(response.error))
                    {
                        Debug.LogWarning($"[AudioSender] 伺服器錯誤: {response.error}");
                    }
                    else
                    {
                        Debug.LogError($"[AudioSender] 請求失敗: {request.error}. Response: {jsonResponse}");
                    }
                }
                catch
                {
                    Debug.LogError($"[AudioSender] 無法解析伺服器回傳錯誤: {request.error}");
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("[AudioSender] 未預期的例外錯誤: " + ex.Message);
        }

        // ✅ 不論成功或失敗，統一執行以下流程
        Debug.Log($"[AudioSender] 傳輸結束，準備檢查答案並進入下一場景。結果：'{userResponse}'");

        if (answerManager != null)
        {
            answerManager.CheckAnswer(userResponse, questionSequenceIndex);
        }
        else
        {
            Debug.LogWarning("[AudioSender] AnswerManager 未設定，略過答案檢查。");
        }

        // ✅ 無論伺服器狀況如何，都會換場
        if (SceneFlowManager.instance != null)
        {
            StartCoroutine(LoadNextSceneWithDelay(2f));
        }
    }

    private IEnumerator LoadNextSceneWithDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        SceneFlowManager.instance.LoadNextScene();
    }
}
