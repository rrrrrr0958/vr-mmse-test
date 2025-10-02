using UnityEngine;
using System.Collections;
using TMPro;
using Unity.XR.CoreUtils; // 必須引入此命名空間才能使用 XR Origin 元件

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

    [Tooltip("每段語音檔的 Clip (請照順序拖曳)")]
    public AudioClip[] ruleClips;

    // =================================================================
    // 時間設定
    // =================================================================

    [Header("時間設定")]
    [Tooltip("遊戲開始時的初始延遲秒數")]
    public float initialDelaySeconds = 3f;

    [Tooltip("在 'treasurebg_rule' 顯示後等待的秒數")]
    public float treasureDisplaySeconds = 3f;

    // =================================================================
    // 內部資料
    // =================================================================

    private string[] ruleTexts = new string[]
    {
        "歡迎來到VR樂園",
        "我們準備了一系列的挑戰任務",
        "任務成功後可以獲得寶箱的鑰匙",
        "現在先來知道挑戰的規則"
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
        if (voiceAudioSource == null || RuleText_rule == null || xrOrigin == null || vrCameraTransform == null)
        {
            Debug.LogError("VR/UI 設定不完整，請檢查 Inspector 中的 AudioSource, RuleText, XR Origin, 或 VR Camera Transform 是否已設定！");
            return;
        }

        // 確保世界方向與玩家 HMD 方向同步（在遊戲流程開始前執行）
        ApplyCameraRotationToOrigin();

        // 啟動主要的遊戲流程協程
        StartCoroutine(StartGameFlow());
    }

    // =================================================================
    // 核心功能方法
    // =================================================================

    /// <summary>
    /// 將 VR 攝影機的 Y 軸旋轉應用到 XR Origin，以對齊世界起始方向。
    /// </summary>
    public void ApplyCameraRotationToOrigin()
    {
        // 1. 獲取攝影機的旋轉
        Quaternion cameraRotation = vrCameraTransform.rotation;

        // 2. 僅提取 Y 軸 (Yaw) 的角度，忽略俯仰 (Pitch) 和側傾 (Roll)
        Vector3 euler = cameraRotation.eulerAngles;

        // 3. 創建一個只包含 Y 軸旋轉的新四元數
        Quaternion targetRotation = Quaternion.Euler(0f, euler.y, 0f);

        // 4. 將此 Y 軸旋轉應用到 XR Origin，這會旋轉整個 VR 世界
        xrOrigin.transform.rotation = targetRotation;

        Debug.Log($"VR 空間方向已鎖定。XR Origin 旋轉 Y 軸角度為: {targetRotation.eulerAngles.y}");
    }

    /// <summary>
    /// 遊戲開場和教學流程的控制協程。
    /// </summary>
    IEnumerator StartGameFlow()
    {
        // 1. 遊戲一開始先停 initialDelaySeconds 秒
        yield return new WaitForSeconds(initialDelaySeconds);

        // 顯示規則文字物件
        RuleText_rule.gameObject.SetActive(true);

        // 2. 播放第一階段語音和文字 (索引 0, 1, 2)
        // 語音: "歡迎來到VR樂園"
        // 語音: "我們準備了一系列的挑戰任務"
        // 語音: "任務成功後可以獲得寶箱的鑰匙"
        for (int i = 0; i < 3; i++)
        {
            if (i < ruleClips.Length)
            {
                yield return StartCoroutine(PlayVoiceAndText(ruleTexts[i], ruleClips[i]));
            }
        }

        // 3. 語音撥完"任務成功後可以獲得寶箱的鑰匙"後，隱藏 RuleText_rule，顯示 treasurebg_rule
        RuleText_rule.gameObject.SetActive(false);
        treasurebg_rule.SetActive(true);

        // 4. 等待 treasureDisplaySeconds 秒
        yield return new WaitForSeconds(treasureDisplaySeconds);

        // 5. 隱藏 treasurebg_rule，顯示 RuleText_rule
        treasurebg_rule.SetActive(false);
        RuleText_rule.gameObject.SetActive(true);

        // 6. 繼續撥放後續語音和文字 (索引 3)
        // 語音: "現在先來知道挑戰的規則"
        if (ruleClips.Length > 3)
        {
            yield return StartCoroutine(PlayVoiceAndText(ruleTexts[3], ruleClips[3]));
        }

        Debug.Log("開場流程結束，遊戲正式開始！");
    }

    /// <summary>
    /// 輔助方法：同步播放單段語音和顯示文字。
    /// </summary>
    IEnumerator PlayVoiceAndText(string text, AudioClip clip)
    {
        // 寫入文字
        RuleText_rule.text = text;

        // 播放語音
        voiceAudioSource.PlayOneShot(clip);

        // 等待語音播放完畢
        yield return new WaitForSeconds(clip.length);
    }
}