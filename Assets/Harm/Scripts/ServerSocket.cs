using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[RequireComponent(typeof(XRSocketInteractor))]
public class ServerSocket : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] float cubeSize = 0.04f;
    [SerializeField] Color socketColor = Color.red;

    [Header("Detection")]
    [SerializeField] float detectionRadius = 0.3f;

    [Header("Events")]
    public UnityEvent<CablePlug> plugConnected;
    public UnityEvent<CablePlug> plugDisconnected;

    XRSocketInteractor socket;
    CablePlug connectedPlug;
    GameObject visual;

    void Awake()
    {
        socket = GetComponent<XRSocketInteractor>();
        socket.hoverSocketSnapping = true;
        socket.recycleDelayTime = 0.5f;

        // The trigger collider size controls the actual snap detection range
        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            box.isTrigger = true;
            box.size = Vector3.one * detectionRadius;
        }

        CreateVisual();
    }

    void CreateVisual()
    {
        visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "SocketVisual";
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one * cubeSize;

        // Remove collider — the BoxCollider on this GameObject handles interaction
        Destroy(visual.GetComponent<Collider>());

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = socketColor;
        visual.GetComponent<Renderer>().material = mat;
    }

    public void OnPlugConnected(CablePlug plug)
    {
        connectedPlug = plug;
        plugConnected?.Invoke(plug);
        Debug.Log($"[ServerSocket] {name}: Plug connected");
    }

    public void OnPlugDisconnected(CablePlug plug)
    {
        if (connectedPlug == plug)
        {
            connectedPlug = null;
            plugDisconnected?.Invoke(plug);
            Debug.Log($"[ServerSocket] {name}: Plug disconnected");
        }
    }

    public bool IsConnected => connectedPlug != null;
    public CablePlug ConnectedPlug => connectedPlug;
}
