using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// Handles the kinematic/dynamic switching for rack-mounted servers.
/// When socketed in the rack: kinematic (no physics).
/// When grabbed and released outside a socket: dynamic with gravity.
/// </summary>
public class ServerGrabbable : MonoBehaviour
{
    Rigidbody rb;
    XRGrabInteractable grab;
    bool hasBeenGrabbed;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();

        // Start kinematic (in rack)
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (grab != null)
        {
            grab.selectEntered.AddListener(OnGrab);
            grab.selectExited.AddListener(OnRelease);
        }
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        hasBeenGrabbed = true;

        // Unparent from rack so it moves freely
        transform.SetParent(null, true);

        if (rb != null)
        {
            rb.isKinematic = true; // kinematic while held (XRGrabInteractable controls it)
            rb.useGravity = false;
        }
    }

    void OnRelease(SelectExitEventArgs args)
    {
        // Check if we got socketed (another interactor selected us immediately)
        // Small delay to let socket claim us
        Invoke(nameof(CheckIfSocketed), 0.1f);
    }

    void CheckIfSocketed()
    {
        if (grab != null && grab.isSelected)
        {
            // We got socketed — stay kinematic
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }
        else
        {
            // Released into the world — enable physics
            if (rb != null)
            {
                rb.isKinematic = false;
                rb.useGravity = true;
            }
        }
    }

    void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrab);
            grab.selectExited.RemoveListener(OnRelease);
        }
    }
}
