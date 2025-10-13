using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Firebase;
using Firebase.Database;

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

    public void SaveAge(string age)
    {
        if (user != null)
        {
            DatabaseReference reference = FirebaseDatabase.DefaultInstance.RootReference;
            // reference.Child(user.UserId).Child("email").SetValueAsync(user.Email).ContinueWith(task =>
            // {
            //     if (task.IsCompletedSuccessfully)
            //     {
            //         print("saved!");
            //     }
            // });
            reference.Child(user.UserId).Child("age").SetValueAsync(age).ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    print("saved!");
                }
            });
        }
        else
        {
            print("no user.");
        }
        
    }

    public void SaveMale(string gender)
    {
        if (user != null)
        {
            DatabaseReference reference = FirebaseDatabase.DefaultInstance.RootReference;
            // reference.Child(user.UserId).Child("email").SetValueAsync(user.Email).ContinueWith(task =>
            // {
            //     if (task.IsCompletedSuccessfully)
            //     {
            //         print("saved!");
            //     }
            // });
            reference.Child(user.UserId).Child("gender").SetValueAsync(gender).ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    print("saved!");
                }
            });
        }
        else
        {
            print("no user.");
        }
    }

    public void SaveFemale(string gender)
    {
        if (user != null)
        {
            DatabaseReference reference = FirebaseDatabase.DefaultInstance.RootReference;
            // reference.Child(user.UserId).Child("email").SetValueAsync(user.Email).ContinueWith(task =>
            // {
            //     if (task.IsCompletedSuccessfully)
            //     {
            //         print("saved!");
            //     }
            // });
            reference.Child(user.UserId).Child("gender").SetValueAsync(gender).ContinueWith(task =>
            {
                if (task.IsCompletedSuccessfully)
                {
                    print("saved!");
                }
            });
        }
        else
        {
            print("no user.");
        }
    }

    public void LoadAge()
    {
        if (user != null)
        {
            DatabaseReference reference = FirebaseDatabase.DefaultInstance.RootReference;
            reference.Child(user.UserId).Child("age").GetValueAsync().ContinueWith(task =>
            {
                DataSnapshot snapshot = task.Result;
                print(snapshot.Value);
            });
        }
        else
        {
            print("No user.");
        }
    }

    public void LoadGender()
    {
        if (user != null)
        {
            DatabaseReference reference = FirebaseDatabase.DefaultInstance.RootReference;
            reference.Child(user.UserId).Child("gender").GetValueAsync().ContinueWith(task =>
            {
                DataSnapshot snapshot = task.Result;
                print(snapshot.Value);
            });
        }
        else
        {
            print("No user.");
        }
    }

    public DatabaseReference GetUserReference()
    {
        DatabaseReference reference = FirebaseDatabase.DefaultInstance.RootReference;
        return reference.Child(user.UserId);
    }

    void OnDestroy()
    {
        auth.StateChanged -= AuthStateChanged;
    }
}
