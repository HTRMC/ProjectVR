using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

/// <summary>
/// Drop-in replacement for ServerSlider. Add this to every server in the rack.
/// Auto-pairs with the nearest ServerSlot sibling for snap positioning.
/// Features:
/// - True wireframe indicator when held near an empty slot
/// - Two-phase snap: align to slot mouth, then slide in
/// - Editor gizmos for visualizing snap range, wireframe pos, slide path
/// </summary>
public class ServerGrabbable : MonoBehaviour
{
    [Header("Snap Detection")]
    [SerializeField] float snapRange = 0.8f;

    [Header("Wireframe Preview")]
    [SerializeField] Vector3 wireframeOffset = Vector3.zero;
    [SerializeField] Color wireframeColor = Color.white;

    [Header("Slide Animation")]
    [SerializeField] float alignDuration = 0.3f;
    [SerializeField] float slideInDuration = 0.25f;
    [SerializeField] Vector3 slideStartOffset = new Vector3(0.75f, 0f, 0f);

    // ── Slot registry (shared across all servers) ─────────────────
    class Slot
    {
        public Transform slotTransform; // the ServerSlot object
        public Transform rackParent;    // the Rack_ parent
        public ServerGrabbable occupant;
    }

    static readonly List<Slot> allSlots = new List<Slot>();
    int currentSlotIndex = -1;

    // ── Instance state ────────────────────────────────────────────
    Rigidbody rb;
    XRGrabInteractable grab;
    bool isHeldByPlayer;
    bool isSocketed = true;
    bool isSnapping;

    // Wireframe indicator
    GameObject indicator;
    Material wireframeMat;

    // Two-phase snap animation
    enum SnapPhase { None, Aligning, Sliding }
    SnapPhase snapPhase;
    Vector3 animStart, animEnd;
    Quaternion animStartRot, animEndRot;
    float animTimer, phaseDuration;
    Vector3 finalPos;
    Quaternion finalRot;
    int targetSlotIndex = -1;

    // ── Setup ─────────────────────────────────────────────────────

    void Start()
    {
        // Find nearest ServerSlot sibling for snap position
        Transform nearestSlot = FindNearestServerSlot();
        if (nearestSlot != null)
        {
            currentSlotIndex = allSlots.Count;
            allSlots.Add(new Slot
            {
                slotTransform = nearestSlot,
                rackParent    = transform.parent,
                occupant      = this
            });
        }

        // Ensure components
        rb = GetComponent<Rigidbody>();
        if (rb == null) rb = gameObject.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;

        grab = GetComponent<XRGrabInteractable>();
        if (grab == null) grab = gameObject.AddComponent<XRGrabInteractable>();
        grab.movementType = XRBaseInteractable.MovementType.Instantaneous;
        grab.throwOnDetach = false;

        grab.selectEntered.AddListener(OnSelectEnter);
        grab.selectExited.AddListener(OnSelectExit);

        var slider = GetComponent<ServerSlider>();
        if (slider != null) slider.enabled = false;

        CreateIndicator();
    }

    Transform FindNearestServerSlot()
    {
        if (transform.parent == null) return null;
        Transform nearest = null;
        float nearestDist = float.MaxValue;

        foreach (Transform sibling in transform.parent)
        {
            if (!sibling.name.StartsWith("ServerSlot")) continue;
            float dist = Vector3.Distance(transform.position, sibling.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = sibling;
            }
        }

        return nearest;
    }

    // ── Wireframe indicator ───────────────────────────────────────

