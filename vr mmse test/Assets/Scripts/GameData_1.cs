using System;
using System.Collections.Generic;

// 標記為 [System.Serializable] 才能讓 Unity 的 JsonUtility 處理它
[System.Serializable]
public class GameData
{
    public string playerId;
    public string timestamp;

    public List<string> clickedAnimalSequence;
    public List<string> correctAnswerSequence;

    public float timeUsed;
    public float accuracy;

    // 建構函式
    public GameData(string id,
                    List<string> clicked,
                    List<string> correct,
                    float timeUsed,
                    float accuracy)
    {
        this.playerId = id;
        this.timestamp = DateTime.UtcNow.ToString("o");

        this.clickedAnimalSequence = clicked;
        this.correctAnswerSequence = correct;

        this.timeUsed = timeUsed;
        this.accuracy = accuracy;
    }
}
