using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Makes servers grabbable from rack sockets. Handles:
/// - Kinematic while socketed, dynamic with gravity when free
/// - Differentiates player grab vs socket grab
/// - Shows outline indicator when near a socket for snapping
/// </summary>
public class ServerGrabbable : MonoBehaviour
{
    [SerializeField] float snapIndicatorRange = 0.5f;
    [SerializeField] Color snapIndicatorColor = new Color(0.3f, 1f, 0.5f, 0.4f);

    Rigidbody rb;
    XRGrabInteractable grab;
    Transform originalParent;
    Vector3 originalLocalPos;
    Quaternion originalLocalRot;
    bool isHeldByPlayer;
    bool isSocketed = true;
    GameObject snapIndicator;
    Renderer[] renderers;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        grab = GetComponent<XRGrabInteractable>();
        originalParent = transform.parent;
        originalLocalPos = transform.localPosition;
        originalLocalRot = transform.localRotation;

        renderers = GetComponentsInChildren<Renderer>();

        // Start kinematic in rack
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

        CreateSnapIndicator();
    }

    void CreateSnapIndicator()
    {
        // Ghost outline cube that shows where the server will snap to
        snapIndicator = GameObject.CreatePrimitive(PrimitiveType.Cube);
        snapIndicator.name = "SnapIndicator";
        Destroy(snapIndicator.GetComponent<Collider>());

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", snapIndicatorColor);
        // Make transparent
        mat.SetFloat("_Surface", 1); // Transparent
        mat.SetFloat("_Blend", 0);
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.renderQueue = 3000;
        snapIndicator.GetComponent<Renderer>().material = mat;

        // Match server mesh size
        var mf = GetComponent<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            snapIndicator.transform.localScale = Vector3.Scale(
                mf.sharedMesh.bounds.size,
                transform.lossyScale
            );
        }

        snapIndicator.SetActive(false);
    }

    void OnSelectEnter(SelectEnterEventArgs args)
    {
        // Check if this is a socket or a player interactor
        if (args.interactorObject is XRSocketInteractor)
        {
            // Socketed — stay kinematic, reparent
            isSocketed = true;
            isHeldByPlayer = false;

            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
            }

            HideSnapIndicator();
        }
        else
        {
            // Player grabbed
            isHeldByPlayer = true;
            isSocketed = false;

            // Unparent so it moves freely
            transform.SetParent(null, true);
        }
    }

    void OnSelectExit(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor)
        {
            // Unsocketed (player pulling it out)
            isSocketed = false;
        }
        else
        {
            // Player released
            isHeldByPlayer = false;

            // Wait a frame for socket to potentially claim it
            Invoke(nameof(EnablePhysicsIfFree), 0.15f);
        }
    }

    void EnablePhysicsIfFree()
    {
        if (isSocketed || isHeldByPlayer) return;

        // Not socketed, not held — enable gravity
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }

        HideSnapIndicator();
    }

    void Update()
    {
        if (!isHeldByPlayer) return;

        // Look for nearby sockets to show snap indicator
        var nearestSocket = FindNearestEmptySocket();
        if (nearestSocket != null)
        {
            snapIndicator.SetActive(true);
            snapIndicator.transform.position = nearestSocket.transform.position;
            snapIndicator.transform.rotation = nearestSocket.transform.rotation;
        }
        else
        {
            HideSnapIndicator();
        }
    }

    XRSocketInteractor FindNearestEmptySocket()
    {
        XRSocketInteractor nearest = null;
        float nearestDist = snapIndicatorRange;

        // Find all server slot sockets
        var sockets = FindObjectsByType<XRSocketInteractor>(FindObjectsSortMode.None);
        foreach (var socket in sockets)
        {
            // Only consider ServerSlot sockets, not cable sockets
            if (!socket.gameObject.name.StartsWith("ServerSlot")) continue;

            // Skip occupied sockets
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

    void HideSnapIndicator()
    {
        if (snapIndicator != null)
            snapIndicator.SetActive(false);
    }

    void OnDestroy()
    {
        if (grab != null)
        {
            grab.selectEntered.RemoveListener(OnSelectEnter);
            grab.selectExited.RemoveListener(OnSelectExit);
        }

        if (snapIndicator != null)
            Destroy(snapIndicator);
    }
}
