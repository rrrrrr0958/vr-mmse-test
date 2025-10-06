using UnityEngine;
using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.UI;
using UnityEngine.Networking;

public class Rule_script : MonoBehaviour
{
    [Header("VR 攝影機與 XR Origin")]
    public XROrigin xrOrigin;
    public Transform vrCameraTransform;

    [Header("VR 輸入設定")]
    private ActionBasedController leftHandController;
    private ActionBasedController rightHandController;

    [Header("UI & 3D 物件")]
    public TextMeshPro RuleText_rule;
    public GameObject treasurebg_rule;
    public GameObject confirmationButton;

    [Header("開始畫面設定")]
    public GameObject startButton;  // ← 在 Inspector 拖入「開始」UI 按鈕

    // 新增開始提示的語音和文字
    [Header("開始提示語音與文字")]
    public AudioClip startClip;
    public string startText = "請用控制器任意鍵點選下方按鈕";

    private bool gameStarted = false;

    // 語音辨識相關設定
    [Header("語音辨識設定")]
    public string recognitionServerURL = "http://localhost:5000/recognize_speech";
    public float maxRecordingTime = 5.0f; // 最大錄音時間 (秒)
    private const int SAMPLE_RATE = 16000; // 錄音取樣率 (語音辨識常用 16000)

    [Header("語音設定")]
    public AudioSource voiceAudioSource;
    public AudioClip[] ruleClips;

    [Header("時間設定")]
    public float initialDelaySeconds = 3f;
    public float treasureDisplaySeconds = 3f;
    public float textSegmentDelay = 0.0f;

    private string[] ruleTexts_Final = new string[]
    {
        "歡迎來到VR樂園",
        "我們準備了一系列的挑戰任務",
        "所有任務完成後可以開啟寶箱",
        "現在先來知道挑戰的規則",
        "第一：請勿移動和大幅度轉頭",
        "第二：若在遊戲過程中感到任何不適",
        "請立即告知身旁的護理人員",
        "第三：遊戲任務如果需要點選物品",
        "請使用食指按下扳機鍵",
        "現在請使用扳機鍵對準按鈕並按下",
        "第四：若遊戲任務需要作答",
        "請在題目播放完畢後直接說出答案",
        "或是依照題目指令回答",
        "現在請說出：      「我知道了」", // Index 13
        "接下來開始遊戲吧！"     // Index 14
    };

    private bool buttonWasPressed = false;

    void Start()
    {
        var controllers = FindObjectsOfType<ActionBasedController>();
        foreach (var c in controllers)
        {
            if (c.name.ToLower().Contains("left")) leftHandController = c;
            else if (c.name.ToLower().Contains("right")) rightHandController = c;
        }

        if (leftHandController == null && rightHandController == null)
            Debug.LogWarning("⚠️ 未找到任何 Action-Based Controller，請確認 XR Rig 設置正確。");

        // UI 初始化
        RuleText_rule?.gameObject.SetActive(false);
        treasurebg_rule?.SetActive(false);
        confirmationButton?.SetActive(false);
        startButton?.SetActive(true);

        // 🌟 綁定開始按鈕
        if (startButton != null)
        {
            Button btn = startButton.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => { gameStarted = true; startButton.SetActive(false); });
        }

        if (voiceAudioSource == null || RuleText_rule == null || xrOrigin == null || vrCameraTransform == null || confirmationButton == null)
        {
            Debug.LogError("⚠️ 必要的 UI 或 XR 元件未設定！");
            return;
        }

