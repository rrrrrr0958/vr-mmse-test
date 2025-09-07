using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[CreateAssetMenu(fileName = "LocationDB", menuName = "MMSE/LocationDB")]
public class LocationDB : ScriptableObject {
    public List<LocationEntry> entries = new();

    public LocationEntry FindBySceneAndVP(string sceneName, string vpName) {
        return entries.FirstOrDefault(e => e.sceneName == sceneName && e.viewpointName == vpName);
    }

    public IEnumerable<LocationEntry> AllByScene(string sceneName) =>
        entries.Where(e => e.sceneName == sceneName);

    /// <summary>產生包含正解的隨機選項清單（長度為 count；不足時盡力填滿）</summary>
    public List<LocationEntry> RandomOptions(int count, LocationEntry mustInclude, System.Random rng = null) {
        rng ??= new System.Random();

        var pool = entries.Where(e => e != mustInclude)
                          .OrderBy(_ => rng.Next())
                          .Take(Mathf.Max(0, count - 1))
                          .ToList();

        pool.Add(mustInclude);
        return pool.OrderBy(_ => rng.Next()).ToList();
    }
}
