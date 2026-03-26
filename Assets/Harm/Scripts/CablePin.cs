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
    Vector3 lastSafePos;

    const float SNAP_RANGE = 3f;
    const float PIN_RADIUS = 0.02f;

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
        lastSafePos = transform.position;
    }

    void Update()
    {
        if (isHeld)
        {
            Vector3 targetPos = transform.position;

            // Prevent tunneling: spherecast from last safe position to current
            Vector3 toTarget = targetPos - lastSafePos;
            float dist = toTarget.magnitude;

            if (dist > 0.001f)
            {
                if (Physics.SphereCast(lastSafePos, PIN_RADIUS, toTarget / dist,
                    out RaycastHit hit, dist, Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore))
                {
                    if (hit.collider != pinCollider)
                    {
                        // Clamp to surface, offset by radius + small margin
                        targetPos = hit.point + hit.normal * (PIN_RADIUS + 0.005f);
                        transform.position = targetPos;
                    }
                }
            }

            // Resolve any remaining overlap
            var overlaps = Physics.OverlapSphere(targetPos, PIN_RADIUS,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
            foreach (var col in overlaps)
            {
                if (col == pinCollider) continue;
                if (col.isTrigger) continue;

                Vector3 closest = col.ClosestPoint(targetPos);
                Vector3 diff = targetPos - closest;
                float d = diff.magnitude;
                if (d < PIN_RADIUS && d > 0.0001f)
                {
                    targetPos = closest + (diff / d) * (PIN_RADIUS + 0.005f);
                    transform.position = targetPos;
                }
            }

            lastSafePos = targetPos;
            cable.PinNode(nodeIndex, targetPos);
        }
    }

    void OnReleased(SelectExitEventArgs args)
    {
        isHeld = false;
        // Find nearest cable tray surface to snap to (only objects tagged "Kabelgoot")
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
                if (!hit.collider.CompareTag("Kabelgoot")) continue;
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
            // Re-pin at cable tray surface position
            transform.position = snapPos;
            cable.PinNode(nodeIndex, snapPos);
        }
        else
        {
            // No cable tray nearby - remove pin entirely
            pinInteraction.RemovePin(this);
        }
    }
}
