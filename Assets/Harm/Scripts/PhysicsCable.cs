using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[ExecuteInEditMode]
public class PhysicsCable : MonoBehaviour
{
    [Header("Cable Settings")]
    [SerializeField] float cableLength = 1.5f;
    [SerializeField] int segments = 20;
    [SerializeField] float plugMass = 0.3f;

    [Header("Simulation")]
    [SerializeField] int solverIterations = 30;
    [SerializeField] [Range(0.9f, 1f)] float damping = 0.985f;
    [SerializeField] float tensionForce = 80f;

    [Header("Collision")]
    [SerializeField] float collisionRadius = 0.015f;
    [SerializeField] LayerMask collisionMask = -1;

    [Header("Puzzle")]
    [SerializeField] string colorID;

    [Header("Visuals")]
    [SerializeField] Material cableMaterial;
    [SerializeField] float cableWidth = 0.015f;
    [SerializeField] int smoothSegments = 5;
    [SerializeField] Color plugColor = new Color(0.2f, 0.8f, 1f, 1f);
    [SerializeField] Color outlineColor = Color.white;
    [SerializeField] float outlineWidth = 0.03f;

    Vector3[] nodes;
    Vector3[] prevNodes;
    float segmentLength;
    Rigidbody plugARb, plugBRb;
    Collider plugACol, plugBCol;
    Collider[] overlapBuffer = new Collider[8];
    LineRenderer line;
    LineRenderer outlineLine;
    GameObject plugAOutline, plugBOutline;
    int highlightRefCount;
    bool spawned;

    public string ColorID => colorID;
    public void SetColorID(string id) { colorID = id; }
    public void SetPlugColor(Color color) { plugColor = color; }

    void OnEnable()
    {
        if (!Application.isPlaying)
            SetupPreviewLine();
    }

    void OnDisable()
    {
        if (!Application.isPlaying && line != null)
            line.enabled = false;
    }

    void Start()
    {
        if (!Application.isPlaying) return;

        line = GetComponent<LineRenderer>();
        if (line != null)
        {
            DestroyImmediate(line);
            line = null;
        }

        SpawnCable();
    }

    void SpawnCable()
    {
        int nodeCount = segments + 1;
        segmentLength = cableLength / segments;
        nodes = new Vector3[nodeCount];
        prevNodes = new Vector3[nodeCount];

        for (int i = 0; i < nodeCount; i++)
        {
            nodes[i] = transform.TransformPoint(new Vector3(i * segmentLength, 0, 0));
            prevNodes[i] = nodes[i];
        }

        plugARb = CreatePlug("Plug_A", nodes[0]);
        plugBRb = CreatePlug("Plug_B", nodes[nodeCount - 1]);

        plugACol = plugARb.GetComponent<Collider>();
        plugBCol = plugBRb.GetComponent<Collider>();
        Physics.IgnoreCollision(plugACol, plugBCol);

        line = gameObject.AddComponent<LineRenderer>();
        line.material = cableMaterial != null ? new Material(cableMaterial) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        line.material.color = plugColor;
        line.startColor = plugColor;
        line.endColor = plugColor;
        line.startWidth = cableWidth;
        line.endWidth = cableWidth;
        line.useWorldSpace = true;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;

        // Outline LineRenderer on a child (disabled by default)
        var outlineGo = new GameObject("CableOutline");
        outlineGo.transform.SetParent(transform, false);
        outlineLine = outlineGo.AddComponent<LineRenderer>();
        var cableOutlineShader = Shader.Find("Custom/CableOutline");
        var cableOutlineMat = new Material(cableOutlineShader != null ? cableOutlineShader : Shader.Find("Universal Render Pipeline/Unlit"));
        cableOutlineMat.SetColor("_BaseColor", outlineColor);
        outlineLine.material = cableOutlineMat;
        outlineLine.startWidth = outlineWidth;
        outlineLine.endWidth = outlineWidth;
        outlineLine.useWorldSpace = true;
        outlineLine.numCapVertices = 4;
        outlineLine.numCornerVertices = 4;
        outlineGo.SetActive(false);

        // Plug outline meshes
        plugAOutline = CreatePlugOutline(plugARb.transform);
        plugBOutline = CreatePlugOutline(plugBRb.transform);

        spawned = true;
    }

    GameObject CreatePlugOutline(Transform parent)
    {
        var outline = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        outline.name = "PlugOutline";
        outline.transform.SetParent(parent, false);
        outline.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        outline.transform.localScale = new Vector3(0.018f, 0.03f, 0.018f); // same size as plug
        Destroy(outline.GetComponent<Collider>());

        var meshOutlineShader = Shader.Find("Custom/MeshOutline");
        var mat = new Material(meshOutlineShader != null ? meshOutlineShader : Shader.Find("Universal Render Pipeline/Unlit"));
        mat.SetColor("_BaseColor", outlineColor);
        mat.SetFloat("_OutlineWidth", 0.003f);
        outline.GetComponent<Renderer>().material = mat;
        outline.SetActive(false);

        return outline;
    }

