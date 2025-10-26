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
    public int totalScore = 0;

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
            Debug.Log("✅ FirebaseManager 初始化完成並保留跨場景存在。");
        }
        else
        {
            Debug.Log("⚠️ 重複的 FirebaseManager 被銷毀。");
            Destroy(gameObject);
            return;
        }

        InitializeFirebase();
    }

    async void InitializeFirebase()
    {
        var dependencyStatus = await FirebaseApp.CheckAndFixDependenciesAsync();
        if (dependencyStatus != DependencyStatus.Available)
        {
            Debug.LogError("❌ Firebase 初始化失敗: " + dependencyStatus);
            return;
        }
        

        auth = FirebaseAuth.DefaultInstance;
        firestore = FirebaseFirestore.DefaultInstance;
        storage = FirebaseStorage.DefaultInstance;
        auth.StateChanged -= AuthStateChanged;
        auth.StateChanged += AuthStateChanged;

        if(auth.CurrentUser != null)
        {
            user = auth.CurrentUser;
            Debug.Log($"🔁 自動恢復登入：{user.Email}");
        }
        else
        {
            Debug.Log("⚠️ 尚未登入，等待使用者登入...");
        }

        Debug.Log("✅ Firebase 初始化完成");
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
                Debug.Log($"登入：{user.Email}, UID: {user.UserId}");
            }
        }
    }

    public bool IsUserLoggedIn()
    {
        return user != null && auth != null && auth.CurrentUser != null;
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
    public string testId { get; private set; }
    public Timestamp startTimestamp;
    public Timestamp endTimestamp;

    public string GenerateTestId()
    {
        if (user == null)
        {
            Debug.LogWarning("⚠️ 尚未登入，無法產生 Test ID。");
            return null;
        }
        testId = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        Debug.Log("🧩 產生新的 Test ID: " + testId);

        DocumentReference testRef = firestore.Collection("Users")
                                            .Document(user.UserId)
                                            .Collection("tests")
                                            .Document(testId);

        startTimestamp = Timestamp.GetCurrentTimestamp();

        Dictionary<string, object> testData = new Dictionary<string, object>
        {
            { "startTimestamp", startTimestamp }
        };

        testRef.SetAsync(testData).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogError("❌ 建立測驗文件失敗：" + task.Exception);
            }
            else
            {
                Debug.Log($"✅ 已建立測驗文件：{testId}（含 startTimestamp）");
            }
        });

        return testId;
    }

    // public void SaveTestResult(string testId, int totalScore, float totalTime, string timestamp, Action<bool, string> callback = null)
    public void SaveTestResult(string testId, Action<bool, string> callback = null)
    {
        if (user == null)
        {
            callback?.Invoke(false, "No user");
            return;
        }

        DocumentReference testDoc = firestore.Collection("Users")
                                             .Document(user.UserId)
                                             .Collection("tests")
                                             .Document(testId);

        endTimestamp = Timestamp.GetCurrentTimestamp();
        TimeSpan duration = endTimestamp.ToDateTime() - startTimestamp.ToDateTime();
        // double totalSeconds = duration.TotalSeconds;

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "endTimestamp", endTimestamp },
            // { "totalTime", totalSeconds },
            { "totalTime", duration.ToString(@"hh\:mm\:ss") },
            { "totalScore", totalScore }
        };

        testDoc.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
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

    // public void SaveLevelData(string testId, int levelIndex, int score, float time, Action<bool, string> callback = null)
    public void SaveLevelData(string testId, string levelIndex, int score, Action<bool, string> callback = null)

    {
        if (user == null)
        {
            callback?.Invoke(false, "No user");
            return;
        }

        DocumentReference levelDoc = firestore.Collection("Users")
                                               .Document(user.UserId)
                                               .Collection("tests")
                                               .Document(testId)
                                               .Collection("levelResults")
                                               .Document("level_" + levelIndex);

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "score", score },
            { "levelfinishtimestamp", FieldValue.ServerTimestamp}
            // { "time", time }
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

    public void SaveLevelOptions(string testId, string levelIndex, Dictionary<string, string> correctOptions, Dictionary<string, string> chosenOptions, Action<bool, string> callback = null)
    {
        if (user == null)
        {
            callback?.Invoke(false, "No user");
            return;
        }

        DocumentReference levelDoc = firestore.Collection("Users")
                                               .Document(user.UserId)
                                               .Collection("tests")
                                               .Document(testId)
                                               .Collection("levelResults")
                                               .Document("level_" + levelIndex);

        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "correctOption", correctOptions },
            { "chosenOption", chosenOptions },
            { "timestamp", FieldValue.ServerTimestamp }
        };

        levelDoc.SetAsync(data, SetOptions.MergeAll).ContinueWithOnMainThread(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogWarning("SaveLevelOptions 失敗: " + task.Exception);
                callback?.Invoke(false, task.Exception.Message);
            }
            else
            {
                Debug.Log($"✅ SaveLevelOptions 成功: level_{levelIndex}");
                callback?.Invoke(true, null);
            }
        });
    }


    public void UploadFile(byte[] fileBytes, string storagePath, string contentType, Action<bool, string> callback)
    {
        StorageReference storageRef = storage.GetReference(storagePath);

        // 建立 Metadata
        var metadata = new MetadataChange { ContentType = contentType };

        storageRef.PutBytesAsync(fileBytes, metadata).ContinueWithOnMainThread(uploadTask =>
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

    private (string ext, string contentType) DetectFileType(byte[] data, string key = "")
    {
        if (data == null || data.Length < 4)
            return (".bin", "application/octet-stream");

        // 常見格式的檔頭 (Magic Number)
        // PNG: 89 50 4E 47
        if (data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return (".png", "image/png");

        // WAV: 52 49 46 46 ("RIFF") ... WAVE
        if (data[0] == 0x52 && data[1] == 0x49 && data[2] == 0x46 && data[3] == 0x46)
        {
            // 有 "WAVE" 字樣的就是 WAV
            string header = System.Text.Encoding.ASCII.GetString(data, 0, Math.Min(data.Length, 12));
            if (header.Contains("WAVE"))
                return (".wav", "audio/wav");
        }

        // JSON: 通常以 { 或 [ 開頭
        if (data[0] == '{' || data[0] == '[')
            return (".json", "application/json");

        // CSV: 通常是純文字（英數字 + 逗號）
        string textHead = System.Text.Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 100));
        if (textHead.Contains(",") && textHead.IndexOfAny(new[] { '\n', '\r' }) > 0)
            return (".csv", "text/csv");

        // 若 key 有附帶提示也可輔助判斷
        key = key.ToLower();
        if (key.Contains("png")) return (".png", "image/png");
        if (key.Contains("wav")) return (".wav", "audio/wav");
        if (key.Contains("csv")) return (".csv", "text/csv");
        if (key.Contains("json")) return (".json", "application/json");

        // 預設值
        return (".bin", "application/octet-stream");
    }

    public void UploadFilesAndSaveUrls(string testId, string levelIndex, Dictionary<string, byte[]> files, Action<bool, string> callback = null)
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

            // 自動偵測檔案類型
            var (ext, ctype) = DetectFileType(data, key);

            string fname = $"{key}_{Guid.NewGuid().ToString().Substring(0, 8)}";
            string path = basePath + fname;

            UploadFile(data, path, ctype, (ok, result) =>
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
                                                               .Collection("tests")
                                                               .Document(testId)
                                                               .Collection("levelResults")
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
                                        .Collection("tests");

        col.OrderByDescending("startTimestamp").Limit(limit).GetSnapshotAsync().ContinueWithOnMainThread(task =>
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
