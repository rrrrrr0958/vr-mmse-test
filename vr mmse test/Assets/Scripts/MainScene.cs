using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;
using Firebase.Database;
using System;

public class MainScene : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    [SerializeField]
    FirebaseManager FirebaseManager;
    [SerializeField]
    TMP_InputField InputEmail;
    [SerializeField]
    TMP_InputField InputPassword;
    [SerializeField]
    GameObject LoginPanel;
    [SerializeField]
    GameObject InfoPanel;
    [SerializeField]
    Text textEmail;
    [SerializeField]
    GameObject UserGenderPanel;
    [SerializeField]
    GameObject UserAgePanel;
    [SerializeField]
    TMP_InputField InputAge;
    [SerializeField]
    GameObject UserPanelDisplay;
    [SerializeField]
    Text textAge;
    [SerializeField]
    Text textGender;
    void Start()
    {
        FirebaseManager.auth.StateChanged += AuthStateChanged;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.S))
        {
            FirebaseManager.SaveAge(InputAge.text);
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            // FirebaseManager.LoadData();
            StartCoroutine(LoadAgeTask());
        }
    }

    public void Register()
    {
        FirebaseManager.Register(InputEmail.text, InputPassword.text);
    }
    public void Login()
    {
        FirebaseManager.Login(InputEmail.text, InputPassword.text);
    }
    public void Logout()
    {
        FirebaseManager.Logout();
        InputAge.text = "";
        InputEmail.text = "";
        InputPassword.text = "";
    }
    public void CheckInfo()
    {
        StartCoroutine(LoadAgeTask());
        StartCoroutine(LoadGenderTask());
        InfoPanel.SetActive(false);
        UserPanelDisplay.SetActive(true);
    }
    public void ReturnMainPage()
    {
        UserPanelDisplay.SetActive(false);
        InfoPanel.SetActive(true);
    }
    public void SaveAge()
    {
        FirebaseManager.SaveAge(InputAge.text);
        UserAgePanel.SetActive(false);
        InfoPanel.SetActive(true);
    }

    public void SaveMaleGender()
    {
        FirebaseManager.SaveMale("男性");
        UserGenderPanel.SetActive(false);
        UserAgePanel.SetActive(true);
    }

    public void SaveFemaleGender()
    {
        FirebaseManager.SaveFemale("女性");
        UserGenderPanel.SetActive(false);
        UserAgePanel.SetActive(true);
    }

    void AuthStateChanged(object sender, System.EventArgs eventArgs)
    {
        if (FirebaseManager.user == null)
        {
            textEmail.text = "";
            textAge.text = "";
            textGender.text = "";
            LoginPanel.SetActive(true);
            InfoPanel.SetActive(false);
        }
        else
        {
            textEmail.text = FirebaseManager.user.Email;
            LoginPanel.SetActive(false);

            StartCoroutine(CheckIfFirstLogin());
        }
    }

    void OnDestroy()
    {
        FirebaseManager.auth.StateChanged -= AuthStateChanged;
    }

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
            // InputAge.text = "";

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
            // InputAge.text = "";

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

        // 檢查 age 與 gender 是否存在
        bool hasAge = snapshot.Child("age").Value != null;
        bool hasGender = snapshot.Child("gender").Value != null;

        if (!hasAge || !hasGender)
        {
            // 🚀 第一次登入 → 要先填資料
            InfoPanel.SetActive(false);
            UserGenderPanel.SetActive(true);
        }
        else
        {
            // 🚀 已有完整資料 → 顯示 InfoPanel
            string age = snapshot.Child("age").Value.ToString();
            string gender = snapshot.Child("gender").Value.ToString();

            textAge.text = age;
            textGender.text = gender;

            InfoPanel.SetActive(true);
            UserGenderPanel.SetActive(false);
        }
    }
}
