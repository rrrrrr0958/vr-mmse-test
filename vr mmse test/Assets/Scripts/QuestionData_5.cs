using UnityEngine;
using System; // 引入 System 命名空間，因為我們需要 [Serializable] 屬性

// [Serializable] 屬性讓這個類別的物件可以在 Unity Inspector 中顯示和編輯
[Serializable]
public class QuestionData
{
    public string questionText; // 題目的文字內容
    public AudioClip questionAudio; // 題目語音的 Audio Clip
}