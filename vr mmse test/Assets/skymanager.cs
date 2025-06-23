using UnityEngine;

public class skymanager : MonoBehaviour
{
    public float skyspeed;

    // Update is called once per frame
    void Update()
    {
        RenderSettings.skybox.SetFloat("_Rotation", Time.time * skyspeed);
    }
}
