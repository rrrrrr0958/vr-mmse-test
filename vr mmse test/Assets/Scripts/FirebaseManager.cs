using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FirebaseManager : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public Firebase.Auth.FirebaseAuth auth;
    public Firebase.Auth.FirebaseUser user;
    void Start()
    {
        auth = Firebase.Auth.FirebaseAuth.DefaultInstance;
        auth.StateChanged += AuthStateChanged;
    }

    // Update is called once per frame
    void Update()
    {

    }

    public void Register(string email, string password)
    {
        auth.CreateUserWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
        {
            if (task.IsCanceled)
            {
                return;
            }
            if (task.IsFaulted)
            {
                print(task.Exception.InnerException.Message);
                return;
            }
            if (task.IsCompletedSuccessfully)
            {
                print("Registered!");
            }
        });
    }

    public void Login(string email, string password)
    {
        auth.SignInWithEmailAndPasswordAsync(email, password).ContinueWith(task =>
        {
            if (task.IsFaulted)
            {
                print(task.Exception.InnerException.Message);
                return;
            }
            if (task.IsCompletedSuccessfully)
            {
                print("Login!");
            }
        });
    }

    public void Logout()
    {
        auth.SignOut();
    }

    void AuthStateChanged(object sender, System.EventArgs eventArgs)
    {
        if (auth.CurrentUser != user)
        {
            user = auth.CurrentUser;
            if (user != null)
            {
                print($"Login - {user.Email}");
            }
        }
    }
    void OnDestroy()
    {
        auth.StateChanged -= AuthStateChanged;
    }
}
