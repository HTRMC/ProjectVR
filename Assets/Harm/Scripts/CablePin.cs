using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

/// <summary>
/// A cable pin that holds a cable node to a surface.
/// Grab to reposition or remove (release away from any surface to remove).
/// </summary>
public class CablePin : MonoBehaviour
{
    CablePinInteraction pinInteraction;
    PhysicsCable cable;
    int nodeIndex;
    Collider pinCollider;

    bool isHeld;

    const float SNAP_RANGE = 3f;

    public int NodeIndex => nodeIndex;

    public void Init(CablePinInteraction interaction, PhysicsCable cable, int nodeIndex, Color cableColor)
    {
        this.pinInteraction = interaction;
        this.cable = cable;
        this.nodeIndex = nodeIndex;

        var rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        pinCollider = gameObject.AddComponent<SphereCollider>();
        ((SphereCollider)pinCollider).radius = 0.03f;

        // Register collider so cable physics ignores it
        cable.AddIgnoredCollider(pinCollider);

        var grab = gameObject.AddComponent<XRGrabInteractable>();
        grab.movementType = XRGrabInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;
        grab.matchAttachPosition = true;
        grab.matchAttachRotation = false;

        grab.selectEntered.AddListener(OnGrabbed);
        grab.selectExited.AddListener(OnReleased);

        // Visual: small colored disc (cable clip)
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "PinVisual";
        visual.transform.SetParent(transform, false);
        visual.transform.localScale = new Vector3(0.03f, 0.005f, 0.03f);
        Destroy(visual.GetComponent<Collider>());

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = cableColor * 0.8f;
        visual.GetComponent<Renderer>().material = mat;
    }

    void OnGrabbed(SelectEnterEventArgs args)
    {
        isHeld = true;
    }

    void Update()
    {
        if (isHeld)
        {
            // Keep cable node following the pin while being dragged
            cable.PinNode(nodeIndex, transform.position);
        }
    }

    void OnReleased(SelectExitEventArgs args)
    {
        isHeld = false;
        // Find nearest surface to snap to
        Vector3 pos = transform.position;
        Vector3[] dirs = { Vector3.up, Vector3.down, Vector3.left, Vector3.right, Vector3.forward, Vector3.back };
        float closestDist = SNAP_RANGE;
        Vector3 snapPos = pos;
        Vector3 snapNormal = Vector3.up;
        bool foundSurface = false;

        foreach (var dir in dirs)
        {
            if (Physics.Raycast(pos, dir, out RaycastHit hit, SNAP_RANGE,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == pinCollider) continue;
                if (hit.distance < closestDist)
                {
                    closestDist = hit.distance;
                    snapPos = hit.point + hit.normal * 0.015f;
                    snapNormal = hit.normal;
                    foundSurface = true;
                }
            }
        }

        if (foundSurface)
        {
            // Re-pin at new surface position
            transform.position = snapPos;
            cable.PinNode(nodeIndex, snapPos);
        }
        else
        {
            // No surface nearby - remove pin entirely
            pinInteraction.RemovePin(this);
        }
    }
}
