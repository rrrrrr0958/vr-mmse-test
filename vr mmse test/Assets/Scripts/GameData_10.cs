using System;
using System.Collections.Generic;

[Serializable]
public class GameData
{
    public string playerId;
    public string timestamp;
    public List<string> clickedAnimalSequence;
    public List<string> correctAnswerSequence;
    public float accuracy;
    public float timeUsed;

    public GameData(string playerId,
                    List<string> clicked,
                    List<string> correct,
                    float accuracy,
                    float timeUsed)
    {
        this.playerId = playerId;
        this.timestamp = DateTime.UtcNow.ToString("o");
        this.clickedAnimalSequence = new List<string>(clicked);
        this.correctAnswerSequence = new List<string>(correct);
        this.accuracy = accuracy;
        this.timeUsed = timeUsed;
    }
}