    void CreateIndicator()
    {
        indicator = new GameObject("SlotIndicator");

        // Build wireframe line mesh from server's mesh edges
        var meshFilters = GetComponentsInChildren<MeshFilter>();
        if (meshFilters.Length == 0) return;

        wireframeMat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        wireframeMat.SetColor("_BaseColor", wireframeColor);

        foreach (var sourceMf in meshFilters)
        {
            if (sourceMf.sharedMesh == null) continue;
            if (!sourceMf.sharedMesh.isReadable) continue; // skip non-readable meshes

            Vector3 localPos = transform.InverseTransformPoint(sourceMf.transform.position);
            Quaternion localRot = Quaternion.Inverse(transform.rotation) * sourceMf.transform.rotation;
            Vector3 localScale = new Vector3(
                sourceMf.transform.lossyScale.x / Mathf.Max(transform.lossyScale.x, 0.0001f),
                sourceMf.transform.lossyScale.y / Mathf.Max(transform.lossyScale.y, 0.0001f),
                sourceMf.transform.lossyScale.z / Mathf.Max(transform.lossyScale.z, 0.0001f)
            );

            var wireMesh = BuildEdgeMesh(sourceMf.sharedMesh);

            var go = new GameObject("Wire");
            go.transform.SetParent(indicator.transform, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = localRot;
            go.transform.localScale = localScale;
            go.AddComponent<MeshFilter>().sharedMesh = wireMesh;
            var r = go.AddComponent<MeshRenderer>();
            r.material = wireframeMat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
        }

        indicator.SetActive(false);
    }

    /// <summary>
    /// Builds a wireframe mesh showing only feature edges (quad outlines).
    /// Filters out internal diagonal edges shared by two coplanar triangles.
    /// </summary>
    static Mesh BuildEdgeMesh(Mesh source)
    {
        var srcVerts = source.vertices;
        var srcTris  = source.triangles;
        int triCount = srcTris.Length / 3;

        // Compute face normals
        var faceNormals = new Vector3[triCount];
        for (int i = 0; i < triCount; i++)
        {
            int i0 = srcTris[i * 3], i1 = srcTris[i * 3 + 1], i2 = srcTris[i * 3 + 2];
            faceNormals[i] = Vector3.Cross(
                srcVerts[i1] - srcVerts[i0],
                srcVerts[i2] - srcVerts[i0]
            ).normalized;
        }

        // Map each edge to the faces that share it
        var edgeFaces = new Dictionary<long, List<int>>();

        for (int face = 0; face < triCount; face++)
        {
            int b = face * 3;
            AddEdgeFace(edgeFaces, srcTris[b], srcTris[b + 1], face);
            AddEdgeFace(edgeFaces, srcTris[b + 1], srcTris[b + 2], face);
            AddEdgeFace(edgeFaces, srcTris[b + 2], srcTris[b], face);
        }

        // Keep edges that are feature edges:
        //   - boundary (only 1 face)
        //   - shared by 2+ faces with different normals (hard edge)
        const float coplanarThreshold = 0.99f;
        var keepEdges = new List<(int a, int b)>();

        foreach (var kvp in edgeFaces)
        {
            long key = kvp.Key;
            var faces = kvp.Value;

            int a = (int)(key >> 32);
            int b2 = (int)(key & 0xFFFFFFFF);

            if (faces.Count == 1)
            {
                // Boundary edge — always keep
                keepEdges.Add((a, b2));
            }
            else
            {
                // Check if any pair of faces are NOT coplanar
                bool isFeature = false;
                for (int i = 0; i < faces.Count && !isFeature; i++)
                    for (int j = i + 1; j < faces.Count && !isFeature; j++)
                        if (Vector3.Dot(faceNormals[faces[i]], faceNormals[faces[j]]) < coplanarThreshold)
                            isFeature = true;

                if (isFeature)
                    keepEdges.Add((a, b2));
            }
        }

        // Build line mesh
        var verts   = new Vector3[keepEdges.Count * 2];
        var indices = new int[keepEdges.Count * 2];

        for (int i = 0; i < keepEdges.Count; i++)
        {
            verts[i * 2]     = srcVerts[keepEdges[i].a];
            verts[i * 2 + 1] = srcVerts[keepEdges[i].b];
            indices[i * 2]     = i * 2;
            indices[i * 2 + 1] = i * 2 + 1;
        }

        var mesh = new Mesh { name = "Wireframe" };
        mesh.vertices = verts;
        mesh.SetIndices(indices, MeshTopology.Lines, 0);
        mesh.RecalculateBounds();
        return mesh;
    }

    static long EdgeKey(int a, int b)
    {
        int lo = Mathf.Min(a, b);
        int hi = Mathf.Max(a, b);
        return ((long)lo << 32) | (uint)hi;
    }

    static void AddEdgeFace(Dictionary<long, List<int>> map, int a, int b, int face)
    {
        long key = EdgeKey(a, b);
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<int>(2);
            map[key] = list;
        }
        list.Add(face);
    }

    // ── XR events ─────────────────────────────────────────────────

    void OnSelectEnter(SelectEnterEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor) return;

        isHeldByPlayer = true;
        isSocketed = false;
        isSnapping = false;
        snapPhase = SnapPhase.None;

        if (currentSlotIndex >= 0)
        {
            allSlots[currentSlotIndex].occupant = null;
            currentSlotIndex = -1;
        }

