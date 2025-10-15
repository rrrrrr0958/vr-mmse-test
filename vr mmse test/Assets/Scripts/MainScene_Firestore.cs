using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Firebase.Auth;
using Firebase.Firestore;

public class MainScene_Firestore : MonoBehaviour
{
    [Header("Firebase 相關")]
    [SerializeField] FirebaseManager_Firestore FirebaseManager;

    [SerializeField] TMP_InputField InputEmail;
    [SerializeField] TMP_InputField InputPassword;

    [Header("UI Panels")]
    [SerializeField] GameObject LoginPanel;
    [SerializeField] GameObject InfoPanel;
    [SerializeField] GameObject UserGenderPanel;
    [SerializeField] GameObject UserAgePanel;
    [SerializeField] GameObject UserPanelDisplay;
    [SerializeField] GameObject AnotherInfoObject;

    [Header("UI Texts")]
    [SerializeField] Text textEmail;
    [SerializeField] Text textAge;
    [SerializeField] Text textGender;

    [Header("Age Input")]
    [SerializeField] TMP_InputField InputAge;

    [Header("Sound Settings")]
    [SerializeField] AudioSource audioSource;
    [SerializeField] AudioClip buttonClickSound;

    void Start()
    {
        FirebaseManager.auth.StateChanged += AuthStateChanged;
    }

    void OnDestroy()
    {
        FirebaseManager.auth.StateChanged -= AuthStateChanged;
    }

    void AuthStateChanged(object sender, System.EventArgs eventArgs)
    {
        if (FirebaseManager.user == null)
        {
            // 未登入
            textEmail.text = "";
            textAge.text = "";
            textGender.text = "";
            LoginPanel.SetActive(true);
            InfoPanel.SetActive(false);
            if (AnotherInfoObject != null) AnotherInfoObject.SetActive(false);
        }
        else
        {
            textEmail.text = FirebaseManager.user.Email;
            LoginPanel.SetActive(false);

            // 登入後載入資料或檢查是否第一次登入
            FirebaseManager.GetUserProfile((ok, dict) =>
            {
                if (ok && dict != null && dict.ContainsKey("age") && dict.ContainsKey("gender"))
                {
                    // 有資料
                    string email = dict["email"].ToString();
                    string age = dict["age"].ToString();
                    string gender = dict["gender"].ToString();
                    textEmail.text = email;
                    textAge.text = age;
                    textGender.text = gender;

                    InfoPanel.SetActive(true);
                    if (AnotherInfoObject != null) AnotherInfoObject.SetActive(true);
                    UserGenderPanel.SetActive(false);
                }
                else
                {
                    // 第一次登入，還沒填年齡/性別
                    InfoPanel.SetActive(false);
                    if (AnotherInfoObject != null) AnotherInfoObject.SetActive(false);
                    UserGenderPanel.SetActive(true);
                }
            });
        }
    }

    // ----------- UI 按鈕綁定的方法 -----------

    public void Register()
    {
        string email = InputEmail.text;
        string password = InputPassword.text;

        FirebaseManager_Firestore.Instance.Register(email, password, (ok, msg) =>
        {
            if (ok)
            {
                Debug.Log("註冊完成，前往性別選擇畫面");
                if (UserGenderPanel != null)
                    UserGenderPanel.SetActive(true); // 顯示性別選擇
            }
            else
            {
                Debug.LogError("註冊失敗: " + msg);
            }
        });
        LoginPanel.SetActive(false);
        // InfoPanel.SetActive(true);
    }


    public void Login()
    {
        PlayButtonSound();
        FirebaseManager.auth.SignInWithEmailAndPasswordAsync(InputEmail.text, InputPassword.text).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                Debug.LogWarning("登入失敗: " + task.Exception);
            }
            else
            {
                Debug.Log("登入成功");
            }
        });
        LoginPanel.SetActive(false);
        InfoPanel.SetActive(true);

    }

    public void Logout()
    {
        PlayButtonSound();
        FirebaseManager.auth.SignOut();
        InputAge.text = "";
        InputEmail.text = "";
        InputPassword.text = "";

        InfoPanel.SetActive(false);
        if (AnotherInfoObject != null) AnotherInfoObject.SetActive(false);
        LoginPanel.SetActive(true);
    }

    public void SaveAge()
    {
        PlayButtonSound();
        string age = InputAge.text;
        // gender 應該已經存過
        FirebaseManager.SetUserProfile(age, null, (ok, err) =>
        {
            if (!ok)
                Debug.LogWarning("Set age 失敗: " + err);
        });

        UserAgePanel.SetActive(false);
        InfoPanel.SetActive(true);
        if (AnotherInfoObject != null) AnotherInfoObject.SetActive(true);
    }

    public void SaveMaleGender()
    {
        PlayButtonSound();
        FirebaseManager.SetUserProfile(null, "Male", (ok, err) =>
        {
            if (!ok)
                Debug.LogWarning("Set gender 失敗: " + err);
        });

        UserGenderPanel.SetActive(false);
        UserAgePanel.SetActive(true);
    }

    public void SaveFemaleGender()
    {
        PlayButtonSound();
        FirebaseManager.SetUserProfile(null, "Female", (ok, err) =>
        {
            if (!ok)
                Debug.LogWarning("Set gender 失敗: " + err);
        });

        UserGenderPanel.SetActive(false);
        UserAgePanel.SetActive(true);
    }

    public void CheckInfo()
    {
        PlayButtonSound();
        // 重新取得使用者資料
        FirebaseManager.GetUserProfile((ok, dict) =>
        {
            if (ok && dict != null)
            {
                if (dict.ContainsKey("age"))
                    textAge.text = dict["age"].ToString();
                if (dict.ContainsKey("gender"))
                    textGender.text = dict["gender"].ToString();
            }
        });

        InfoPanel.SetActive(false);
        if (AnotherInfoObject != null) AnotherInfoObject.SetActive(false);
        UserPanelDisplay.SetActive(true);
    }

    public void ReturnMainPage()
    {
        PlayButtonSound();
        UserPanelDisplay.SetActive(false);
        InfoPanel.SetActive(true);
        if (AnotherInfoObject != null) AnotherInfoObject.SetActive(true);
    }

    public void OnSceneSwitchButtonClicked()
    {
        PlayButtonSound();
        SceneFlowManager.instance.LoadNextScene();
    }

    void PlayButtonSound()
    {
        if (audioSource != null && buttonClickSound != null)
        {
            audioSource.PlayOneShot(buttonClickSound);
        }
        else
        {
            Debug.LogWarning("AudioSource 或 ButtonClickSound 未設定");
        }
    }
}
