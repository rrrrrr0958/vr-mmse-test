using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour {
    /// <summary>給 Button 用，直接載入場景（確保場景已加進 Build Settings）</summary>
    public void LoadScene(string sceneName) {
        if (string.IsNullOrEmpty(sceneName)) return;
        SceneManager.LoadScene(sceneName);
    }

    /// <summary>靜態便捷方法</summary>
    public static void Load(string sceneName) {
        if (string.IsNullOrEmpty(sceneName)) return;
        SceneManager.LoadScene(sceneName);
    }
}
