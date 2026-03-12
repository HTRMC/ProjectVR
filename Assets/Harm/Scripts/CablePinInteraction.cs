using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using System.Collections.Generic;

public class CablePinInteraction : MonoBehaviour
{
    PhysicsCable cable;
    XRSimpleInteractable interactable;
    GameObject previewSphere;
    GameObject cableBody;
    List<SphereCollider> bodyColliders = new List<SphereCollider>();
    // Map from node index to collider index for disabling near pins
    Dictionary<int, int> nodeToColliderIdx = new Dictionary<int, int>();
    HashSet<Collider> bodyColliderSet = new HashSet<Collider>();
    List<CablePin> pins = new List<CablePin>();

    IXRHoverInteractor currentInteractor;
    int nearestNodeIndex = -1;
    bool isHovered;

    const int COLLIDER_SPACING = 3;
    const float COLLIDER_RADIUS = 0.04f;
    const float PREVIEW_SIZE = 0.03f;
    const float MAX_INTERACT_DIST_SQ = 1f; // 1m max perpendicular distance to ray

    public void Init(PhysicsCable cable)
    {
        this.cable = cable;

        // Preview sphere (hidden by default)
        previewSphere = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        previewSphere.name = "PinPreview";
        previewSphere.transform.localScale = Vector3.one * PREVIEW_SIZE;
        Destroy(previewSphere.GetComponent<Collider>());

        var prevMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        prevMat.color = new Color(1f, 1f, 1f, 0.7f);
        previewSphere.GetComponent<Renderer>().material = prevMat;
        previewSphere.SetActive(false);

        // Cable body object for interaction
        cableBody = new GameObject("CableBody");
        cableBody.transform.SetParent(transform, false);

        // Create non-trigger sphere colliders along cable FIRST
        int nodeCount = cable.NodeCount;
        int colIdx = 0;
        for (int i = COLLIDER_SPACING; i < nodeCount - COLLIDER_SPACING; i += COLLIDER_SPACING)
        {
            var colGo = new GameObject($"BC_{i}");
            colGo.transform.SetParent(cableBody.transform, false);
            colGo.transform.position = cable.GetNodePosition(i);

            var col = colGo.AddComponent<SphereCollider>();
            col.radius = COLLIDER_RADIUS;
            bodyColliders.Add(col);
            bodyColliderSet.Add(col);
            cable.AddIgnoredCollider(col);
            nodeToColliderIdx[i] = colIdx;
            colIdx++;
        }

        // Rigidbody + interactable AFTER colliders exist
        var rb = cableBody.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        interactable = cableBody.AddComponent<XRSimpleInteractable>();

        interactable.hoverEntered.AddListener(OnHoverEntered);
        interactable.hoverExited.AddListener(OnHoverExited);
        interactable.selectEntered.AddListener(OnSelectEntered);
    }

    void OnHoverEntered(HoverEnterEventArgs args)
    {
        currentInteractor = args.interactorObject;
        isHovered = true;
    }

    bool IsInteractorCloseEnough()
    {
        if (currentInteractor == null) return false;
        var tf = (currentInteractor as Component)?.transform;
        if (tf == null) return false;

        // Check if any cable node is within interaction range
        float maxDist = 2f;
        float maxDistSq = maxDist * maxDist;
        Vector3 pos = tf.position;
        int nodeCount = cable.NodeCount;
        for (int i = 0; i < nodeCount; i += COLLIDER_SPACING)
        {
            if ((cable.GetNodePosition(i) - pos).sqrMagnitude < maxDistSq)
                return true;
        }
        return false;
    }

    void OnHoverExited(HoverExitEventArgs args)
    {
        if (args.interactorObject == currentInteractor)
        {
            isHovered = false;
            currentInteractor = null;
            nearestNodeIndex = -1;
            previewSphere.SetActive(false);
        }
    }

