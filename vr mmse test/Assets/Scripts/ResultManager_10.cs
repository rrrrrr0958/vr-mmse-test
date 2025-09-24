using System.Collections.Generic;
using UnityEngine;

public class ResultManager : MonoBehaviour
{
    public static ResultManager instance;

    void Awake()
    {
        if (instance == null) instance = this;
    }

    // 每回合結束呼叫（由 GameManager 觸發）
    public void OnRoundFinished(List<string> clicked, List<string> correct, float accuracy, float timeUsed)
    {
        // 這裡可以擴充：寫檔 / 上傳 / 儲存到資料庫…
        GameManager.instance.ConvertGameDataToJson("Player001", accuracy, timeUsed);
    }
}