        transform.SetParent(null, true);

        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void OnSelectExit(SelectExitEventArgs args)
    {
        if (args.interactorObject is XRSocketInteractor) return;

        isHeldByPlayer = false;
        HideIndicator();

        int slotIdx = FindNearestEmptySlotIndex();
        if (slotIdx >= 0)
            BeginSnap(slotIdx);
        else
            EnablePhysics();
    }

    // ── Two-phase snap animation ──────────────────────────────────

    void BeginSnap(int slotIdx)
    {
        isSnapping = true;
        targetSlotIndex = slotIdx;
        var slot = allSlots[slotIdx];

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        if (grab != null) grab.enabled = false;

        finalPos = slot.slotTransform.position;
        finalRot = slot.slotTransform.rotation;

        // Approach position: slot position + slideStartOffset in slot's local space
        Vector3 approachPos = slot.slotTransform.TransformPoint(slideStartOffset);

        animStart    = transform.position;
        animStartRot = transform.rotation;
        animEnd      = approachPos;
        animEndRot   = finalRot;
        animTimer    = 0f;
        phaseDuration = alignDuration;
        snapPhase    = SnapPhase.Aligning;
    }

    void UpdateSnapAnimation()
    {
        animTimer += Time.deltaTime;
        float t = Mathf.Clamp01(animTimer / phaseDuration);

        if (snapPhase == SnapPhase.Aligning)
        {
            float e = 1f - (1f - t) * (1f - t);
            transform.position = Vector3.Lerp(animStart, animEnd, e);
            transform.rotation = Quaternion.Slerp(animStartRot, animEndRot, e);

            if (t >= 1f)
            {
                transform.position = animEnd;
                transform.rotation = animEndRot;

                animStart     = animEnd;
                animEnd       = finalPos;
                animTimer     = 0f;
                phaseDuration = slideInDuration;
                snapPhase     = SnapPhase.Sliding;
            }
        }
        else if (snapPhase == SnapPhase.Sliding)
        {
            float e = t * t * (3f - 2f * t);
            transform.position = Vector3.Lerp(animStart, animEnd, e);

            if (t >= 1f)
            {
                transform.position = finalPos;
                transform.rotation = finalRot;

                var slot = allSlots[targetSlotIndex];
                transform.SetParent(slot.rackParent, true);

                slot.occupant = this;
                currentSlotIndex = targetSlotIndex;
                targetSlotIndex = -1;

                snapPhase = SnapPhase.None;
                isSnapping = false;
                isSocketed = true;

                if (grab != null) grab.enabled = true;
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────

    void EnablePhysics()
    {
        if (rb != null)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
        }
    }

    void Update()
    {
        if (isSnapping && snapPhase != SnapPhase.None)
        {
            UpdateSnapAnimation();
            return;
        }

        if (!isHeldByPlayer)
        {
            HideIndicator();
            return;
        }

        int slotIdx = FindNearestEmptySlotIndex();
        if (slotIdx >= 0)
        {
            var slot = allSlots[slotIdx];
            indicator.transform.localScale = transform.lossyScale;
            indicator.transform.position = slot.slotTransform.TransformPoint(wireframeOffset);
            indicator.transform.rotation = slot.slotTransform.rotation;
            indicator.SetActive(true);
        }
        else
        {
            HideIndicator();
        }
    }

    int FindNearestEmptySlotIndex()
    {
        int nearest = -1;
        float nearestDist = snapRange;

        for (int i = 0; i < allSlots.Count; i++)
        {
            if (allSlots[i].occupant != null) continue;

            // Measure distance from server to the slide start position, not the slot itself
            Vector3 slideStartPos = allSlots[i].slotTransform.TransformPoint(slideStartOffset);
            float dist = Vector3.Distance(transform.position, slideStartPos);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = i;
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

        if (currentSlotIndex >= 0 && currentSlotIndex < allSlots.Count)
            allSlots[currentSlotIndex].occupant = null;

        if (indicator != null)
            Destroy(indicator);
    }

    // ── Editor helper ─────────────────────────────────────────────

    /// <summary>Returns the nearest ServerSlot sibling (edit-time only).</summary>
    public Transform GetNearestServerSlot()
    {
        if (transform.parent == null) return null;
        Transform nearest = null;
        float nearestDist = float.MaxValue;

        foreach (Transform sibling in transform.parent)
        {
            if (!sibling.name.StartsWith("ServerSlot")) continue;
            float dist = Vector3.Distance(transform.position, sibling.position);
            if (dist < nearestDist)
            {
                nearestDist = dist;
                nearest = sibling;
            }
        }

        return nearest;
    }
}
