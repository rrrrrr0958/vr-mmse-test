using System;
using UnityEngine; // 確保有這個，如果你在 Unity 裡使用 JsonUtility

[System.Serializable]
public class RecognitionResponse
{
    public string transcription;
    // 為了涵蓋 Flask 可能返回的錯誤，最好也加上 error 欄位
    public string error; 
}