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
    [SerializeField] GameObject alreadyRegister;
    [SerializeField] GameObject falseEmailOrPassword;

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
            // 已登入
            textEmail.text = FirebaseManager.user.Email;
            LoginPanel.SetActive(false);

            // 登入後載入資料或檢查是否第一次登入
            FirebaseManager.GetUserProfile((ok, dict) =>
            {
                if (ok && dict != null && dict.ContainsKey("age") && dict.ContainsKey("gender"))
                {
                    // 有資料
                    string age = dict["age"].ToString();
                    string gender = dict["gender"].ToString();
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
        PlayButtonSound();
        alreadyRegister.SetActive(false);
        falseEmailOrPassword.SetActive(false);
        string email = InputEmail.text;
        string password = InputPassword.text;

        FirebaseManager.Register(email, password, (ok, msg) =>
        {
            if (ok)
            {
                Debug.Log("註冊完成");
                LoginPanel.SetActive(false);
                UserGenderPanel.SetActive(true);
            }
            else
            {
                Debug.LogError("註冊失敗: " + msg);
                alreadyRegister.SetActive(true);
            }
        });
    }

    public void Login()
    {
        PlayButtonSound();
        alreadyRegister.SetActive(false);
        falseEmailOrPassword.SetActive(false);
        
        string email = InputEmail.text;
        string password = InputPassword.text;

        // 使用 FirebaseManager 的 Login 函數
        FirebaseManager.Login(email, password, (ok, msg) =>
        {
            if (ok)
            {
                Debug.Log("登入成功");
                LoginPanel.SetActive(false);
                InfoPanel.SetActive(true);
            }
            else
            {
                Debug.LogError("登入失敗: " + msg);
                falseEmailOrPassword.SetActive(true);
            }
        });
    }

    public void Logout()
    {
        PlayButtonSound();
        FirebaseManager.Logout();
        InfoPanel.SetActive(false);
        LoginPanel.SetActive(true);
        
        // 清空輸入欄位
        InputAge.text = "";
        InputEmail.text = "";
        InputPassword.text = "";
        
        // AuthStateChanged 會自動處理 UI 切換
    }

    public void SaveAge()
    {
        PlayButtonSound();
        string age = InputAge.text;
        
        if (string.IsNullOrEmpty(age))
        {
            Debug.LogWarning("年齡不能為空");
            return;
        }
        
        // gender 應該已經存過
        FirebaseManager.SetUserProfile(age, null, (ok, err) =>
        {
            if (ok)
            {
                Debug.Log("年齡儲存成功");
                // 更新顯示的年齡
                textAge.text = age;
                
                UserAgePanel.SetActive(false);
                InfoPanel.SetActive(true);
                if (AnotherInfoObject != null) AnotherInfoObject.SetActive(true);
            }
            else
            {
                Debug.LogWarning("Set age 失敗: " + err);
            }
        });
    }

    public void SaveMaleGender()
    {
        PlayButtonSound();
        FirebaseManager.SetUserProfile(null, "男性", (ok, err) =>
        {
            if (ok)
            {
                Debug.Log("性別儲存成功");
                textGender.text = "男性";
                
                UserGenderPanel.SetActive(false);
                UserAgePanel.SetActive(true);
            }
            else
            {
                Debug.LogWarning("Set gender 失敗: " + err);
            }
        });
    }

    public void SaveFemaleGender()
    {
        PlayButtonSound();
        FirebaseManager.SetUserProfile(null, "女性", (ok, err) =>
        {
            if (ok)
            {
                Debug.Log("性別儲存成功");
                textGender.text = "女性";
                
                UserGenderPanel.SetActive(false);
                UserAgePanel.SetActive(true);
            }
            else
            {
                Debug.LogWarning("Set gender 失敗: " + err);
            }
        });
    }

    public void CheckInfo()
    {
        PlayButtonSound();
        // 重新取得使用者資料
        FirebaseManager.GetUserProfile((ok, dict) =>
        {
            if (ok && dict != null)
            {
                if (dict.ContainsKey("email"))
                    textEmail.text = dict["email"].ToString();
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