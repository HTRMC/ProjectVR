using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRGrabInteractable))]
[RequireComponent(typeof(Rigidbody))]
public class CablePlug : MonoBehaviour
{
    Rigidbody rb;
    XRGrabInteractable grab;
    PhysicsCable cable;
    ServerSocket currentSocket;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        cable = GetComponentInParent<PhysicsCable>();
    }

    void OnEnable()
    {
        grab.selectEntered.AddListener(OnSelectEntered);
        grab.selectExited.AddListener(OnSelectExited);
        grab.hoverEntered.AddListener(OnHoverEntered);
        grab.hoverExited.AddListener(OnHoverExited);
    }

    void OnDisable()
    {
        grab.selectEntered.RemoveListener(OnSelectEntered);
        grab.selectExited.RemoveListener(OnSelectExited);
        grab.hoverEntered.RemoveListener(OnHoverEntered);
        grab.hoverExited.RemoveListener(OnHoverExited);
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor) return;
        if (cable != null) cable.SetHighlight(true);
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor) return;
        if (cable != null) cable.SetHighlight(false);
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor socketInteractor)
        {
            rb.isKinematic = true;
            currentSocket = socketInteractor.GetComponent<ServerSocket>();
            if (currentSocket != null)
                currentSocket.OnPlugConnected(this);
            Debug.Log($"[CablePlug] Connected to socket: {socketInteractor.name}");
        }
        else
        {
            Debug.Log($"[CablePlug] Grabbed by: {(args.interactorObject as MonoBehaviour)?.name}");
        }
    }

    void OnSelectExited(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
        {
            rb.isKinematic = false;
            if (currentSocket != null)
            {
                currentSocket.OnPlugDisconnected(this);
                currentSocket = null;
            }
            Debug.Log("[CablePlug] Disconnected from socket");
        }
        else
        {
            Debug.Log("[CablePlug] Released from hand");
        }
    }

    public bool IsSocketed => currentSocket != null;
}
