using UnityEngine;

public class SeasonSceneryManager : MonoBehaviour
{
    [Header("季節場景")]
    public GameObject springScene;
    public GameObject summerScene;
    public GameObject autumnScene;
    public GameObject winterScene;
    
    private int currentSeasonIndex = -1;
    
    void Start()
    {
        // 自動找到季節場景
        if (springScene == null) springScene = GameObject.Find("SpringScene");
        if (summerScene == null) summerScene = GameObject.Find("SummerScene");
        if (autumnScene == null) autumnScene = GameObject.Find("AutumnScene");
        if (winterScene == null) winterScene = GameObject.Find("WinterScene");
        
        HideAllSeasons();
    }
    
    public void ShowSeason(int seasonIndex)
    {
        HideAllSeasons();
        currentSeasonIndex = seasonIndex;
        
        switch (seasonIndex)
        {
            case 0: // 春天
                if (springScene != null) springScene.SetActive(true);
                break;
            case 1: // 夏天
                if (summerScene != null) summerScene.SetActive(true);
                break;
            case 2: // 秋天
                if (autumnScene != null) autumnScene.SetActive(true);
                break;
            case 3: // 冬天
                if (winterScene != null) winterScene.SetActive(true);
                break;
        }
        
        Debug.Log($"顯示季節: {GetSeasonName(seasonIndex)}");
    }
    
    public int ShowRandomSeason()
    {
        int randomIndex = Random.Range(0, 4);
        ShowSeason(randomIndex);
        return randomIndex;
    }
    
    public void HideAllSeasons()
    {
        if (springScene != null) springScene.SetActive(false);
        if (summerScene != null) summerScene.SetActive(false);
        if (autumnScene != null) autumnScene.SetActive(false);
        if (winterScene != null) winterScene.SetActive(false);
        currentSeasonIndex = -1;
    }
    
    public int GetCurrentSeasonIndex()
    {
        return currentSeasonIndex;
    }
    
    public string GetSeasonName(int seasonIndex)
    {
        return seasonIndex switch
        {
            0 => "春天",
            1 => "夏天",
            2 => "秋天",
            3 => "冬天",
            _ => "未知"
        };
    }
}