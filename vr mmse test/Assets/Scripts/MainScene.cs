using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

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
    void Start()
    {
        FirebaseManager.auth.StateChanged += AuthStateChanged;
    }

    // Update is called once per frame
    void Update()
    {

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
    }
    void AuthStateChanged(object sender, System.EventArgs eventArgs)
    {
        if (FirebaseManager.user == null)
        {
            textEmail.text = "";
            LoginPanel.SetActive(true);
            InfoPanel.SetActive(false);
        }
        else
        {
            textEmail.text = FirebaseManager.user.Email;
            LoginPanel.SetActive(false);
            InfoPanel.SetActive(true);
        }
    }

    void OnDestroy()
    {
        FirebaseManager.auth.StateChanged -= AuthStateChanged;
    }
}
