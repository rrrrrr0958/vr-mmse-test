using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LevelIntroDefinition", menuName = "Game/Level Intro Definition")]
public class LevelIntroDefinition : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        [Tooltip("對應場景名稱（SceneManager 內的 sceneName）")]
        public string sceneName;

        [Tooltip("顯示在畫面上的文字，例如：歡迎來到第一關")]
        [TextArea] public string message;

        [Tooltip("要播的語音（可為空）")]
        public AudioClip voiceClip;

        [Tooltip("畫面顯示總秒數（包含淡入淡出）")]
        public float totalDisplaySeconds = 3.5f;

        [Tooltip("淡入秒數")]
        public float fadeInSeconds = 0.35f;
        [Tooltip("淡出秒數")]
        public float fadeOutSeconds = 0.35f;

        [Tooltip("是否在此場景顯示導入（可快速停用某關）")]
        public bool enabled = true;
    }

    public List<Entry> entries = new List<Entry>();

    public bool TryGet(string sceneName, out Entry entry)
    {
        entry = entries.Find(e => e.enabled && e.sceneName == sceneName);
        return entry != null;
    }
}
