using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public List<string> clickedAnimalSequence = new List<string>();
    public List<string> correctAnswerSequence = new List<string> { "兔子", "熊貓", "鹿" };

    private float startTime;
    private float endTime;

    void Start()
    {
        if (instance == null) instance = this;
        startTime = Time.time;
    }

    public void OnAnimalButtonClick(string animalName)
    {
        clickedAnimalSequence.Add(animalName);

        if (clickedAnimalSequence.Count == correctAnswerSequence.Count)
        {
            endTime = Time.time;
            ResultManager.instance.ShowResult(clickedAnimalSequence, correctAnswerSequence, startTime, endTime);
        }
    }

   // ✅ 加了 playerId 的預設值 "Guest"
    public string ConvertGameDataToJson(string playerId = "Guest", float accuracy = 0, float timeUsed = 0)
    {
        GameData data = new GameData(
            playerId,
            this.clickedAnimalSequence,
            this.correctAnswerSequence,
            timeUsed,
            accuracy
        );

        string json = JsonUtility.ToJson(data, true);
        Debug.Log("📄 遊戲數據 JSON:\n" + json);
        return json;
    }

}
