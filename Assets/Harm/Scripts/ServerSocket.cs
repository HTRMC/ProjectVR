using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

[ExecuteInEditMode]
[RequireComponent(typeof(XRSocketInteractor))]
public class ServerSocket : MonoBehaviour
{
    [Header("Visual")]
    [SerializeField] float cubeSize = 0.04f;
    [SerializeField] Color socketColor = Color.red;

    [Header("Puzzle")]
    [SerializeField] string colorID;

    [Header("Detection")]
    [SerializeField] float detectionRadius = 0.3f;

    [Header("Events")]
    public UnityEvent<CablePlug> plugConnected;
    public UnityEvent<CablePlug> plugDisconnected;

    XRSocketInteractor socket;
    CablePlug connectedPlug;
    GameObject visual;

    void OnEnable()
    {
        EnsureVisual();
    }

    void OnDisable()
    {
        if (!Application.isPlaying && visual != null)
            visual.SetActive(false);
    }

    void Awake()
    {
        if (!Application.isPlaying) return;

        socket = GetComponent<XRSocketInteractor>();
        socket.hoverSocketSnapping = true;
        socket.recycleDelayTime = 0.5f;

        var box = GetComponent<BoxCollider>();
        if (box != null)
        {
            box.isTrigger = true;
            box.size = Vector3.one * detectionRadius;
        }

        EnsureVisual();
    }

    void EnsureVisual()
    {
        // Find existing visual child
        if (visual == null)
        {
            var existing = transform.Find("SocketVisual");
            if (existing != null)
                visual = existing.gameObject;
        }

        if (visual == null)
            CreateVisual();
        else
            UpdateVisual();

        visual.SetActive(true);
    }

    void CreateVisual()
    {
        visual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        visual.name = "SocketVisual";
        visual.transform.SetParent(transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localScale = Vector3.one * cubeSize;

        // Remove collider — the BoxCollider on this GameObject handles interaction
        if (Application.isPlaying)
            Destroy(visual.GetComponent<Collider>());
        else
            DestroyImmediate(visual.GetComponent<Collider>());

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", socketColor);
        visual.GetComponent<Renderer>().material = mat;
    }

    void UpdateVisual()
    {
        visual.transform.localScale = Vector3.one * cubeSize;
        var renderer = visual.GetComponent<Renderer>();
        if (renderer != null && renderer.sharedMaterial != null)
            renderer.sharedMaterial.SetColor("_BaseColor", socketColor);
    }

#if UNITY_EDITOR
    void Update()
    {
        if (!Application.isPlaying)
            EnsureVisual();
    }
#endif

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
    public string ColorID => colorID;

    public void SetColorID(string id) { colorID = id; }
    public void SetSocketColor(Color color) { socketColor = color; }
}
