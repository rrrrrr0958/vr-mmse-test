using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using Firebase.Firestore;
using Firebase.Storage;
using UnityEngine;
using System;
using System.Collections.Generic;

public class FirebaseManager_Firestore : MonoBehaviour
{
    public static FirebaseManager_Firestore Instance;

    public FirebaseAuth auth;
    public FirebaseUser user;
    public FirebaseFirestore firestore;
    public FirebaseStorage storage;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(this.gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        InitializeFirebase();
    }

    async void InitializeFirebase()
    {
        var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (dependencyStatus == DependencyStatus.Available)
        {
            auth = FirebaseAuth.DefaultInstance;
            firestore = FirebaseFirestore.DefaultInstance;
            storage = FirebaseStorage.DefaultInstance;
            auth.StateChanged += AuthStateChanged;
            Debug.Log("✅ Firebase 初始化完成");
        }
        else
        {
            Debug.LogError("❌ Firebase 初始化失敗: " + dependencyStatus);
        }
    }

    void OnDestroy()
    {
        if (auth != null)
            auth.StateChanged -= AuthStateChanged;
    }

    void AuthStateChanged(object sender, EventArgs eventArgs)
    {
        if (auth.CurrentUser != user)
        {
            bool signedIn = user != auth.CurrentUser && auth.CurrentUser != null;
            if (!signedIn && user != null)
            {
                Debug.Log("登出或無使用者");
            }
            user = auth.CurrentUser;
            if (signedIn)
            {
                Debug.Log($"登入：{user.Email}");
            }
        }
    }

    // -------------------------------------------------------------------
    // 🔹 註冊新使用者
    // -------------------------------------------------------------------
    public void Register(string email, string password, Action<bool, string> callback = null)
    {
        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("註冊失敗: " + task.Exception);
                callback?.Invoke(false, task.Exception.Message);
                return;
            }

            user = task.Result.User;
            Debug.Log("✅ 註冊成功：" + user.Email);

