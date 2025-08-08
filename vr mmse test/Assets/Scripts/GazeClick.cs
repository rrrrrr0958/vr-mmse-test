using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class GazeClick : MonoBehaviour
{
    public Button targetButton;
    public float clickDelay = 2f;

    private float timer = 0f;
    private bool isGazing = false;

    public void OnPointerEnter()
    {
        isGazing = true;
        timer = 0f;
    }

    public void OnPointerExit()
    {
        isGazing = false;
        timer = 0f;
    }

    void Update()
    {
        if (isGazing)
        {
            timer += Time.deltaTime;
            if (timer >= clickDelay)
            {
                targetButton.onClick.Invoke();
                isGazing = false;
                timer = 0f;
            }
        }
    }
}
