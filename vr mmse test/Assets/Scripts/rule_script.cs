using UnityEngine;
using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit; //  用 XRI 的 Interactor 狀態
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.UI;
using UnityEngine.Networking;
using System.Text;
using UnityEngine.SocialPlatforms.Impl;

public class Rule_script : MonoBehaviour
{
    private FirebaseManager_Firestore FirebaseManager;
    [Header("VR 攝影機與 XR Origin")]
    public XROrigin xrOrigin;
    public Transform vrCameraTransform;

    // ====== ✅ 直接拖拽左右手 Interactor（Ray 或 Direct 皆可，需繼承 XRBaseControllerInteractor） ======
    [Header("Hand Interactors (拖拽 XR Origin 子物件上的 Interactor)")]
    [Tooltip("拖左手的 XR Ray Interactor 或 XR Direct Interactor")]
    public XRBaseInputInteractor leftHandInteractor;
    [Tooltip("拖右手的 XR Ray Interactor 或 XR Direct Interactor")]
    public XRBaseInputInteractor rightHandInteractor;

    [Header("UI & 3D 物件")]
    public TextMeshPro RuleText_rule;
    public GameObject treasurebg_rule;
    public GameObject confirmationButton;
    public GameObject vr_hand;
    public GameObject vr_hand_start;

    [Header("開始畫面設定")]
    public GameObject startButton;
    // 新增：用於 StartButton 的 Image 元件，以控制顏色
    private Image startButtonImage;

    // 新增開始提示的語音和文字
    [Header("開始提示語音與文字")]
    public AudioClip startClip;
    // **根據需求修改提示文字**
    public string startText = "請使用食指按下\n扳機鍵，點擊下方按鈕直到變成黃色";

    private bool gameStarted = false;

    // 語音辨識相關設定
    [Header("語音辨識設定")]
    public string recognitionServerURL = "http://localhost:5000/recognize_speech";
    public float maxRecordingTime = 5.0f; // 最大錄音時間 (秒)
    private const int SAMPLE_RATE = 16000; // 錄音取樣率 (語音辨識常用 16000)

    [Header("語音設定")]
    public AudioSource voiceAudioSource;
    public AudioClip[] ruleClips;

    [Header("音效")]
    public AudioClip buttonClickSound;

    // 【修正需求 2 & 3 新增】用於成功與失敗的音效和文字
    [Header("錄音成功/失敗回饋")]
    public AudioClip successClip; // 新增：錄音成功音效
    public AudioClip failureClip; // 新增：錄音結束/失敗音效
    private const string SUCCESS_TEXT = "太棒了!錄音成功";
    private const string FAILURE_TEXT = "錄音已結束";

    // **修正：用於追蹤前一秒的時間，確保倒數只在整秒變化時更新。**
    private int lastTimeLeft = 0;

    [Header("時間設定")]
    public float initialDelaySeconds = 3f;
    public float treasureDisplaySeconds = 3f;
    public float textSegmentDelay = 0.0f;
    // 新增：按鈕按下後等待時間
    public float postButtonDelay = 1.0f;

    // **根據需求修改規則段落**
    private string[] ruleTexts_Final = new string[]
    {
        "歡迎來到VR樂園",       // Index 0
        "我們總共有10道\n遊戲任務", // Index 1
        "所有任務完成後\n可以開啟寶箱", // Index 2 (寶箱展示)
        "現在先來知道\n挑戰的規則",   // Index 3
        "第一：請勿移動\n和大幅度轉頭", // Index 4
        "第二：若在遊戲過程中感到任何不適", // Index 5
        "請立即告知\n身旁的護理人員",   // Index 6
        "第三：若遊戲\n任務需要作答",   // Index 7
        "請在題目播放完畢後直接說出答案", // Index 8
        "或是依照\n題目指令回答",     // Index 9
        "現在請說出：\n「我知道了」", // Index 10 (語音輸入指令)
        "接下來開始遊戲吧！"      // Index 11 (流程結束)
    };

    // 定義按鈕顏色
    private Color defaultColor = Color.white; // 假設按鈕初始色為白色
    private Color hoverColor = new Color(0.5f, 0.5f, 0.5f); // 深灰色 (R:0.5, G:0.5, B:0.5)
    private Color pressedColor = Color.yellow; // 黃色