    public void SetHighlight(bool on)
    {
        highlightRefCount += on ? 1 : -1;
        highlightRefCount = Mathf.Max(0, highlightRefCount);
        bool active = highlightRefCount > 0;

        if (outlineLine != null)
            outlineLine.gameObject.SetActive(active);
        if (plugAOutline != null)
            plugAOutline.SetActive(active);
        if (plugBOutline != null)
            plugBOutline.SetActive(active);
    }

    Rigidbody CreatePlug(string plugName, Vector3 position)
    {
        var go = new GameObject(plugName);
        go.transform.SetParent(transform, false);
        go.transform.position = position;

        var rb = go.AddComponent<Rigidbody>();
        rb.mass = plugMass;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        rb.linearDamping = 2f;
        rb.angularDamping = 2f;

        var col = go.AddComponent<CapsuleCollider>();
        col.direction = 0;
        col.radius = 0.02f;
        col.height = 0.06f;

        var grab = go.AddComponent<XRGrabInteractable>();
        grab.movementType = XRGrabInteractable.MovementType.VelocityTracking;
        grab.throwOnDetach = false;
        grab.useDynamicAttach = true;
        grab.matchAttachPosition = true;
        grab.matchAttachRotation = true;

        go.AddComponent<CablePlug>();

        // Visible plug mesh
        var visual = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        visual.name = "PlugVisual";
        visual.transform.SetParent(go.transform, false);
        visual.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        visual.transform.localScale = new Vector3(0.018f, 0.03f, 0.018f);
        Destroy(visual.GetComponent<Collider>());

        var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        mat.color = plugColor;
        visual.GetComponent<Renderer>().material = mat;

        return rb;
    }

    void FixedUpdate()
    {
        if (!spawned) return;

        float dt = Time.fixedDeltaTime;
        int n = nodes.Length;

        // Sync endpoints to plug positions
        nodes[0] = plugARb.position;
        nodes[n - 1] = plugBRb.position;
        prevNodes[0] = nodes[0];
        prevNodes[n - 1] = nodes[n - 1];

        // Verlet integration for middle nodes
        Vector3 g = Physics.gravity * (dt * dt);
        float maxSpeed = segmentLength * 2f;
        for (int i = 1; i < n - 1; i++)
        {
            Vector3 vel = (nodes[i] - prevNodes[i]) * damping;
            if (vel.sqrMagnitude > maxSpeed * maxSpeed)
                vel = vel.normalized * maxSpeed;
            prevNodes[i] = nodes[i];
            nodes[i] += vel + g;
        }

        // Anti-tunneling: SphereCast from prev to current catches ground pass-through
        PreventTunneling();

        // Save endpoint positions before constraint solving
        Vector3 savedA = nodes[0];
        Vector3 savedB = nodes[n - 1];

        // Iterative distance constraints with interleaved overlap collision
        for (int iter = 0; iter < solverIterations; iter++)
        {
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 delta = nodes[i + 1] - nodes[i];
                float dist = delta.magnitude;
                if (dist < 0.0001f) continue;

                float error = (dist - segmentLength) / dist;
                Vector3 correction = delta * (error * 0.5f);
                nodes[i] += correction;
                nodes[i + 1] -= correction;
            }

            if (iter % 5 == 0)
                ResolveOverlaps();
        }

        ResolveOverlaps();

        // Rope tension on plugs: how much the solver wanted to move the endpoints
        Vector3 tensionA = nodes[0] - savedA;
        Vector3 tensionB = nodes[n - 1] - savedB;

        if (!plugARb.isKinematic)
            plugARb.AddForce(tensionA * tensionForce, ForceMode.Force);
        if (!plugBRb.isKinematic)
            plugBRb.AddForce(tensionB * tensionForce, ForceMode.Force);

