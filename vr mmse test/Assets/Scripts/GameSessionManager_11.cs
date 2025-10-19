using System.Collections.Generic;
using UnityEngine;

public class GameSessionManager : MonoBehaviour
{
    public static GameSessionManager Instance;

    // 用來紀錄每個場景被玩了幾次
    private Dictionary<string, int> scenePlayCount = new Dictionary<string, int>();

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 保持跨場景不被摧毀
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public int GetNextRoundNumber(string sceneName)
    {
        if (!scenePlayCount.ContainsKey(sceneName))
            scenePlayCount[sceneName] = 0;

        scenePlayCount[sceneName]++;
        return scenePlayCount[sceneName];
    }
}