    private bool buttonWasPressed = false;

    // 🌟 新增：用於解析 JSON 回應的結構
    [System.Serializable]
    public class RecognitionResponse
    {
        public string transcription;
        public string error;
    }

    void Start()
    {
        // FirebaseManager = FirebaseManager_Firestore.Instance;
        // 檢查左右手 Interactor 是否已指定
        if (leftHandInteractor == null || rightHandInteractor == null)
        {
            Debug.LogWarning("⚠️ 尚未在 Inspector 指定左右手的 XRBaseControllerInteractor（XR Ray/Direct Interactor）。" +
                             "請在 XR Origin 的子物件上找到 Interactor，拖到本腳本的欄位。");
        }

        // UI 初始化
        RuleText_rule?.gameObject.SetActive(false);
        treasurebg_rule?.SetActive(false);
        confirmationButton?.SetActive(false);
        startButton?.SetActive(true);
        // 【新增】確保這個 UI 也是關閉的
        vr_hand?.SetActive(false);
        vr_hand_start?.SetActive(false);

        // 🌟 綁定開始按鈕和顏色設定
        if (startButton != null)
        {
            Button btn = startButton.GetComponent<Button>();
            startButtonImage = startButton.GetComponent<Image>(); // 獲取 Image 組件

            if (btn != null)
            {
                // 設定按鈕顏色 (確保在 Inspector 中也設定了 Color Block)
                ColorBlock cb = btn.colors;
                cb.highlightedColor = hoverColor; // Highlighted/Hover 顏色設定為深灰色
                cb.pressedColor = pressedColor; // Pressed 顏色設定為黃色
                btn.colors = cb;

                // 按鈕點擊事件改由 WaitForStartThenBegin 協程控制，避免直接在此處結束遊戲流程。
                // 這裡只需確保按鈕點擊會設置 gameStarted = true
                btn.onClick.AddListener(OnStartButtonClick);
            }
            if (startButtonImage != null)
            {
                defaultColor = startButtonImage.color; // 紀錄初始顏色
            }
        }

        if (voiceAudioSource == null || RuleText_rule == null || xrOrigin == null || vrCameraTransform == null || confirmationButton == null)
        {
            Debug.LogError("⚠️ 必要的 UI 或 XR 元件未設定！");
            return;
        }

        // 【新增】檢查成功/失敗音效是否設定
        if (successClip == null) Debug.LogWarning("⚠️ successClip 未設定。");
        if (failureClip == null) Debug.LogWarning("⚠️ failureClip 未設定。");

        ApplyCameraRotationToOrigin();
        StartCoroutine(WaitForStartThenBegin());
    }

