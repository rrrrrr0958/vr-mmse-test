using System;
using System.Collections.Generic;

[Serializable]
public class GameDataMenu
{
    public string playerId;
    public string timestamp;
    public List<string> selections;

    public GameDataMenu(string playerId, List<string> selections)
    {
        this.playerId = playerId;
        this.timestamp = DateTime.UtcNow.ToString("o");
        this.selections = new List<string>(selections);
    }

    // 預設建構函式（JsonUtility.FromJson需要）
    public GameDataMenu()
    {
        this.selections = new List<string>();
    }
}