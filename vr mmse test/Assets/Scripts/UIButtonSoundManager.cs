using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;

public class UIButtonSoundManager : MonoBehaviour
{
    [Header("Audio Settings")]
    public AudioSource audioSource;          // 播放音效的 AudioSource
    [Range(0f, 1f)] public float volume = 1f;

    [Header("Sound Clips")]
    public AudioClip keyboardClickSound;     // 鍵盤按鍵音效
    public AudioClip uiClickSound;           // 其他 UI 按鈕音效

    [Header("Button Groups")]
    public Transform keyboardRoot;           // 虛擬鍵盤的父物件（包含所有鍵盤按鈕）
    public Transform uiRoot;                 // 其他 UI 的父物件（可不填，會自動搜尋）

    private List<Button> keyboardButtons = new List<Button>();
    private List<Button> uiButtons = new List<Button>();

    void Awake()
    {
        // ✅ 確保 AudioSource 存在
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }

        // ✅ 預載音效，避免延遲
        if (keyboardClickSound != null)
            keyboardClickSound.LoadAudioData();
        if (uiClickSound != null)
            uiClickSound.LoadAudioData();

        // ✅ 綁定鍵盤按鈕
        if (keyboardRoot != null)
        {
            keyboardButtons.AddRange(keyboardRoot.GetComponentsInChildren<Button>(true));
            foreach (Button btn in keyboardButtons)
            {
                btn.onClick.AddListener(PlayKeyboardSound);
            }
        }

        // ✅ 綁定非鍵盤按鈕
        if (uiRoot == null)
            uiRoot = this.transform; // 如果沒特別指定，就抓整個場景裡的

        uiButtons.AddRange(uiRoot.GetComponentsInChildren<Button>(true));
        foreach (Button btn in uiButtons)
        {
            // 避免重複綁到鍵盤的
            if (!keyboardButtons.Contains(btn))
                btn.onClick.AddListener(PlayUISound);
        }
    }

    public void PlayKeyboardSound()
    {
        if (keyboardClickSound != null && audioSource != null)
        {
            audioSource.pitch = Random.Range(0.95f, 1.05f); // 可選：增加自然感
            audioSource.PlayOneShot(keyboardClickSound, volume);
        }
    }

    public void PlayUISound()
    {
        if (uiClickSound != null && audioSource != null)
        {
            audioSource.pitch = 1f;
            audioSource.PlayOneShot(uiClickSound, volume);
        }
    }
}
