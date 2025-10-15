using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using Firebase.Storage;
using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;

public class FirebaseManager_Firestore : MonoBehaviour
{
    public FirebaseAuth auth;
    public FirebaseUser user;
    public FirebaseFirestore firestore;
    public FirebaseStorage storage;

    void Awake()
    {
        auth = FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;

        firestore = FirebaseFirestore.DefaultInstance;
        storage = FirebaseStorage.DefaultInstance;

        DontDestroyOnLoad(this.gameObject); // 保持此物件在跨場景存在
    }

    void OnDestroy()
    {
        auth.StateChanged -= AuthStateChanged;
    }

    void AuthStateChanged(object sender, EventArgs eventArgs)
    {
        if (auth.CurrentUser != user)
        {
            user = auth.CurrentUser;
            if (user != null)
            {
                Debug.Log($"登入：{user.Email}");
            }
            else
            {
                Debug.Log("登出或無使用者");
            }
        }
    }

    // ------------------ 使用者基本資料儲存（age / gender） ------------------

    public void SetUserProfile(string age, string gender, Action<bool, string> callback = null)
    {
        if (user == null)
        {
            callback?.Invoke(false, "No user logged in");
            return;
        }
        DocumentReference docRef = firestore.Collection("Users").Document(user.UserId);
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "age", age },
            { "gender", gender },
            { "updatedAt", FieldValue.ServerTimestamp }
        };
        docRef.SetAsync(data, SetOptions.MergeAll).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogWarning("SetUserProfile 失敗: " + task.Exception);
                callback?.Invoke(false, task.Exception.Message);
            }
            else
            {
                Debug.Log("SetUserProfile 成功");
                callback?.Invoke(true, null);
            }
        });
    }

    public void GetUserProfile(Action<bool, Dictionary<string, object>> callback)
    {
        if (user == null)
        {
            callback?.Invoke(false, null);
            return;
        }
        DocumentReference docRef = firestore.Collection("Users").Document(user.UserId);
        docRef.GetSnapshotAsync().ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogWarning("GetUserProfile 失敗: " + task.Exception);
                callback?.Invoke(false, null);
            }
            else
            {
                DocumentSnapshot snap = task.Result;
                if (snap.Exists)
                {
                    var dict = snap.ToDictionary();
                    callback?.Invoke(true, dict);
                }
                else
                {
                    Debug.Log("GetUserProfile：Document 不存在");
                    callback?.Invoke(false, null);
                }
            }
        });
    }

    // ------------------ 測驗紀錄 + 關卡資料 + 檔案上傳 ------------------

    public string GenerateTestId()
    {
        return DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }

    // 存一筆完整測驗（總成績 + 時間）， levels 子集合可後續更新
    public void SaveTestResult(string testId, int totalScore, float totalTime, string timestamp, Action<bool, string> callback = null)
    {
        if (user == null)
        {
            callback?.Invoke(false, "No user");
            return;
        }
        DocumentReference testDoc = firestore.Collection("Users")
                                             .Document(user.UserId)
                                             .Collection("testResults")
                                             .Document(testId);
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "timestamp", timestamp },
            { "totalScore", totalScore },
            { "totalTime", totalTime }
        };
        testDoc.SetAsync(data).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogWarning("SaveTestResult 失敗: " + task.Exception);
                callback?.Invoke(false, task.Exception.Message);
            }
            else
            {
                Debug.Log("SaveTestResult 成功");
                callback?.Invoke(true, null);
            }
        });
    }

    // 儲存單一關卡資料（不含檔案 URL）
    public void SaveLevelData(string testId, int levelIndex, int score, float time, Action<bool, string> callback = null)
    {
        if (user == null)
        {
            callback?.Invoke(false, "No user");
            return;
        }
        DocumentReference levelDoc = firestore.Collection("Users")
                                               .Document(user.UserId)
                                               .Collection("testResults")
                                               .Document(testId)
                                               .Collection("levels")
                                               .Document("level_" + levelIndex);
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "score", score },
            { "time", time }
        };
        levelDoc.SetAsync(data, SetOptions.MergeAll).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogWarning("SaveLevelData 失敗: " + task.Exception);
                callback?.Invoke(false, task.Exception.Message);
            }
            else
            {
                Debug.Log($"SaveLevelData 成功: level_{levelIndex}");
                callback?.Invoke(true, null);
            }
        });
    }

    // 上傳檔案並取得下載 URL
    public void UploadFile(byte[] fileBytes, string storagePath, Action<bool, string> callback)
    {
        StorageReference storageRef = storage.GetReference(storagePath);
        storageRef.PutBytesAsync(fileBytes).ContinueWith(uploadTask =>
        {
            if (uploadTask.IsFaulted || uploadTask.IsCanceled)
            {
                Debug.LogError("UploadFile 失敗: " + uploadTask.Exception);
                callback?.Invoke(false, uploadTask.Exception.Message);
            }
            else
            {
                storageRef.GetDownloadUrlAsync().ContinueWith(urlTask =>
                {
                    if (urlTask.IsFaulted || urlTask.IsCanceled)
                    {
                        Debug.LogError("GetDownloadUrl 失敗: " + urlTask.Exception);
                        callback?.Invoke(false, urlTask.Exception.Message);
                    }
                    else
                    {
                        string downloadUrl = urlTask.Result.ToString();
                        Debug.Log("檔案 URL: " + downloadUrl);
                        callback?.Invoke(true, downloadUrl);
                    }
                });
            }
        });
    }

    // 上傳多個檔案並把 URL 寫入對應 level doc 中的 fields
    public void UploadFilesAndSaveUrls(string testId, int levelIndex, Dictionary<string, byte[]> files, Action<bool, string> callback = null)
    {
        if (user == null)
        {
            callback?.Invoke(false, "No user");
            return;
        }

        string basePath = $"users/{user.UserId}/testResults/{testId}/level_{levelIndex}/";
        Dictionary<string, object> urlFields = new Dictionary<string, object>();

        int total = files.Count;
        int completed = 0;
        bool errOccurred = false;
        string errMsg = null;

        foreach (var kv in files)
        {
            string key = kv.Key;           // e.g. "image", "audio", "video"
            byte[] data = kv.Value;
            string fname = $"{key}_{Guid.NewGuid().ToString().Substring(0, 8)}";
            string path = basePath + fname;

            UploadFile(data, path, (ok, result) =>
            {
                if (!ok)
                {
                    errOccurred = true;
                    errMsg = result;
                }
                else
                {
                    urlFields[key + "Url"] = result;
                }

                completed++;
                if (completed == total)
                {
                    if (errOccurred)
                    {
                        callback?.Invoke(false, errMsg);
                    }
                    else
                    {
                        // 把 URL fields 寫入該 level document under field "files"
                        DocumentReference levelDoc = firestore.Collection("Users")
                                                               .Document(user.UserId)
                                                               .Collection("testResults")
                                                               .Document(testId)
                                                               .Collection("levels")
                                                               .Document("level_" + levelIndex);
                        Dictionary<string, object> merge = new Dictionary<string, object>
                        {
                            { "files", urlFields }
                        };
                        levelDoc.SetAsync(merge, SetOptions.MergeAll).ContinueWith(task =>
                        {
                            if (task.IsFaulted)
                                callback?.Invoke(false, task.Exception.Message);
                            else
                                callback?.Invoke(true, null);
                        });
                    }
                }
            });
        }
    }

    // 讀取使用者最近的 testResults（限筆數）
    // 修改後：回傳 List<DocumentSnapshot]，避免使用 QueryDocumentSnapshot 導致的找不到類別錯誤
    public void LoadRecentTests(int limit, Action<bool, List<DocumentSnapshot>> callback)
    {
        if (user == null)
        {
            callback?.Invoke(false, null);
            return;
        }

        CollectionReference col = firestore.Collection("Users")
                                        .Document(user.UserId)
                                        .Collection("testResults");

        col.OrderByDescending("timestamp").Limit(limit).GetSnapshotAsync().ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogWarning("LoadRecentTests 失敗: " + task.Exception);
                callback?.Invoke(false, null);
            }
            else
            {
                QuerySnapshot snap = task.Result;
                // Documents 屬性為 IReadOnlyList<DocumentSnapshot>
                callback?.Invoke(true, new List<DocumentSnapshot>(snap.Documents));
            }
        });
    }

}
