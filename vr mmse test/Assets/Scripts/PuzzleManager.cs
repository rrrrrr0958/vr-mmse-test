// PuzzleManager.cs
using System;
using UnityEngine;

public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Instance { get; private set; }

    [SerializeField] private int totalPieces = 11;
    private bool[] piecesCollected;

    // 當某片被收集時發事件 (index)
    public event Action<int> OnPieceCollected;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            piecesCollected = new bool[totalPieces];
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // 嘗試收集指定 index，回傳是否為「新收集」
    public bool TryCollectPiece(int index)
    {
        if (index < 0 || index >= piecesCollected.Length)
        {
            Debug.LogWarning($"PuzzleManager: TryCollectPiece index {index} out of range.");
            return false;
        }
        if (piecesCollected[index]) return false; // 已收過

        piecesCollected[index] = true;
        OnPieceCollected?.Invoke(index);
        return true;
    }

    // 嘗試收集下一個尚未收集的片（在單場景重複測試時很方便）
    public bool TryCollectNext()
    {
        for (int i = 0; i < piecesCollected.Length; i++)
        {
            if (!piecesCollected[i])
            {
                piecesCollected[i] = true;
                OnPieceCollected?.Invoke(i);
                return true;
            }
        }
        return false;
    }

    public bool IsPieceCollected(int index)
    {
        if (index < 0 || index >= piecesCollected.Length) return false;
        return piecesCollected[index];
    }

    public int CollectedCount
    {
        get
        {
            int c = 0;
            foreach (var b in piecesCollected) if (b) c++;
            return c;
        }
    }

    public bool IsComplete() => CollectedCount >= piecesCollected.Length;

    // 若需要：重置（測試用）
    public void ResetAll()
    {
        for (int i = 0; i < piecesCollected.Length; i++) piecesCollected[i] = false;
    }
}
