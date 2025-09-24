using UnityEngine;

public class PlayIntroAudio : MonoBehaviour
{
    private AudioSource audioSource;

    void Start()
    {
        // 取得 AudioSource 元件
        audioSource = GetComponent<AudioSource>();

        // 遊戲開始時播放音檔
        if (audioSource != null && audioSource.clip != null)
        {
            audioSource.Play();
        }
        else
        {
            Debug.LogWarning("AudioSource 或 Clip 尚未設定！");
        }
    }
}
