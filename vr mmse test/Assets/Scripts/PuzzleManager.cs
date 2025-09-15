using System;
using UnityEngine;

public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Instance { get; private set; }

    [SerializeField] private int totalPieces = 11;
    private bool[] piecesCollected;

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

    public bool TryCollectPiece(int index)
    {
        if (index < 0 || index >= piecesCollected.Length) return false;
        if (piecesCollected[index]) return false;
        piecesCollected[index] = true;
        Debug.Log($"[PuzzleManager] Collected piece {index}");
        OnPieceCollected?.Invoke(index);
        return true;
    }

    public bool TryCollectNext()
    {
        for (int i = 0; i < piecesCollected.Length; i++)
        {
            if (!piecesCollected[i])
            {
                piecesCollected[i] = true;
                Debug.Log($"[PuzzleManager] Collected next piece {i}");
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

    public void ResetAll()
    {
        for (int i = 0; i < piecesCollected.Length; i++) piecesCollected[i] = false;
    }
}
