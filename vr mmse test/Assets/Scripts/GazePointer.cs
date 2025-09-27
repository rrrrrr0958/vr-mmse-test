using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class GazePointer : MonoBehaviour
{
    public float maxDistance = 10f;
    public LayerMask interactableLayers;
    public Transform reticle; // assign small sphere or quad
    public float defaultDistance = 3f;
    public string[] debugDeviceNames;

    Camera cam;
    IGazeInteractable current = null;

    InputDevice rightController;
    InputDevice leftController;

    void Start()
    {
        cam = Camera.main;
        TryGetControllers();
    }

    void TryGetControllers()
    {
        rightController = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
        leftController = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand);
    }

    void Update()
    {
        if (!rightController.isValid || !leftController.isValid) TryGetControllers();

        Ray ray = new Ray(cam.transform.position, cam.transform.forward);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, maxDistance, interactableLayers))
        {
            // reticle to hit point
            if (reticle != null) { reticle.position = hit.point; reticle.gameObject.SetActive(true); }

            var gz = hit.collider.GetComponent<MonoBehaviour>() as IGazeInteractable;
            if (gz != null)
            {
                if (current == null || current != gz)
                {
                    current?.OnGazeExit();
                    current = gz;
                    current.OnGazeEnter();
                }

                if (IsSelectPressed())
                {
                    current.OnGazeSelect();
                }
            }
            else
            {
                // not interactable component
                ClearCurrent();
            }
        }
        else
        {
            // no hit -> put reticle at default
            if (reticle != null)
            {
                reticle.position = cam.transform.position + cam.transform.forward * defaultDistance;
                reticle.gameObject.SetActive(true);
            }
            ClearCurrent();
        }
    }

    void ClearCurrent()
    {
        if (current != null)
        {
            current.OnGazeExit();
            current = null;
        }
    }

    bool IsSelectPressed()
    {
        bool pressed = false;
        if (rightController.isValid)
        {
            // joystick press (primary2DAxisClick), A button (primaryButton), trigger
            rightController.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out pressed);
            if (pressed) return true;
            rightController.TryGetFeatureValue(CommonUsages.primaryButton, out pressed);
            if (pressed) return true;
            rightController.TryGetFeatureValue(CommonUsages.triggerButton, out pressed);
            if (pressed) return true;
        }
        if (leftController.isValid)
        {
            leftController.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out pressed);
            if (pressed) return true;
            leftController.TryGetFeatureValue(CommonUsages.primaryButton, out pressed);
            if (pressed) return true;
            leftController.TryGetFeatureValue(CommonUsages.triggerButton, out pressed);
            if (pressed) return true;
        }

        // For Editor debugging
        if (Input.GetKeyDown(KeyCode.Space)) return true;

        return false;
    }
}

public interface IGazeInteractable
{
    void OnGazeEnter();
    void OnGazeExit();
    void OnGazeSelect();
}
