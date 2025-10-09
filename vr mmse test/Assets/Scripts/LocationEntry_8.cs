using UnityEngine;

[System.Serializable]
public class LocationEntry
{
    [Header("識別")]
    [Tooltip("場景名稱，例如 \"F1\" / \"F2\" / \"F3\"")]
    public string sceneName;
    [Tooltip("樓層標籤（顯示用），例如 \"一樓(F1)\" 或 \"F1\"")]
    public string floorLabel;
    [Tooltip("攤位標籤（顯示用），例如 \"麵包攤\" / \"魚攤\"")]
    public string stallLabel;
    [Tooltip("與場景中 Viewpoint 物件名稱一致，例如 \"VP_Bakery\"")]
    public string viewpointName;

    [Header("顯示文字")]
    [TextArea]
    [Tooltip("題目/選項顯示，例如「一樓 麵包攤」。若留空，會自動使用 floorLabel + stallLabel。")]
    public string displayText;

    /// <summary>
    /// 取得此條目的顯示文字。若未設定 displayText，則以 floorLabel + stallLabel 自動拼字。
    /// </summary>
    public string GetDisplayText()
    {
        if (!string.IsNullOrWhiteSpace(displayText))
            return displayText.Trim();

        string floor = string.IsNullOrWhiteSpace(floorLabel) ? "" : floorLabel.Trim();
        string stall = string.IsNullOrWhiteSpace(stallLabel) ? "" : stallLabel.Trim();

        if (!string.IsNullOrEmpty(floor) && !string.IsNullOrEmpty(stall))
            return $"{floor} {stall}";
        if (!string.IsNullOrEmpty(floor))
            return floor;
        if (!string.IsNullOrEmpty(stall))
            return stall;

        // 最後保底用 viewpointName，避免出現空字串
        return string.IsNullOrWhiteSpace(viewpointName) ? "(未命名地點)" : viewpointName.Trim();
    }
}
