using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

public class ServerSlider : MonoBehaviour
{
    [SerializeField] float maxSlideDistance = 0.7f;

    Vector3 closedPos;
    Vector3 slideDir;
    float currentSlide;
    bool isGrabbed;
    IXRSelectInteractor currentInteractor;
    Vector3 lastInteractorPos;

    void Start()
    {
        closedPos = transform.position;
        // Auto-detect slide direction: servers at Z < 0 slide +Z, others slide -Z
        slideDir = transform.position.z < 0f ? Vector3.forward : Vector3.back;

        SetupCollider();
        SetupInteraction();
    }

    void SetupCollider()
    {
        if (GetComponent<Collider>() != null) return;

        var col = gameObject.AddComponent<BoxCollider>();
        var mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            col.center = mf.sharedMesh.bounds.center;
            col.size = mf.sharedMesh.bounds.size;
        }
    }

    void SetupInteraction()
    {
        var rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        var grab = GetComponent<XRGrabInteractable>();
        if (grab == null) grab = gameObject.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Kinematic;
        grab.trackPosition = false;
        grab.trackRotation = false;
        grab.throwOnDetach = false;

        grab.selectEntered.AddListener(OnGrab);
        grab.selectExited.AddListener(OnRelease);
    }

    void OnGrab(SelectEnterEventArgs args)
    {
        isGrabbed = true;
        currentInteractor = args.interactorObject;
        lastInteractorPos = ((MonoBehaviour)currentInteractor).transform.position;
    }

    void OnRelease(SelectExitEventArgs args)
    {
        isGrabbed = false;
        currentInteractor = null;
    }

    void Update()
    {
        if (!isGrabbed || currentInteractor == null) return;

        var pos = ((MonoBehaviour)currentInteractor).transform.position;
        float delta = Vector3.Dot(pos - lastInteractorPos, slideDir);
        currentSlide = Mathf.Clamp(currentSlide + delta, 0f, maxSlideDistance);
        transform.position = closedPos + slideDir * currentSlide;
        lastInteractorPos = pos;
    }

    void OnDestroy()
    {
        var grab = GetComponent<XRGrabInteractable>();
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnGrab);
            grab.selectExited.RemoveListener(OnRelease);
        }
    }
}