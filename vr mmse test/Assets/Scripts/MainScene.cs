using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using Firebase.Database;
using System;

public class MainScene : MonoBehaviour
{
    [Header("Firebase 相關")]
    [SerializeField] FirebaseManager FirebaseManager;
    [SerializeField] TMP_InputField InputEmail;
    [SerializeField] TMP_InputField InputPassword;

    [Header("UI Panels")]
    [SerializeField] GameObject LoginPanel;
    [SerializeField] GameObject InfoPanel;
    [SerializeField] GameObject UserGenderPanel;
    [SerializeField] GameObject UserAgePanel;
    [SerializeField] GameObject UserPanelDisplay;
    [SerializeField] GameObject AnotherInfoObject; // 【新增】與 InfoPanel 同時出現的物件

    [Header("UI Texts")]
    [SerializeField] Text textEmail;
    [SerializeField] Text textAge;
    [SerializeField] Text textGender;

    [Header("Age Input")]
    [SerializeField] TMP_InputField InputAge;

    // 🔊【新增】音效系統
    [Header("Sound Settings")]
    [SerializeField] AudioSource audioSource; // 拖進 AudioSource 元件
    [SerializeField] AudioClip buttonClickSound; // 拖進點擊音效檔

    void Start()
    {
        FirebaseManager.auth.StateChanged += AuthStateChanged;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            FirebaseManager.SaveAge(InputAge.text);
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            StartCoroutine(LoadAgeTask());
        }
    }

    // ----------------- 按鈕觸發事件 -----------------

    public void Register()
    {
        PlayButtonSound(); // 🔊 播放音效
        FirebaseManager.Register(InputEmail.text, InputPassword.text);
    }

    public void Login()
    {
        PlayButtonSound(); // 🔊 播放音效
        FirebaseManager.Login(InputEmail.text, InputPassword.text);
    }

    public void Logout()
    {
        PlayButtonSound(); // 🔊 播放音效
        FirebaseManager.Logout();
        InputAge.text = "";
        InputEmail.text = "";
        InputPassword.text = "";
        
        // 登出時，隱藏 InfoPanel 和新增的物件
        InfoPanel.SetActive(false);
        if (AnotherInfoObject != null) AnotherInfoObject.SetActive(false); // 【新增】
    }

    public void CheckInfo()
    {
        PlayButtonSound(); // 🔊 播放音效
        StartCoroutine(LoadAgeTask());
        StartCoroutine(LoadGenderTask());
        InfoPanel.SetActive(false);
        if (AnotherInfoObject != null) AnotherInfoObject.SetActive(false); // 【修改】隱藏新增的物件
        UserPanelDisplay.SetActive(true);
    }

    public void ReturnMainPage()
    {
        PlayButtonSound(); // 🔊 播放音效
        UserPanelDisplay.SetActive(false);
        InfoPanel.SetActive(true);
        if (AnotherInfoObject != null) AnotherInfoObject.SetActive(true); // 【修改】顯示新增的物件
    }

    public void SaveAge()
    {
        PlayButtonSound(); // 🔊 播放音效
        FirebaseManager.SaveAge(InputAge.text);
        UserAgePanel.SetActive(false);
        InfoPanel.SetActive(true);
        if (AnotherInfoObject != null) AnotherInfoObject.SetActive(true); // 【修改】顯示新增的物件
    }

    public void SaveMaleGender()
    {
        PlayButtonSound(); // 🔊 播放音效
        FirebaseManager.SaveMale("男性");
        UserGenderPanel.SetActive(false);
        UserAgePanel.SetActive(true);
    }

    public void SaveFemaleGender()
    {
        PlayButtonSound(); // 🔊 播放音效
        FirebaseManager.SaveFemale("女性");
        UserGenderPanel.SetActive(false);
        UserAgePanel.SetActive(true);
    }

    public void OnSceneSwitchButtonClicked()
    {
        PlayButtonSound();
        SceneFlowManager.instance.LoadNextScene();
        // SceneFlowManager.instance.LoadNextScene();
        // SceneManager.LoadScene("NextSceneName");
    }

    // ----------------- 🔊 新增的音效函式 -----------------
    public void PlayButtonSound()
    {
        if (audioSource != null && buttonClickSound != null)
        {
            audioSource.PlayOneShot(buttonClickSound);
        }
        else
        {
            Debug.LogWarning("⚠️ AudioSource 或 ButtonClickSound 未指定！");
        }
    }

    // ----------------- 狀態切換邏輯 -----------------
    void AuthStateChanged(object sender, System.EventArgs eventArgs)
    {
        if (FirebaseManager.user == null)
        {
            // 未登入狀態
            textEmail.text = "";
            textAge.text = "";
            textGender.text = "";
            LoginPanel.SetActive(true);
            InfoPanel.SetActive(false);
            if (AnotherInfoObject != null) AnotherInfoObject.SetActive(false); // 【修改】未登入時隱藏
        }
        else
        {
            // 已登入狀態
            textEmail.text = FirebaseManager.user.Email;
            LoginPanel.SetActive(false);

            StartCoroutine(CheckIfFirstLogin());
        }
    }

    void OnDestroy()
    {
        FirebaseManager.auth.StateChanged -= AuthStateChanged;
    }

    // ----------------- 其餘原有程式不變 -----------------
    IEnumerator LoadAgeTask()
    {
        var task = FirebaseManager.GetUserReference().Child("age").GetValueAsync();
        yield return new WaitUntil(() => task.IsCompleted);
        DataSnapshot snapshot = task.Result;
        if (snapshot.Value != null)
        {
            string age = snapshot.Value.ToString();
            print(age);
            textAge.text = age;
        }
        else
        {
            print("No age data.");
        }
    }

    IEnumerator LoadGenderTask()
    {
        var task = FirebaseManager.GetUserReference().Child("gender").GetValueAsync();
        yield return new WaitUntil(() => task.IsCompleted);
        DataSnapshot snapshot = task.Result;
        if (snapshot.Value != null)
        {
            string gender = snapshot.Value.ToString();
            print(gender);
            textGender.text = gender;
        }
        else
        {
            print("No gender data.");
        }
    }

    IEnumerator CheckIfFirstLogin()
    {
        var task = FirebaseManager.GetUserReference().GetValueAsync();
        yield return new WaitUntil(() => task.IsCompleted);

        if (task.Exception != null)
        {
            Debug.LogWarning($"Failed to fetch user data: {task.Exception}");
            yield break;
        }

        DataSnapshot snapshot = task.Result;

        bool hasAge = snapshot.Child("age").Value != null;
        bool hasGender = snapshot.Child("gender").Value != null;

        if (!hasAge || !hasGender)
        {
            InfoPanel.SetActive(false);
            if (AnotherInfoObject != null) AnotherInfoObject.SetActive(false); // 【修改】如果第一次登入，隱藏
            UserGenderPanel.SetActive(true);
        }
        else
        {
            string age = snapshot.Child("age").Value.ToString();
            string gender = snapshot.Child("gender").Value.ToString();

            textAge.text = age;
            textGender.text = gender;

            InfoPanel.SetActive(true);
            if (AnotherInfoObject != null) AnotherInfoObject.SetActive(true); // 【修改】如果不是第一次登入，顯示
            UserGenderPanel.SetActive(false);
        }
    }
}