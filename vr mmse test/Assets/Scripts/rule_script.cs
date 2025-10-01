using UnityEngine;
using System.Collections;
using TMPro; // 記得引入 TextMeshPro 命名空間

public class Rule_script : MonoBehaviour
{
    // === 公開變數：在 Inspector 中拖曳設定 ===

    [Header("UI & 3D 物件")]
    [Tooltip("用於顯示規則文字的 TextMeshPro 物件")]
    public TextMeshPro RuleText_rule;

    [Tooltip("在規則中間階段要顯示的背景/提示物件")]
    public GameObject treasurebg_rule;

    [Header("語音設定")]
    [Tooltip("播放語音用的 AudioSource 元件")]
    public AudioSource voiceAudioSource;

    [Tooltip("每段語音檔的 Clip (請照順序拖曳)")]
    public AudioClip[] ruleClips;

    [Header("時間設定")]
    [Tooltip("遊戲開始時的初始延遲秒數")]
    public float initialDelaySeconds = 3f;

    [Tooltip("在 'treasurebg_rule' 顯示後等待的秒數")]
    public float treasureDisplaySeconds = 3f;

    // === 規則文字陣列 (與語音順序對應) ===
    private string[] ruleTexts = new string[]
    {
        "歡迎來到VR樂園",
        "我們準備了一系列的挑戰任務",
        "任務成功後可以獲得寶箱的鑰匙",
        "現在先來知道挑戰的規則"
    };

    // === Unity 生命週期方法 ===

    void Start()
    {
        // 確保 RuleText 和 treasurebg_rule 一開始是隱藏的
        RuleText_rule.gameObject.SetActive(false);
        treasurebg_rule.SetActive(false);

        // 檢查設定是否完整
        if (voiceAudioSource == null || RuleText_rule == null)
        {
            Debug.LogError("請在 Inspector 中拖曳設定 voiceAudioSource 和 RuleText_rule!");
            return;
        }

        // 啟動主要的遊戲流程協程
        StartCoroutine(StartGameFlow());
    }

    // === 遊戲流程控制協程 ===

    IEnumerator StartGameFlow()
    {
        // 1. 遊戲一開始先停 initialDelaySeconds 秒
        yield return new WaitForSeconds(initialDelaySeconds);

        // 顯示 RuleText_rule 物件
        RuleText_rule.gameObject.SetActive(true);

        // 2. 播放第一階段語音和文字 (索引 0, 1)
        // 從第一段語音開始播放
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
        if (ruleClips.Length > 3)
        {
            yield return StartCoroutine(PlayVoiceAndText(ruleTexts[3], ruleClips[3]));
        }

        Debug.Log("開場流程結束，可以開始遊戲或下一個流程。");
    }

    // === 輔助方法：同步播放語音和文字 ===

    IEnumerator PlayVoiceAndText(string text, AudioClip clip)
    {
        // 寫入文字
        RuleText_rule.text = text;

        // 播放語音
        voiceAudioSource.PlayOneShot(clip);

        // 等待語音播放完畢 (語音的長度就是等待的時間)
        yield return new WaitForSeconds(clip.length);
    }
}