            // 註冊後建立 Firestore 文件
            DocumentReference docRef = firestore.Collection("Users").Document(user.UserId);
            Dictionary<string, object> data = new Dictionary<string, object>
            {
                { "email", user.Email },
                { "gender", "" },
                { "age", "" },
                { "createdAt", Timestamp.GetCurrentTimestamp() }
            };
            docRef.SetAsync(data).ContinueWithOnMainThread(createTask =>
            {
                if (createTask.IsFaulted)
                {
                    Debug.LogWarning("建立使用者文件失敗: " + createTask.Exception);
                    callback?.Invoke(false, createTask.Exception.Message);
                }
                else
                {
                    Debug.Log("✅ Firestore 使用者文件建立成功");
                    callback?.Invoke(true, null);
                }
            });
        });
    }

    // -------------------------------------------------------------------
    // 🔹 登入
    // -------------------------------------------------------------------
    public void Login(string email, string password, Action<bool, string> callback = null)
    {
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWithOnMainThread(task =>
        {
            if (task.IsCanceled || task.IsFaulted)
            {
                Debug.LogError("登入失敗: " + task.Exception);
                callback?.Invoke(false, task.Exception.Message);
                return;
            }

            user = task.Result.User;
            Debug.Log("✅ 登入成功：" + user.Email);
            callback?.Invoke(true, null);
        });
    }

    // -------------------------------------------------------------------
    // 🔹 登出
    // -------------------------------------------------------------------
    public void Logout()
    {
        if (auth != null)
        {
            auth.SignOut();
            user = null;
            Debug.Log("✅ 登出成功");
        }
    }

    // -------------------------------------------------------------------
    // 🔹 使用者資料：年齡與性別
    // -------------------------------------------------------------------
    public void SetUserProfile(string age, string gender, Action<bool, string> callback = null)
    {
        Debug.Log($"[SetUserProfile] Received -> age: {age ?? "NULL"}, gender: {gender ?? "NULL"}");

        if (user == null)
        {
            Debug.LogWarning("⚠️ SetUserProfile: user == null");
            callback?.Invoke(false, "No user logged in");
            return;
        }

        DocumentReference docRef = firestore.Collection("Users").Document(user.UserId);
        Dictionary<string, object> data = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(age))
            data["age"] = age;

        if (!string.IsNullOrEmpty(gender))
            data["gender"] = gender;

        if (data.Count == 0)
        {
            Debug.Log("⚠️ SetUserProfile: 沒有欄位可更新");
            callback?.Invoke(true, null);
            return;
        }

        // data["updatedAt"] = FieldValue.ServerTimestamp;

        docRef.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogWarning("SetUserProfile 失敗: " + task.Exception);
                callback?.Invoke(false, task.Exception.Message);
            }
            else
            {
                Debug.Log($"✅ SetUserProfile 成功，寫入欄位: {string.Join(", ", data.Keys)}");
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
        docRef.GetSnapshotAsync().ContinueWithOnMainThread(task =>
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
                    Debug.Log("⚠️ GetUserProfile：Document 不存在");
                    callback?.Invoke(false, null);
                }
            }
        });
    }

    // -------------------------------------------------------------------
    // 🔹 測驗紀錄與上傳功能
    // -------------------------------------------------------------------

    public string GenerateTestId()
    {
        return DateTime.Now.ToString("yyyyMMdd_HHmmss");
    }

    // public void SaveTestResult(string testId, int totalScore, float totalTime, string timestamp, Action<bool, string> callback = null)
    public void SaveTestResult(string testId, int totalScore, float totalTime, Action<bool, string> callback = null)
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
            { "timestamp", FieldValue.ServerTimestamp},
            { "totalScore", totalScore },
            { "totalTime", totalTime }
        };

        testDoc.SetAsync(data).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogWarning("SaveTestResult 失敗: " + task.Exception);
                callback?.Invoke(false, task.Exception.Message);
            }
            else
            {
                Debug.Log("✅ SaveTestResult 成功");
                callback?.Invoke(true, null);
            }
        });
    }

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

        levelDoc.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogWarning("SaveLevelData 失敗: " + task.Exception);
                callback?.Invoke(false, task.Exception.Message);
            }
            else
            {
                Debug.Log($"✅ SaveLevelData 成功: level_{levelIndex}");
                callback?.Invoke(true, null);
            }
        });
    }

    public void UploadFile(byte[] fileBytes, string storagePath, Action<bool, string> callback)
    {
        StorageReference storageRef = storage.GetReference(storagePath);
        storageRef.PutBytesAsync(fileBytes).ContinueWithOnMainThread(uploadTask =>
        {
            if (uploadTask.IsFaulted || uploadTask.IsCanceled)
            {
                Debug.LogError("UploadFile 失敗: " + uploadTask.Exception);
                callback?.Invoke(false, uploadTask.Exception.Message);
            }
            else
            {
                storageRef.GetDownloadUrlAsync().ContinueWithOnMainThread(urlTask =>
                {
                    if (urlTask.IsFaulted || urlTask.IsCanceled)
                    {
                        Debug.LogError("GetDownloadUrl 失敗: " + urlTask.Exception);
                        callback?.Invoke(false, urlTask.Exception.Message);
                    }
                    else
                    {
                        string downloadUrl = urlTask.Result.ToString();
                        Debug.Log("📤 檔案 URL: " + downloadUrl);
                        callback?.Invoke(true, downloadUrl);
                    }
                });
            }
        });
    }

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
            string key = kv.Key;
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
                        levelDoc.SetAsync(merge, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
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

        col.OrderByDescending("timestamp").Limit(limit).GetSnapshotAsync().ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogWarning("LoadRecentTests 失敗: " + task.Exception);
                callback?.Invoke(false, null);
            }
            else
            {
                QuerySnapshot snap = task.Result;
                callback?.Invoke(true, new List<DocumentSnapshot>(snap.Documents));
            }
        });
    }
}
