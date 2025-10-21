using UnityEngine;
using TMPro;
using Firebase.Firestore;
using System.Collections.Generic;
using System.Collections;
using UnityEngine.UI;

public class TestHistoryUI : MonoBehaviour
{
    private FirebaseManager_Firestore firebaseManager;
    [SerializeField] private Transform listParent;
    [SerializeField] private GameObject itemPrefab;
    public Button endGameButton; 

    public AudioSource audioSource;
    public AudioClip confirmSound;
    [Range(0f, 1f)] public float confirmVolume = 1f;

    void Start()
    {
        FirebaseManager_Firestore.Instance.LoadRecentTests(5, OnRecentTestsLoaded);
    }

    private void OnRecentTestsLoaded(bool success, List<DocumentSnapshot> docs)
    {
        if (!success || docs == null)
        {
            Debug.LogWarning("❌ 無法載入測驗紀錄");
            return;
        }

        // 清空舊資料
        foreach (Transform child in listParent)
            Destroy(child.gameObject);

        Debug.Log($"✅ 成功載入 {docs.Count} 筆測驗紀錄");

        int index = 1;
        foreach (var doc in docs)
        {
            var data = doc.ToDictionary();

            string startTimestamp;

            if (data.ContainsKey("startTimestamp") && data["startTimestamp"] is Timestamp ts)
            {
                startTimestamp = ts.ToDateTime().ToLocalTime().ToString("yyyy/MM/dd HH:mm:ss");
            }
            else
            {
                startTimestamp = data.ContainsKey("startTimestamp") ? data["startTimestamp"].ToString() : "N/A";
            }

            string totalScore = data.ContainsKey("totalScore") ? data["totalScore"].ToString() : "N/A";
            string totalTime = data.ContainsKey("totalTime") ? data["totalTime"].ToString() : "N/A";


            GameObject item = Instantiate(itemPrefab, listParent, false);
            TMP_Text textComponent = item.GetComponentInChildren<TMP_Text>();
            if (textComponent != null)
                textComponent.text = $"#{index} | {startTimestamp} | 分數: {totalScore} | 時間: {totalTime}";

            Debug.Log($"#{index} | 開始時間: {startTimestamp} | 分數: {totalScore} | 花費時間: {totalTime}");
            index++;
        }
    }

    public void OnContinueButtonClicked()
    {
        StartCoroutine(HandleContinueButton());
    }

    private IEnumerator HandleContinueButton()
    {
        if (confirmSound != null && audioSource != null)
            audioSource.PlayOneShot(confirmSound, confirmVolume);

        yield return new WaitForSeconds(0.5f);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;  // 在 Editor 中停止播放
#else
        Application.Quit();  // 在 Build 後結束遊戲
#endif
    }
}
