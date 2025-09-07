using UnityEngine;

[System.Serializable]
public class LocationEntry {
    [Header("識別")]
    public string sceneName;      // 例如 "F1"
    public string floorLabel;     // 例如 "F1"
    public string stallLabel;     // 例如 "Bakery"
    public string viewpointName;  // 與場景中 VP 物件名稱一致（例如 "VP_Bakery"）

    [Header("顯示文字")]
    [TextArea] public string displayText; // 題目/選項顯示，例如「一樓 麵包攤」
}
