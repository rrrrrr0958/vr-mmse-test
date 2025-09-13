using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System;

[CreateAssetMenu(fileName = "LocationDB", menuName = "MMSE/LocationDB")]
public class LocationDB : ScriptableObject
{
    [Tooltip("地點/攤位清單。請確保 sceneName 與 viewpointName 與場景中的實際物件對得上。")]
    public List<LocationEntry> entries = new();

    // 允許外部查看數量
    public int Count => entries != null ? entries.Count : 0;

    // ========= 主查詢 API（供 SessionController 使用） =========

    /// <summary>以場景名 + Viewpoint 名稱尋找條目（大小寫與空白容錯）。</summary>
    public LocationEntry FindBySceneAndVP(string sceneName, string vpName)
    {
        if (!HasData()) return null;
        string s = Norm(sceneName);
        string v = Norm(vpName);
        return entries.FirstOrDefault(e => Norm(e.sceneName) == s && Norm(e.viewpointName) == v);
    }

    /// <summary>回傳指定場景內的所有條目。</summary>
    public IEnumerable<LocationEntry> AllByScene(string sceneName)
    {
        if (!HasData()) return Enumerable.Empty<LocationEntry>();
        string s = Norm(sceneName);
        return entries.Where(e => Norm(e.sceneName) == s);
    }

    /// <summary>
    /// 以「攤位 ID」查找條目。容忍幾種常見寫法：
    /// 1) 直接傳 viewpointName（最建議）
    /// 2) 直接傳 displayText（例如「一樓 麵包攤」）
    /// 3) "sceneName:viewpointName"（例如 "F1:VP_Bakery"）
    /// 4) "floorLabel stallLabel"（例如「一樓 麵包攤」）
    /// </summary>
    public LocationEntry FindByStallId(string stallId)
    {
        if (!HasData() || string.IsNullOrWhiteSpace(stallId)) return null;

        string key = Norm(stallId);

        // 1) 先用 viewpointName 精準找
        var hit = entries.FirstOrDefault(e => Norm(e.viewpointName) == key);
        if (hit != null) return hit;

        // 2) 用 displayText（或自動組的顯示字串）比對
        hit = entries.FirstOrDefault(e => Norm(e.GetDisplayText()) == key);
        if (hit != null) return hit;

        // 3) "scene:viewpoint" 格式
        var parts = key.Split(':');
        if (parts.Length == 2)
        {
            string s = parts[0];
            string v = parts[1];
            hit = entries.FirstOrDefault(e => Norm(e.sceneName) == s && Norm(e.viewpointName) == v);
            if (hit != null) return hit;
        }

        // 4) "floorLabel stallLabel" 組合
        hit = entries.FirstOrDefault(e => Norm($"{e.floorLabel} {e.stallLabel}") == key);
        if (hit != null) return hit;

        return null;
    }

    /// <summary>
    /// 產生包含正解的隨機選項清單（長度為 count；不足時盡力填滿）。
    /// 如果 entries 太少，會回傳目前可用的全部。
    /// </summary>
    public List<LocationEntry> RandomOptions(int count, LocationEntry mustInclude, System.Random rng = null)
    {
        rng ??= new System.Random();
        if (!HasData())
            return new List<LocationEntry>();

        // 先把正解放進去，再補干擾項
        var result = new List<LocationEntry> { mustInclude };

        // 池：先同場景，後其他場景
        var same = entries.Where(e => !ReferenceEquals(e, mustInclude) && SameScene(e, mustInclude))
                          .OrderBy(_ => rng.Next());
        var others = entries.Where(e => !ReferenceEquals(e, mustInclude) && !SameScene(e, mustInclude))
                            .OrderBy(_ => rng.Next());

        foreach (var e in same.Concat(others))
        {
            if (result.Count >= Mathf.Max(2, count)) break;
            if (!result.Contains(e)) result.Add(e);
        }

        // 洗牌
        return result.OrderBy(_ => rng.Next()).ToList();
    }

    // ========= 工具 & 驗證 =========

    bool HasData() => entries != null && entries.Count > 0;

    static bool SameScene(LocationEntry a, LocationEntry b) =>
        a != null && b != null && Norm(a.sceneName) == Norm(b.sceneName);

    static string Norm(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToLowerInvariant();

    /// <summary>
    /// 清理資料：修剪空白、補 displayText、避免 null 清單。
    /// 在 Inspector 內容變動時自動執行。
    /// </summary>
    void OnValidate()
    {
        if (entries == null)
        {
            entries = new List<LocationEntry>();
            return;
        }

        for (int i = 0; i < entries.Count; i++)
        {
            var e = entries[i];
            if (e == null) continue;

            // 去除常見空白
            e.sceneName     = e.sceneName?.Trim();
            e.floorLabel    = e.floorLabel?.Trim();
            e.stallLabel    = e.stallLabel?.Trim();
            e.viewpointName = e.viewpointName?.Trim();
            // 若 displayText 未填，使用自動組字
            if (string.IsNullOrWhiteSpace(e.displayText))
                e.displayText = e.GetDisplayText();
        }

        // 可選：檢查 viewpointName 是否重複（會影響查找）
        var dupKeys = entries
            .Where(e => e != null && !string.IsNullOrWhiteSpace(e.viewpointName))
            .GroupBy(e => Norm(e.viewpointName))
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .ToList();

        if (dupKeys.Count > 0)
        {
            Debug.LogWarning($"[LocationDB] 發現重複的 viewpointName：{string.Join(", ", dupKeys)}。請確保每個地點的 viewpointName 唯一。");
        }
    }
}