        ApplyCameraRotationToOrigin();
        StartCoroutine(WaitForStartThenBegin());
    }

    public void ApplyCameraRotationToOrigin()
    {
        Quaternion cameraRotation = vrCameraTransform.rotation;
        xrOrigin.transform.rotation = Quaternion.Euler(0f, cameraRotation.eulerAngles.y, 0f);
    }

    public void ConfirmButtonClick()
    {
        buttonWasPressed = true;
        Debug.Log("🟢 按鈕被點擊。");
    }

    IEnumerator StartGameFlow()
    {
        yield return new WaitForSeconds(initialDelaySeconds);
        RuleText_rule.gameObject.SetActive(true);

        for (int i = 0; i < ruleTexts_Final.Length; i++)
        {
            if (i == 2)
            {
                // 寶箱展示 (文字、語音、背景同步)
                treasurebg_rule.SetActive(true);
                yield return StartCoroutine(PlayVoiceAndText(i));
                treasurebg_rule.SetActive(false);
            }
            else if (i == 9)
            {
                // 按鈕測試
                yield return StartCoroutine(PlayVoiceAndText(i));

                RuleText_rule.text = "";
                confirmationButton.SetActive(true);
                yield return StartCoroutine(WaitForButtonPress());
                confirmationButton.SetActive(false);
            }
            else if (i == 13)
            {
                // 語音輸入指令 (Index 13: "現在請說出：「我知道了」")
                yield return StartCoroutine(PlayVoiceAndText(i));

                // 🌟 新增: 錄音並傳送到伺服器
                yield return StartCoroutine(StartRecordingAndRecognize());

                // 錄音結束後，繼續播放下一個語音
            }
            else
            {
                // 其他段落照常播放
                yield return StartCoroutine(PlayVoiceAndText(i));
            }
        }

        RuleText_rule.gameObject.SetActive(false);
        Debug.Log("🎯 規則播放完畢，流程結束。");
    }

    IEnumerator PlayVoiceAndText(int index)
    {
        var clip = ruleClips[index];
        RuleText_rule.text = ruleTexts_Final[index];
        voiceAudioSource.PlayOneShot(clip);
        yield return new WaitForSeconds(clip.length + textSegmentDelay);
    }

    IEnumerator WaitForStartThenBegin()
    {
        // 1. 🌟 顯示開始畫面 UI 和文字
        RuleText_rule.gameObject.SetActive(true);
        RuleText_rule.text = startText;
        startButton?.SetActive(true);

        // 2. 🌟 播放開始語音
        if (voiceAudioSource != null && startClip != null)
        {
            voiceAudioSource.PlayOneShot(startClip);
            // 播放完畢後，語音會自動停止，我們繼續等待輸入
        }
        else
        {
            Debug.LogWarning("⚠️ 缺少 Start Clip，將不播放開始語音。");
        }

        // 3. 等待開始輸入 (UI 按鈕點擊 或 扳機鍵按下)
        while (!gameStarted)
        {
            // 偵測左右手扳機啟動 (IsAnyTriggerPressed() 已經處理了按壓的瞬間)
            if (IsAnyTriggerPressed())
            {
                gameStarted = true;
                Debug.Log("🟢 透過 VR 板機開始流程。");
            }
            yield return null;
        }

        // 4. 開始流程後的清理
        // 停止可能還在播放的開始語音
        voiceAudioSource.Stop();
        startButton?.SetActive(false);
        RuleText_rule.gameObject.SetActive(false);

        // 5. 啟動主流程
        StartCoroutine(StartGameFlow());
    }

    IEnumerator WaitForButtonPress()
    {
        buttonWasPressed = false;
        Debug.Log("🕹️ 等待玩家按下 VR 板機鍵或按鈕...");

        Button btn = confirmationButton.GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(ConfirmButtonClick);

        bool triggerHeld = false;

        while (!buttonWasPressed)
        {
            // 🌟 只要任一控制器的扳機按下即可
            if (IsAnyTriggerPressed())
            {
                if (!triggerHeld)
                {
                    buttonWasPressed = true;
                    Debug.Log("🎮 任一 VR 板機鍵按下偵測到。");
                }
                triggerHeld = true;
            }
            else
            {
                triggerHeld = false;
            }

            yield return null;
        }

        if (btn != null)
            btn.onClick.RemoveListener(ConfirmButtonClick);
    }

    private bool IsAnyTriggerPressed()
    {
        bool leftTrigger = false;
        bool rightTrigger = false;

        if (leftHandController != null)
            leftTrigger = leftHandController.activateAction.action.ReadValue<float>() > 0.1f;

        if (rightHandController != null)
            rightTrigger = rightHandController.activateAction.action.ReadValue<float>() > 0.1f;

        return leftTrigger || rightTrigger;
    }

    // ===============================================
    // 🌟 新增: 語音錄製與辨識邏輯
    // ===============================================

    IEnumerator StartRecordingAndRecognize()
    {
        // 1. 檢查是否有麥克風
        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("🔴 找不到麥克風設備！無法進行錄音。");
            RuleText_rule.text = "找不到麥克風！";
            yield return new WaitForSeconds(2f);
            yield break; // 結束錄音流程
        }

        string deviceName = Microphone.devices[0];
        Debug.Log($"🎙️ 開始錄音，使用設備: {deviceName}");

        // 2. 開始錄音
        // 顯示 "錄音中"
        RuleText_rule.text = "錄音中";

        // 啟動錄音，長度為 maxRecordingTime，使用 SAMPLE_RATE
        AudioClip recordingClip = Microphone.Start(deviceName, false, (int)maxRecordingTime, SAMPLE_RATE);
        float startTime = Time.time;

        // 3. 等待錄音結束 (達到最大時間)
        // ⚠️ 備註: 若需按鍵停止錄音，這裡需要一個 while 迴圈等待按鍵事件
        while (Microphone.IsRecording(deviceName) && (Time.time - startTime < maxRecordingTime))
        {
            yield return null;
        }

        // 4. 停止錄音
        Microphone.End(deviceName);
        Debug.Log($"✅ 錄音停止，錄音長度: {Time.time - startTime:F2} 秒");

        // 5. 處理錄製的音訊 (只取有效的長度)
        float clipLength = Time.time - startTime;
        if (clipLength > 0.1f) // 避免錄到空檔
        {
            // 從 AudioClip 截取出有效的錄音部分
            AudioClip finalClip = TrimAudioClip(recordingClip, clipLength);

            // 6. 將音訊上傳並等待辨識結果
            //RuleText_rule.text = "處理中...";
            yield return StartCoroutine(UploadAudio(finalClip));

            // 釋放記憶體
            Destroy(finalClip);
        }
        else
        {
            //RuleText_rule.text = "未錄到聲音，繼續。";
            Debug.Log("⚠️ 錄音時間過短，未上傳。");
            yield return new WaitForSeconds(1f);
        }
    }

    // 協助函式: 截取錄音片段
    private AudioClip TrimAudioClip(AudioClip originalClip, float clipLength)
    {
        int samples = (int)(clipLength * originalClip.frequency);
        float[] data = new float[samples];
        originalClip.GetData(data, 0);

        AudioClip newClip = AudioClip.Create("TrimmedClip", samples, originalClip.channels, originalClip.frequency, false);
        newClip.SetData(data, 0);
        return newClip;
    }

    // 協助函式: 執行上傳及語音辨識
    // 協助函式: 執行上傳及語音辨識
    IEnumerator UploadAudio(AudioClip clip)
    {
        // 將 AudioClip 轉換為 WAV 格式的 Byte 陣列
        byte[] wavData = WavUtility.FromAudioClip(clip);

        // 使用 UnityWebRequestMultiPartForm 來發送檔案
        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, "temp_audio.wav", "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post(recognitionServerURL, form))
        {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                // 伺服器連線或協議錯誤
                Debug.LogError($"🔴 語音辨識伺服器錯誤: {www.error}");
                //RuleText_rule.text = "伺服器連線錯誤！";
            }
            else
            {
                // 成功接收回應
                try
                {
                    string jsonResponse = www.downloadHandler.text;
                    RecognitionResponse response = JsonUtility.FromJson<RecognitionResponse>(jsonResponse);

                    if (response.transcription != null)
                    {
                        // 成功辨識
                        Debug.Log($"🗣️ 辨識結果 (Transcription): {response.transcription}");
                        //RuleText_rule.text = $"你說：「{response.transcription}」";
                    }
                    else if (response.error != null)
                    {
                        // 辨識失敗，但伺服器有回傳錯誤 (例如：辨識不到聲音)
                        Debug.LogWarning($"⚠️ 語音辨識錯誤: {response.error}");
                        //RuleText_rule.text = "辨識失敗或無回應。";
                    }
                }
                catch (System.Exception e)
                {
                    // 🔴 錯誤修正：從 try-catch 區塊中移除了 yield return
                    Debug.LogError($"🔴 解析伺服器回應失敗: {e.Message}");
                    //RuleText_rule.text = "處理回應失敗！";
                }
            }
        }

        // 🌟 修正：將等待時間移到 try-catch 區塊外面
        // 根據你的需求，無論結果如何，都讓文字停留 2 秒後再繼續流程
        yield return new WaitForSeconds(2f);
    }

}