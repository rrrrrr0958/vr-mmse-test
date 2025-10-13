using System;
using System.Collections.Generic;

[System.Serializable]
public class GameMultiAttemptData
{
    public List<string> correctAnswers = new List<string>();
    public List<GameAttempt> attempts = new List<GameAttempt>();
}

[System.Serializable]
public class GameAttempt
{
    public int round;
    public List<string> selected;
    public int correctCount;
    public float timeUsed;
}