    // 處理 Start 按鈕點擊的函數
    private void OnStartButtonClick()
    {
        if (gameStarted) return; // 避免重複點擊

        // 【新增】按鈕點擊後設置為已開始，顏色將在協程中處理
        gameStarted = true;
        Debug.Log("🟢 透過 UI 按鈕開始流程。");
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

    string testId;
    string levelIndex = "0";
    int levelScore = 0;

    public void gameStart()
    {
        testId = FirebaseManager_Firestore.Instance.GenerateTestId();
        Debug.Log("測驗開始，Test ID: " + testId);
    }

    IEnumerator StartGameFlow()
    {
        yield return new WaitForSeconds(initialDelaySeconds);
        RuleText_rule.gameObject.SetActive(true);

        // 根據新的 ruleTexts_Final 陣列長度調整迴圈
        for (int i = 0; i < ruleTexts_Final.Length; i++)
        {
            if (i == 2) // Index 2: 寶箱展示 (文字、語音、背景同步)
            {
                treasurebg_rule.SetActive(true);
                yield return StartCoroutine(PlayVoiceAndText(i));
                treasurebg_rule.SetActive(false);
            }
            // **舊的按鈕點擊邏輯 (i == 9) 已被移除**
            else if (i == 10) // Index 10: 語音輸入指令 (現在請說出：「我知道了」)
            {
                yield return StartCoroutine(PlayVoiceAndText(i));

                // 🌟 執行錄音與辨識流程，並接收結果
                RecordingResult result = new RecordingResult { status = RecordingStatus.NotStarted, clipDuration = 0f };

                yield return StartCoroutine(StartRecordingAndRecognize(r => {
                    result = r;
                }));

                // 🌟 根據錄音結果播放不同的語音/文字回饋
                bool isSuccessful = result.status == RecordingStatus.Success;

                if (isSuccessful)
                {
                    // 【修正需求 2】錄音成功
                    Debug.Log("✅ 錄音成功，播放成功提示。");
                    RuleText_rule.text = SUCCESS_TEXT;
                    if (successClip != null) voiceAudioSource.PlayOneShot(successClip);
                }
                else
                {
                    // 【修正需求 3】錄音結束/失敗 (麥克風、空語音、連線或伺服器錯誤)
                    Debug.Log($"❌ 錄音結束或失敗，狀態: {result.status}，播放結束提示。");
                    RuleText_rule.text = FAILURE_TEXT;
                    if (failureClip != null) voiceAudioSource.PlayOneShot(failureClip);
                }

                // 等待語音播放完畢或給予固定時間顯示
                float waitTime = isSuccessful ? (successClip != null ? successClip.length : 2.0f) :
                                               (failureClip != null ? failureClip.length : 2.0f);

                yield return new WaitForSeconds(waitTime + 0.5f);

                // 錄音結束後，繼續播放下一個語音 (Index 11)
            }
            else
            {
                // 其他段落照常播放
                yield return StartCoroutine(PlayVoiceAndText(i));
            }
        }

        RuleText_rule.gameObject.SetActive(false);
        Debug.Log("🎯 規則播放完畢，流程結束。");
        // 🚨 假設 SceneFlowManager.instance.LoadNextScene() 存在且運作正常
        FirebaseManager_Firestore.Instance.SaveLevelData(testId, levelIndex, levelScore);
        SceneFlowManager.instance.LoadNextScene(); 
        Debug.Log("✅ 流程結束，準備載入下一個場景。");
    }

    IEnumerator PlayVoiceAndText(int index)
    {
        // 檢查索引是否超出範圍
        if (index >= ruleClips.Length || index >= ruleTexts_Final.Length)
        {
            Debug.LogError($"❌ ruleClips 或 ruleTexts_Final 的索引 {index} 超出範圍！");
            yield break;
        }

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
        vr_hand_start?.SetActive(true);

        // 2. 🌟 循環播放開始語音直到按鈕被按下
        Debug.Log("🔄 開始循環播放開始語音...");

        while (!gameStarted)
        {
            // 檢查 AudioSource 是否正在播放
            if (voiceAudioSource != null && startClip != null && !voiceAudioSource.isPlaying)
            {
                // 語音播放完畢後，重新播放
                voiceAudioSource.PlayOneShot(startClip);
                // Debug.Log("🎙️ 重新播放開始語音。"); // 避免 Log 洗版
            }

            // 檢查 VR 輸入是否被按下
            if (IsAnyTriggerPressed())
            {
                // 如果按下的是 startButton，OnStartButtonClick 會設置 gameStarted = true
                // 如果按下的是其他東西，這裡的邏輯不會被觸發，因為 startButton 應該是一個 XR Interactable
                // 但我們在 Start() 已經將 Button.onClick 綁定，所以透過 UI/XR Button 按下都會設置 gameStarted = true
            }

            yield return null; // 等待下一幀
        }

        // 3. 按鈕被點擊 (gameStarted = true) 後的顏色變化與流程繼續

        // 立即停止當前正在播放的語音
        if (voiceAudioSource != null)
        {
            voiceAudioSource.Stop();

            // 【新增】播放按鈕點擊音效
            if (buttonClickSound != null)
            {
                voiceAudioSource.PlayOneShot(buttonClickSound);
            }
        }

        // 設置按鈕顏色為黃色 (Pressed Color)
        if (startButtonImage != null)
        {
            startButtonImage.color = pressedColor;
        }

        Debug.Log("⏳ 按鈕按下，等待 2 秒後繼續流程。");
        yield return new WaitForSeconds(postButtonDelay); // 等待 2 秒

        // 4. 開始流程後的清理
        startButton?.SetActive(false);
        RuleText_rule.gameObject.SetActive(false);
        vr_hand_start?.SetActive(false);

        // 5. 啟動主流程
        // gameStart();
        StartCoroutine(StartGameFlow());
    }

    IEnumerator WaitForButtonPress()
    {
        buttonWasPressed = false;
        Debug.Log("🕹️ 等待玩家按下 VR 扳機鍵或按鈕...");

        Button btn = confirmationButton.GetComponent<Button>();
        if (btn != null)
            btn.onClick.AddListener(ConfirmButtonClick);

        bool triggerHeld = false;

        while (!buttonWasPressed)
        {
            if (IsAnyTriggerPressed())
            {
                if (!triggerHeld)
                {
                    buttonWasPressed = true;
                    Debug.Log("🎮 任一 VR 扳機鍵按下偵測到。");
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

    // ====== ✅ 核心：用 Interactor 的互動狀態判斷「是否按下」 ======
    // Activate：通常對應 Trigger；UI Press：用來按 Unity UI
    private bool IsAnyTriggerPressed()
    {
        bool left = leftHandInteractor != null &&
                    (leftHandInteractor.logicalActivateState.active
                    /* || leftHandInteractor.logicalSelectState.active  // 若需要把抓取也算進去，解開註解 */);

        bool right = rightHandInteractor != null &&
                    (rightHandInteractor.logicalActivateState.active
                    /* || rightHandInteractor.logicalSelectState.active */);

        return left || right;
    }


    // ===============================================
    // 🌟 語音錄製與辨識邏輯（以下原樣保留）
    // ===============================================

    public enum RecordingStatus
    {
        NotStarted,
        Success,              // 錄音長度足夠，且成功連線到伺服器並獲得回應 (不論辨識內容)
        NoMic,                // 無麥克風
        TooShort,             // 錄到空音訊或太短 (< 0.1s)
        ConnectionOrServerError // 連線失敗或伺服器回應錯誤 (如連線不到)
    }

    public struct RecordingResult
    {
        public RecordingStatus status;
        public float clipDuration;
    }

    IEnumerator StartRecordingAndRecognize(System.Action<RecordingResult> callback)
    {
        RecordingResult result = new RecordingResult { status = RecordingStatus.TooShort, clipDuration = 0f };

        if (Microphone.devices.Length == 0)
        {
            Debug.LogError("🔴 找不到麥克風設備！無法進行錄音。");
            result.status = RecordingStatus.NoMic;
            callback(result);
            yield break;
        }

        string deviceName = Microphone.devices[0];
        Debug.Log($"🎙️ 開始錄音，使用設備: {deviceName}");

        RuleText_rule.text = "錄音中...\n(剩餘 " + ((int)maxRecordingTime) + " 秒)";
        lastTimeLeft = (int)maxRecordingTime;

        AudioClip recordingClip = Microphone.Start(deviceName, false, (int)maxRecordingTime, SAMPLE_RATE);
        float startTime = Time.time;

        while (Microphone.IsRecording(deviceName) && (Time.time - startTime < maxRecordingTime))
        {
            int timeLeft = (int)Mathf.Ceil(maxRecordingTime - (Time.time - startTime));

            if (timeLeft != lastTimeLeft && timeLeft >= 0)
            {
                RuleText_rule.text = $"錄音中...\n(剩餘 {timeLeft} 秒)";
                lastTimeLeft = timeLeft;
            }
            yield return null;
        }

        Microphone.End(deviceName);
        float endTime = Time.time;
        float clipLength = endTime - startTime;

        Debug.Log($"✅ 錄音停止，錄音長度: {clipLength:F2} 秒");
        RuleText_rule.text = "處理中...";

        if (clipLength > 0.1f)
        {
            AudioClip finalClip = TrimAudioClip(recordingClip, clipLength);
            result.clipDuration = clipLength;

            RecordingStatus uploadStatus = RecordingStatus.ConnectionOrServerError;

            yield return StartCoroutine(UploadAudio(finalClip, status => { uploadStatus = status; }));

            Destroy(finalClip);

            result.status = (uploadStatus == RecordingStatus.Success)
                            ? RecordingStatus.Success
                            : RecordingStatus.ConnectionOrServerError;
        }
        else
        {
            result.status = RecordingStatus.TooShort;
        }

        callback(result);
    }

    private AudioClip TrimAudioClip(AudioClip originalClip, float clipLength)
    {
        int samples = (int)(clipLength * originalClip.frequency);
        float[] data = new float[samples];
        originalClip.GetData(data, 0);

        AudioClip newClip = AudioClip.Create("TrimmedClip", samples, originalClip.channels, originalClip.frequency, false);
        newClip.SetData(data, 0);
        return newClip;
    }

    private byte[] AudioClipToWav(AudioClip clip)
    {
        int channels = clip.channels;
        int sampleRate = clip.frequency;
        int samples = clip.samples;

        float[] data = new float[samples * channels];
        clip.GetData(data, 0);

        short[] intData = new short[data.Length];
        byte[] bytesData = new byte[data.Length * 2];

        int rescaleFactor = 32767;
        for (int i = 0; i < data.Length; i++)
        {
            intData[i] = (short)(data[i] * rescaleFactor);
            bytesData[i * 2] = (byte)(intData[i]);
            bytesData[i * 2 + 1] = (byte)(intData[i] >> 8);
        }

        int headerSize = 44;
        int totalLength = headerSize + bytesData.Length;

        System.IO.MemoryStream stream = new System.IO.MemoryStream(totalLength);
        System.IO.BinaryWriter writer = new System.IO.BinaryWriter(stream);

        writer.Write(Encoding.UTF8.GetBytes("RIFF"));
        writer.Write(totalLength - 8);
        writer.Write(Encoding.UTF8.GetBytes("WAVE"));

        writer.Write(Encoding.UTF8.GetBytes("fmt "));
        writer.Write(16);
        writer.Write((ushort)1);
        writer.Write((ushort)channels);
        writer.Write(sampleRate);
        writer.Write(sampleRate * channels * 2);
        writer.Write((ushort)(channels * 2));
        writer.Write((ushort)16);

        writer.Write(Encoding.UTF8.GetBytes("data"));
        writer.Write(bytesData.Length);
        writer.Write(bytesData);

        byte[] wavData = stream.ToArray();
        writer.Close();
        stream.Close();

        return wavData;
    }

    IEnumerator UploadAudio(AudioClip clip, System.Action<RecordingStatus> callback)
    {
        byte[] wavData = AudioClipToWav(clip);

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", wavData, "temp_audio.wav", "audio/wav");

        using (UnityWebRequest www = UnityWebRequest.Post(recognitionServerURL, form))
        {
            yield return www.SendWebRequest();

            RecordingStatus finalStatus = RecordingStatus.ConnectionOrServerError;

            if (www.result == UnityWebRequest.Result.ConnectionError || www.result == UnityWebRequest.Result.ProtocolError)
            {
                Debug.LogError($"🔴 語音辨識伺服器錯誤: {www.error}");
                finalStatus = RecordingStatus.ConnectionOrServerError;
            }
            else
            {
                try
                {
                    string jsonResponse = www.downloadHandler.text;
                    RecognitionResponse response = JsonUtility.FromJson<RecognitionResponse>(jsonResponse);

                    if (response.transcription != null)
                    {
                        Debug.Log($"🗣️ 辨識結果 (Transcription): {response.transcription}");
                        finalStatus = RecordingStatus.Success;
                    }
                    else if (response.error != null)
                    {
                        Debug.LogWarning($"⚠️ 語音辨識錯誤: {response.error}");
                        finalStatus = RecordingStatus.Success; // 依你的需求：連線成功就算成功流程
                    }
                    else
                    {
                        Debug.LogError($"🔴 伺服器回應格式錯誤: {jsonResponse}");
                        finalStatus = RecordingStatus.ConnectionOrServerError;
                    }
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"🔴 解析伺服器回應失敗: {e.Message}");
                    finalStatus = RecordingStatus.ConnectionOrServerError;
                }
            }

            callback(finalStatus);
        }
    }
}