    void OnSelectEntered(SelectEnterEventArgs args)
    {
        if (!IsInteractorCloseEnough()) return;
        if (nearestNodeIndex >= 0 && !cable.IsNodePinned(nearestNodeIndex))
        {
            CreatePin(nearestNodeIndex);
            previewSphere.SetActive(false);
        }
    }

    void CreatePin(int nodeIndex)
    {
        // Pin stays at the cable node position — user grabs and places it on a surface
        Vector3 pinPos = cable.GetNodePosition(nodeIndex);

        var pinGo = new GameObject($"CablePin_{nodeIndex}");
        pinGo.transform.position = pinPos;

        var pin = pinGo.AddComponent<CablePin>();
        pin.Init(this, cable, nodeIndex, cable.PlugColor);
        pins.Add(pin);

        cable.PinNode(nodeIndex, pinPos);

        // Disable the nearest body collider so controller doesn't
        // accidentally grab the CableBody instead of the CablePin
        DisableNearbyBodyColliders(nodeIndex);
    }

    void DisableNearbyBodyColliders(int nodeIndex)
    {
        // Disable colliders within COLLIDER_SPACING of the pin
        foreach (var kvp in nodeToColliderIdx)
        {
            if (Mathf.Abs(kvp.Key - nodeIndex) <= COLLIDER_SPACING)
            {
                bodyColliders[kvp.Value].enabled = false;
            }
        }
    }

    void EnableBodyCollider(int nodeIndex)
    {
        foreach (var kvp in nodeToColliderIdx)
        {
            if (Mathf.Abs(kvp.Key - nodeIndex) <= COLLIDER_SPACING)
            {
                bodyColliders[kvp.Value].enabled = true;
            }
        }
    }

    void Update()
    {
        if (!isHovered || currentInteractor == null || cable.NodeCount == 0) return;
        if (!IsInteractorCloseEnough())
        {
            previewSphere.SetActive(false);
            return;
        }

        var interactorTf = (currentInteractor as Component)?.transform;
        if (interactorTf == null) return;

        nearestNodeIndex = GetNearestNodeToRay(interactorTf.position, interactorTf.forward);

        if (nearestNodeIndex >= 0 && !cable.IsNodePinned(nearestNodeIndex))
        {
            previewSphere.SetActive(true);
            previewSphere.transform.position = cable.GetNodePosition(nearestNodeIndex);
        }
        else
        {
            previewSphere.SetActive(false);
        }
    }

    int GetNearestNodeToRay(Vector3 rayOrigin, Vector3 rayDir)
    {
        int nearest = -1;
        float minDist = float.MaxValue;
        int nodeCount = cable.NodeCount;

        for (int i = 1; i < nodeCount - 1; i++)
        {
            Vector3 nodePos = cable.GetNodePosition(i);
            Vector3 toNode = nodePos - rayOrigin;
            float t = Vector3.Dot(toNode, rayDir);
            if (t < 0) continue;

            Vector3 closestOnRay = rayOrigin + rayDir * t;
            float dist = (nodePos - closestOnRay).sqrMagnitude;
            if (dist < minDist && dist < MAX_INTERACT_DIST_SQ)
            {
                minDist = dist;
                nearest = i;
            }
        }
        return nearest;
    }

    void LateUpdate()
    {
        int nodeCount = cable.NodeCount;
        if (nodeCount == 0) return;

        int colIdx = 0;
        for (int i = COLLIDER_SPACING; i < nodeCount - COLLIDER_SPACING; i += COLLIDER_SPACING)
        {
            if (colIdx < bodyColliders.Count)
            {
                bodyColliders[colIdx].transform.position = cable.GetNodePosition(i);
                colIdx++;
            }
        }
    }

    public void RemovePin(CablePin pin)
    {
        int nodeIdx = pin.NodeIndex;
        cable.UnpinNode(nodeIdx);
        pins.Remove(pin);
        Destroy(pin.gameObject);
        // Re-enable the body collider so this section of cable is interactable again
        EnableBodyCollider(nodeIdx);
    }
}
