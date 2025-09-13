using System;
using System.Collections.Generic;

[Serializable]
public class GameDataMenu
{
    public string playerId;
    public string timestamp;
    public string[] selections;   // ← 改成陣列，避免 JsonUtility 只留最後一個

    public GameDataMenu(string playerId, List<string> selections)
    {
        this.playerId = playerId;
        this.timestamp = DateTime.UtcNow.ToString("o");
        this.selections = selections != null ? selections.ToArray() : new string[0];
    }

    // 預設建構函式（JsonUtility.FromJson需要）
    public GameDataMenu()
    {
        this.selections = new string[0];
    }

    // （可選）如果哪裡需要 List，提供轉換
    public List<string> ToList() => new List<string>(selections ?? new string[0]);
}
