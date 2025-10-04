using UnityEngine;
using System.Collections;
using TMPro;
using Unity.XR.CoreUtils;
using UnityEngine.XR.Interaction.Toolkit; // 必須引入此命名空間才能使用 XRController

public class Rule_script : MonoBehaviour
{
    // =================================================================
    // VR 流程 & XR 系統設定 (需要在 Inspector 中拖曳設定)
    // =================================================================

    [Header("VR 攝影機與 XR Origin")]
    [Tooltip("場景中的 XR Origin 根物件")]
    public XROrigin xrOrigin;

    [Tooltip("VR Camera/Head 的 Transform 元件 (用於抓取玩家當前方向)")]
    public Transform vrCameraTransform;

    [Header("VR 輸入設定")]
    [Tooltip("右手控制器 (RightHand Controller) 的 XRController 元件")]
    public Transform rightHandController; // 已修正為 XRController 類型

    // =================================================================
    // UI & 語音設定
    // =================================================================

    [Header("UI & 3D 物件")]
    [Tooltip("用於顯示規則文字的 TextMeshPro 元件 (3D 或 World Space Canvas)")]
    public TextMeshPro RuleText_rule;

    [Tooltip("在規則中間階段要顯示的背景/提示物件")]
    public GameObject treasurebg_rule;

    [Header("語音設定")]
    [Tooltip("播放語音用的 AudioSource 元件")]
    public AudioSource voiceAudioSource;

    [Tooltip("每段語音檔的 Clip (請照順序拖曳，共需 9 個音檔)")]
    public AudioClip[] ruleClips;

    // =================================================================
    // 時間設定
    // =================================================================

    [Header("時間設定")]
    [Tooltip("遊戲開始時的初始延遲秒數")]
    public float initialDelaySeconds = 3f;

    [Tooltip("在 'treasurebg_rule' 顯示後等待的秒數")]
    public float treasureDisplaySeconds = 3f;

    // 每一段語音播放完畢後，如果文字被拆解，每段文字之間等待的時間
    [Tooltip("拆分文字段落間的延遲時間")]
    public float textSegmentDelay = 0.5f;

    // =================================================================
    // 內部資料：完整的流程文字 (共 9 段語音對應 9 組文字或文字陣列)
    // =================================================================

    // 這個陣列將用於 PlayVoiceAndTextSegmented 函式
    private string[][] ruleTextSegments = new string[][]
    {
        // Index 0: 歡迎來到VR樂園 (不拆分)
        new string[] { "歡迎來到VR樂園" },
        
        // Index 1: 我們準備了一系列的挑戰任務 (不拆分)
        new string[] { "我們準備了一系列的挑戰任務" },
        
        // Index 2: 所有任務完成後可以開啟寶箱 (不拆分)
        new string[] { "所有任務完成後可以開啟寶箱" },
        
        // Index 3: 現在先來知道挑戰的規則 (不拆分)
        new string[] { "現在先來知道挑戰的規則" },
        
        // Index 4: 規則 1: 第一，請勿移動和大幅度轉頭。 -> 拆分成 2 段
        new string[] { "第一：", "請勿移動和大幅度轉頭" }, 
        
        // Index 5: 規則 2: 第二，若在遊戲過程中感到任何不適，請立即告知身旁的護理人員。 -> 拆分成 2 段
        new string[] { "第二：", "若在遊戲過程中感到任何不適", "請立即告知身旁的護理人員" }, 
        
        // Index 6: 規則 3 (需等待輸入): 第三，遊戲任務如果需要點選物品，請使用右手食指按下板機鍵。現在，請將右手食指對準按鈕並按下。 -> 拆分成 3 段
        new string[] { "第三：", "遊戲任務如果需要點選物品", "請使用右手食指按下板機鍵", "現在請將右手食指對準按鈕並按下" }, 
        
        // Index 7: 規則 4 (需等待語音輸入): 第四，若遊戲任務需要作答，請在題目播放完畢後直接說出答案，或是依照題目指令說出答案。現在，請回答：「我知道了」。 -> 拆分成 3 段
        new string[] { "第四：", "若遊戲任務需要作答", "請在題目播放完畢後直接說出答案", "或是依照題目指令說出答案。", "現在請回答：「我知道了」" }, 
        
        // Index 8: 結尾: 接下來開始遊戲吧！ (不拆分)
        new string[] { "接下來開始遊戲吧！" }
    };

    // =================================================================
    // Unity 生命週期方法
    // =================================================================

    void Start()
    {
        // 初始化：確保 UI 元素和背景是隱藏的
        if (RuleText_rule != null) RuleText_rule.gameObject.SetActive(false);
        if (treasurebg_rule != null) treasurebg_rule.SetActive(false);

        // 檢查關鍵設定
        if (voiceAudioSource == null || RuleText_rule == null || xrOrigin == null || vrCameraTransform == null || rightHandController == null)
        {
            // rightHandController 必須檢查 XRController 是否存在
            Debug.LogError("VR/UI 設定不完整，請檢查 Inspector 中的所有元件是否已設定！(特別是 rightHandController 必須是 XRController 元件)");
            return;
        }

        // 確保世界方向與玩家 HMD 方向同步
        ApplyCameraRotationToOrigin();

        // 啟動主要的遊戲流程協程
        StartCoroutine(StartGameFlow());
    }

    // =================================================================
    // 核心功能方法
    // =================================================================

