using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Makes servers grabbable from rack sockets. Features:
/// - White outline indicator when near an empty slot
/// - Smooth slide-in animation when snapping back
/// - Falls with gravity when released outside a slot
/// </summary>
public class ServerGrabbable : MonoBehaviour
{
    [SerializeField] float snapRange = 0.8f;
    [SerializeField] float slideInDuration = 0.4f;

    Rigidbody rb;
    XRGrabInteractable grab;
    bool isHeldByPlayer;
    bool isSocketed = true;
    bool isSlidingIn;

    // Snap indicator
    GameObject indicator;
    static Material indicatorMat;

    // Slide animation
    Vector3 slideStart;
    Vector3 slideEnd;
    Quaternion slideStartRot;
    Quaternion slideEndRot;
    float slideTimer;
    Transform slideTarget;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (grab != null)
        {
            grab.selectEntered.AddListener(OnSelectEnter);
            grab.selectExited.AddListener(OnSelectExit);
        }

        CreateIndicator();
    }

    void CreateIndicator()
    {
        // Shared material for all indicators
        if (indicatorMat == null)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            indicatorMat = new Material(shader);
            indicatorMat.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.25f));
            indicatorMat.SetFloat("_Surface", 1);
            indicatorMat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            indicatorMat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            indicatorMat.SetInt("_ZWrite", 0);
            indicatorMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            indicatorMat.renderQueue = 3000;
        }

        indicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        indicator.name = "SlotIndicator";
        Destroy(indicator.GetComponent<Collider>());
        indicator.GetComponent<Renderer>().material = indicatorMat;

        // Size to match server mesh
        var mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            indicator.transform.localScale = Vector3.Scale(
                mf.sharedMesh.bounds.size,
                transform.lossyScale
            ) * 1.05f; // slightly bigger than server
        }

        indicator.SetActive(false);
    }

    void OnSelectEnter(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
        {
            // Socket claimed us — animate slide in
            isSocketed = true;
            isHeldByPlayer = false;
            HideIndicator();

            var socketTransform = ((MonoBehaviour)args.interactorObject).transform;
            StartSlideIn(socketTransform);
        }
        else
        {
            // Player grabbed
            isHeldByPlayer = true;
            isSocketed = false;
            isSlidingIn = false;
            transform.SetParent(null, true);
        }
    }

    void OnSelectExit(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
        {
            isSocketed = false;
        }
        else
        {
            isHeldByPlayer = false;
            Invoke(nameof(EnablePhysicsIfFree), 0.15f);
        }
    }

    void EnablePhysicsIfFree()
    {
        if (isSocketed || isHeldByPlayer || isSlidingIn) return;

        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        HideIndicator();
    }

    void StartSlideIn(Transform target)
    {
        slideTarget = target;
        slideStart = transform.position;
        slideStartRot = transform.rotation;
        slideEnd = target.position;
        slideEndRot = target.rotation;
        slideTimer = 0f;
        isSlidingIn = true;

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
    }

    void Update()
    {
        // Slide-in animation
        if (isSlidingIn)
        {
            slideTimer += Time.deltaTime;
            float t = Mathf.Clamp01(slideTimer / slideInDuration);
            // Smooth ease-out curve
            float eased = 1f - (1f - t) * (1f - t);

            transform.position = Vector3.Lerp(slideStart, slideEnd, eased);
            transform.rotation = Quaternion.Slerp(slideStartRot, slideEndRot, eased);

            if (t >= 1f)
            {
                isSlidingIn = false;
                transform.position = slideEnd;
                transform.rotation = slideEndRot;
            }
            return;
        }

        // Show/hide snap indicator while holding
        if (!isHeldByPlayer)
        {
            HideIndicator();
            return;
        }

        var nearest = FindNearestEmptySlot();
        if (nearest != null)
        {
            indicator.SetActive(true);
            indicator.transform.position = nearest.transform.position;
            indicator.transform.rotation = nearest.transform.rotation;
        }
        else
        {
            HideIndicator();
        }
    }

    XRSocketInteractor FindNearestEmptySlot()
    {
        XRSocketInteractor nearest = null;
        float nearestDist = snapRange;

        var sockets = FindObjectsByType<XRSocketInteractor>(FindObjectsSortMode.None);
        foreach (var socket in sockets)
        {
            if (!socket.gameObject.name.StartsWith("ServerSlot")) continue;
            if (socket.hasSelection) continue;

            float dist = Vector3.Distance(transform.position, socket.transform.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = socket;
            }
        }

        return nearest;
    }

    void HideIndicator()
    {
        if (indicator != null)
            indicator.SetActive(false);
    }

    void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnSelectEnter);
            grab.selectExited.RemoveListener(OnSelectExit);
        }

        if (indicator != null)
            Destroy(indicator);
    }
}