        // Pin endpoints back to actual plug positions for rendering
        nodes[0] = plugARb.position;
        nodes[n - 1] = plugBRb.position;
    }

    // ── Collision ────────────────────────────────────────────────────────

    void PreventTunneling()
    {
        for (int i = 1; i < nodes.Length - 1; i++)
        {
            Vector3 movement = nodes[i] - prevNodes[i];
            float moveDist = movement.magnitude;
            if (moveDist < 0.001f) continue;

            Vector3 dir = movement / moveDist;
            if (Physics.SphereCast(prevNodes[i], collisionRadius, dir,
                out RaycastHit hit, moveDist, collisionMask,
                QueryTriggerInteraction.Ignore))
            {
                if (hit.collider == plugACol || hit.collider == plugBCol) continue;

                nodes[i] = hit.point + hit.normal * collisionRadius;
                ApplyFriction(i, hit.normal);
            }
        }
    }

    void ResolveOverlaps()
    {
        for (int i = 1; i < nodes.Length - 1; i++)
        {
            int count = Physics.OverlapSphereNonAlloc(
                nodes[i], collisionRadius, overlapBuffer,
                collisionMask, QueryTriggerInteraction.Ignore);

            for (int j = 0; j < count; j++)
            {
                Collider col = overlapBuffer[j];
                if (col == plugACol || col == plugBCol) continue;

                // Non-convex MeshCollider doesn't support ClosestPoint
                if (col is MeshCollider mc && !mc.convex)
                {
                    ResolveNonConvexOverlap(i, col);
                    continue;
                }

                Vector3 closest = col.ClosestPoint(nodes[i]);
                Vector3 diff = nodes[i] - closest;
                float dist = diff.magnitude;

                if (dist < collisionRadius)
                {
                    Vector3 pushDir = dist > 0.001f
                        ? diff / dist
                        : (nodes[i] - col.bounds.center).normalized;

                    nodes[i] = closest + pushDir * collisionRadius;
                    ApplyFriction(i, pushDir);
                }
            }
        }
    }

    void ResolveNonConvexOverlap(int i, Collider col)
    {
        // Node is inside a non-convex mesh — raycast from outside toward the node
        // to find the surface point and push the node out
        Vector3 dir = nodes[i] - col.bounds.center;
        if (dir.sqrMagnitude < 0.0001f) dir = Vector3.up;
        dir.Normalize();

        float extent = col.bounds.extents.magnitude;
        Vector3 origin = nodes[i] + dir * (extent + 0.1f);

        if (col.Raycast(new Ray(origin, -dir), out RaycastHit hit, extent * 2f + 0.2f))
        {
            nodes[i] = hit.point + hit.normal * collisionRadius;
            ApplyFriction(i, hit.normal);
        }
    }

    void ApplyFriction(int i, Vector3 surfaceNormal)
    {
        Vector3 vel = nodes[i] - prevNodes[i];
        float intoSurface = Vector3.Dot(vel, surfaceNormal);
        if (intoSurface < 0)
            vel -= surfaceNormal * intoSurface; // remove inward component
        vel *= 0.3f; // surface friction
        prevNodes[i] = nodes[i] - vel;
    }

    // ── Editor Preview ──────────────────────────────────────────────────

    void SetupPreviewLine()
    {
        line = GetComponent<LineRenderer>();
        if (line == null)
            line = gameObject.AddComponent<LineRenderer>();

        line.enabled = true;

        if (cableMaterial != null)
        {
            var mat = new Material(cableMaterial);
            mat.SetColor("_BaseColor", plugColor);
            line.sharedMaterial = mat;
        }
        line.startColor = plugColor;
        line.endColor = plugColor;
        line.startWidth = cableWidth;
        line.endWidth = cableWidth;
        line.useWorldSpace = true;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;

        UpdatePreviewLine();
    }

    void UpdatePreviewLine()
    {
        if (line == null) return;

        int nodeCount = segments + 1;
        float spacing = cableLength / segments;

        var pts = new Vector3[nodeCount];
        float halfLen = cableLength * 0.5f;
        for (int i = 0; i < nodeCount; i++)
        {
            float x = i * spacing;
            float t = (x - halfLen) / halfLen;
            float sag = -0.3f * (1f - t * t);
            pts[i] = transform.TransformPoint(new Vector3(x, sag, 0f));
        }

        RenderSmoothed(pts);
    }

#if UNITY_EDITOR
    void Update()
    {
        if (!Application.isPlaying && line != null)
            UpdatePreviewLine();
    }
#endif

    // ── LineRenderer ────────────────────────────────────────────────────

    void LateUpdate()
    {
        if (!Application.isPlaying || !spawned) return;

        RenderSmoothed(nodes);
    }

    void RenderSmoothed(Vector3[] pts)
    {
        if (line == null || pts == null || pts.Length < 2) return;

        int count = (pts.Length - 1) * smoothSegments + 1;
        line.positionCount = count;

        bool doOutline = outlineLine != null;
        if (doOutline)
            outlineLine.positionCount = count;

        int idx = 0;
        for (int i = 0; i < pts.Length - 1; i++)
        {
            var p0 = pts[Mathf.Max(i - 1, 0)];
            var p1 = pts[i];
            var p2 = pts[Mathf.Min(i + 1, pts.Length - 1)];
            var p3 = pts[Mathf.Min(i + 2, pts.Length - 1)];

            for (int j = 0; j < smoothSegments; j++)
            {
                float t = j / (float)smoothSegments;
                Vector3 pos = CatmullRom(p0, p1, p2, p3, t);
                line.SetPosition(idx, pos);
                if (doOutline) outlineLine.SetPosition(idx, pos);
                idx++;
            }
        }
        Vector3 last = pts[pts.Length - 1];
        line.SetPosition(idx, last);
        if (doOutline) outlineLine.SetPosition(idx, last);
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * (
            2f * p1 +
            (-p0 + p2) * t +
            (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2 +
            (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
    }
}
