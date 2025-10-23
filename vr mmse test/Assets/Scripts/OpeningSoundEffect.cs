using UnityEngine;
using UnityEngine.UI;

public class OpeningSoundEffect : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource audioSource;       // 播放音效的 AudioSource
    public AudioClip clickSound;          // 按鈕點擊音效
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Button Target (Optional)")]
    public Button targetButton;           // 指定目標按鈕（可不填）

    private void Awake()
    {
        // ✅ 確保 AudioSource 存在
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        // ✅ 預載音效，避免播放時延遲
        if (clickSound != null)
            clickSound.LoadAudioData();
    }

    private void Start()
    {
        // ✅ 如果未手動指定按鈕，就嘗試抓取自己物件上的 Button
        if (targetButton == null)
            targetButton = GetComponent<Button>();

        if (targetButton != null)
        {
            targetButton.onClick.AddListener(PlayClickSound);
        }
    }

    public void PlayClickSound()
    {
        if (clickSound != null && audioSource != null)
        {
            audioSource.PlayOneShot(clickSound, volume);
        }
    }
}