    public void ApplyCameraRotationToOrigin()
    {
        Quaternion cameraRotation = vrCameraTransform.rotation;
        Vector3 euler = cameraRotation.eulerAngles;

        Quaternion targetRotation = Quaternion.Euler(0f, euler.y, 0f);

        xrOrigin.transform.rotation = targetRotation;

        Debug.Log($"VR 空間方向已鎖定。XR Origin 旋轉 Y 軸角度為: {targetRotation.eulerAngles.y}");
    }

    /// <summary>
    /// 遊戲規則依序播放控制協程。
    /// </summary>
    IEnumerator StartGameFlow()
    {
        // 1. 遊戲一開始先停 initialDelaySeconds 秒
        yield return new WaitForSeconds(initialDelaySeconds);

        // 顯示規則文字物件
        RuleText_rule.gameObject.SetActive(true);

        // 確保音檔數量足夠
        if (ruleClips.Length < ruleTextSegments.Length)
        {
            Debug.LogError($"音檔數量不足！需要 {ruleTextSegments.Length} 個音檔，但只拖曳了 {ruleClips.Length} 個。請檢查 ruleClips 陣列。");
            RuleText_rule.gameObject.SetActive(false);
            yield break;
        }

        // =====================================================
        // PART A: 開場流程 (Index 0, 1, 2, 3)
        // =====================================================

        // Index 0: 歡迎來到VR樂園
        yield return StartCoroutine(PlayVoiceAndTextSegmented(0));

        // Index 1: 我們準備了一系列的挑戰任務
        yield return StartCoroutine(PlayVoiceAndTextSegmented(1));

        // Index 2: 所有任務完成後可以開啟寶箱 (此時顯示寶箱)
        yield return StartCoroutine(PlayVoiceAndTextSegmented(2));

        // 顯示 treasurebg_rule (寶箱背景/提示)
        treasurebg_rule.SetActive(true);

        // 等待 treasureDisplaySeconds 秒 (RuleText_rule 和 treasurebg_rule 一起顯示)
        yield return new WaitForSeconds(treasureDisplaySeconds);

        // 隱藏 treasurebg_rule
        treasurebg_rule.SetActive(false);

        // Index 3: 現在先來知道挑戰的規則
        yield return StartCoroutine(PlayVoiceAndTextSegmented(3));


        // =====================================================
        // PART B: 四個新規則 (Index 4, 5, 6, 7)
        // =====================================================

        for (int i = 4; i <= 7; i++)
        {
            // 播放語音和文字段落
            yield return StartCoroutine(PlayVoiceAndTextSegmented(i));

            // === 規則 3: 等待右手板機鍵輸入 (i == 6) ===
            //if (i == 6)
            //{
            //    RuleText_rule.text = "請按下右手板機鍵..."; // 提示玩家
            //    yield return StartCoroutine(WaitForRightTriggerPress());
            //    RuleText_rule.text = "";
            //}

            // === 規則 4: 等待語音輸入 (i == 7) ===
            //if (i == 7)
            //{
            //    RuleText_rule.text = "請回答「我知道了」..."; // 提示玩家
            //    yield return StartCoroutine(SimulateSpeechInput("我知道了"));
            //    RuleText_rule.text = "";
            //}
        }

        // =====================================================
        // PART C: 結尾 (Index 8)
        // =====================================================

        // Index 8: 接下來開始遊戲吧！
        yield return StartCoroutine(PlayVoiceAndTextSegmented(8));

        // 流程結束後，隱藏文字
        RuleText_rule.gameObject.SetActive(false);
        Debug.Log("規則教學流程結束，遊戲正式開始！");
    }

    /// <summary>
    /// 輔助方法：播放單段語音，並依序顯示多個文字段落。
    /// </summary>
    IEnumerator PlayVoiceAndTextSegmented(int index)
    {
        AudioClip clip = ruleClips[index];
        string[] segments = ruleTextSegments[index];

        // 播放語音 (只播放一次)
        voiceAudioSource.PlayOneShot(clip);

        // 同時開始計時語音播放時間
        float startTime = Time.time;
        float clipDuration = clip.length;

        // 依序顯示文字段落
        for (int i = 0; i < segments.Length; i++)
        {
            RuleText_rule.text = segments[i];

            // 如果不是最後一段文字，就等待一段時間
            if (i < segments.Length - 1)
            {
                yield return new WaitForSeconds(textSegmentDelay);
            }
        }

        // 等待語音播放完畢 (確保語音比文字顯示時間長)
        float remainingTime = clipDuration - (Time.time - startTime);
        if (remainingTime > 0)
        {
            yield return new WaitForSeconds(remainingTime);
        }
    }

    // =================================================================
    // 輸入等待協程
    // =================================================================

    /// <summary>
    /// 等待右手控制器的板機鍵被按下。
    /// </summary>
    IEnumerator WaitForRightTriggerPress()
    {
        Debug.Log("等待右手板機鍵輸入...");

        bool triggerPressed = false;

        while (!triggerPressed)
        {
          
            yield return null; // 等待下一幀
        }

        Debug.Log("右手板機鍵已按下，繼續流程。");
    }

    /// <summary>
    /// 模擬語音輸入等待。
    /// </summary>
    IEnumerator SimulateSpeechInput(string targetPhrase)
    {
        Debug.Log($"等待語音輸入: {targetPhrase}");

        // 模擬語音輸入等待 3 秒
        yield return new WaitForSeconds(3.0f);

        Debug.Log($"語音輸入 '{targetPhrase}' 已模擬，繼續流程。");
    }
}