using UnityEngine;
using System.Collections;

public class BackgroundVoice2 : MonoBehaviour
{
    public AudioSource broadcastSource;
    public RecordingState2 recorder;
    public HostFlask2 hostFlask; // 要在 Inspector 綁定 Flask_manager 上的 HostFlask2

    public AudioClip clip1;  // 魚肉特價快來買
    public AudioClip clip2;  // 雞豬牛羊都有賣
    public AudioClip clip3;  // 早起買菜精神好

    private string[] sentences = new string[]
    {
        "魚肉特價快來買",
        "雞豬牛羊都有賣",
        "早起買菜精神好"
    };

    void Start()
    {
        StartCoroutine(StartGameSequence());
    }

    IEnumerator StartGameSequence()
    {
        Debug.Log("[BackgroundVoice2] 遊戲開始，準備延遲 3 秒播放音檔");

        yield return new WaitForSeconds(3f);

        // 防呆：確認 hostFlask 有綁定
        if (hostFlask == null)
        {
            Debug.LogError("[BackgroundVoice2] hostFlask 未綁定，請確認 Inspector 是否正確設定！");
            yield break;
        }

        // 🎲 隨機抽一題
        int index = Random.Range(0, sentences.Length);
        hostFlask.targetSentence = sentences[index];

        switch (index)
        {
            case 0: broadcastSource.clip = clip1; break;
            case 1: broadcastSource.clip = clip2; break;
            case 2: broadcastSource.clip = clip3; break;
        }

        // 防呆：確認 broadcastSource 有綁定
        if (broadcastSource == null)
        {
            Debug.LogError("[BackgroundVoice2] broadcastSource 未綁定，請確認 Inspector 是否正確設定！");
            yield break;
        }

        // 🔥 保險程式碼：自動啟用物件與元件
        if (!broadcastSource.gameObject.activeInHierarchy)
        {
            Debug.LogWarning("[BackgroundVoice2] Broadcast_2 目前是 Inactive，自動啟用");
            broadcastSource.gameObject.SetActive(true);
        }
        if (!broadcastSource.enabled)
        {
            Debug.LogWarning("[BackgroundVoice2] AudioSource 元件被關閉，自動啟用");
            broadcastSource.enabled = true;
        }

        // 播放音檔
        broadcastSource.Play();
        Debug.Log("[BackgroundVoice2] 播放題目：" + hostFlask.targetSentence);

        yield return new WaitForSeconds(broadcastSource.clip.length);

        // 等待 3 秒
        yield return new WaitForSeconds(3f);

        // 錄音
        recorder.StartRecording(7f);
    }
}
