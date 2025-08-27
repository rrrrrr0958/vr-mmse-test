using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[CreateAssetMenu(menuName="Location/LocationDB")]
public class LocationDB : ScriptableObject
{
    public List<LocationEntry> entries = new();

    public List<LocationEntry> GetByScene(string scene)
        => entries.Where(e => e.sceneName == scene).ToList(); // 一個場景可有多攤（F2 有 Fish/Meat）

    public List<LocationEntry> GetDistractors(LocationEntry correct, int count)
    {
        var pool = entries.Where(e => e != correct).ToList();
        pool = pool.OrderByDescending(e => e.floorLabel == correct.floorLabel)
                   .ThenByDescending(e => e.stallLabel == correct.stallLabel)
                   .ToList();
        return pool.Take(count).ToList();
    }
}
