using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(ServerGrabbable))]
[CanEditMultipleObjects]
public class ServerGrabbableEditor : Editor
{
    SerializedProperty snapRange;
    SerializedProperty wireframeOffset;
    SerializedProperty wireframeColor;
    SerializedProperty alignDuration;
    SerializedProperty slideInDuration;
    SerializedProperty slideStartOffset;

    // Cache feature edges per mesh so we don't recompute every editor frame
    static readonly Dictionary<int, List<(int a, int b)>> edgeCache = new Dictionary<int, List<(int a, int b)>>();

    void OnEnable()
    {
        snapRange        = serializedObject.FindProperty("snapRange");
        wireframeOffset  = serializedObject.FindProperty("wireframeOffset");
        wireframeColor   = serializedObject.FindProperty("wireframeColor");
        alignDuration    = serializedObject.FindProperty("alignDuration");
        slideInDuration  = serializedObject.FindProperty("slideInDuration");
        slideStartOffset = serializedObject.FindProperty("slideStartOffset");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        DrawDefaultInspector();
        serializedObject.ApplyModifiedProperties();
    }

    void OnSceneGUI()
    {
        var server = (ServerGrabbable)target;
        Transform slot = server.GetNearestServerSlot();
        if (slot == null) return;

        // ── Snap range sphere ──
        Handles.color = new Color(1f, 1f, 0f, 0.08f);
        Handles.DrawWireDisc(server.transform.position, Vector3.up, snapRange.floatValue);

        // ── Wireframe preview position (draggable) ──
        Vector3 wireWorld = slot.TransformPoint(wireframeOffset.vector3Value);

        Handles.color = Color.white;
        EditorGUI.BeginChangeCheck();
        Vector3 newWireWorld = Handles.PositionHandle(wireWorld, slot.rotation);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(server, "Move Wireframe Position");
            wireframeOffset.vector3Value = slot.InverseTransformPoint(newWireWorld);
            serializedObject.ApplyModifiedProperties();
        }

        // Draw feature-edge wireframe preview (no quad diagonals)
        var mf = server.GetComponentInChildren<MeshFilter>();
        if (mf != null && mf.sharedMesh != null)
        {
            Handles.color = new Color(1f, 1f, 1f, 0.3f);
            var mesh = mf.sharedMesh;
            var verts = mesh.vertices;
            var tris = mesh.triangles;
            var mtx = Matrix4x4.TRS(wireWorld, slot.rotation, server.transform.lossyScale);

            // Cache the feature edges per mesh to avoid recomputing every frame
            int meshId = mesh.GetInstanceID();
            if (!edgeCache.TryGetValue(meshId, out var featureEdges))
            {
                featureEdges = ComputeFeatureEdges(verts, tris);
                edgeCache[meshId] = featureEdges;
            }

            for (int i = 0; i < featureEdges.Count; i++)
            {
                var e = featureEdges[i];
                Handles.DrawLine(
                    mtx.MultiplyPoint3x4(verts[e.a]),
                    mtx.MultiplyPoint3x4(verts[e.b])
                );
            }
        }

        // Label
        var wireStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.white } };
        Handles.Label(wireWorld + Vector3.up * 0.12f, "Wireframe Preview", wireStyle);

        // ── Slide start position (draggable) ──
        Vector3 slideStartWorld = slot.TransformPoint(slideStartOffset.vector3Value);

        Handles.color = Color.green;
        EditorGUI.BeginChangeCheck();
        Vector3 newSlideStart = Handles.PositionHandle(slideStartWorld, slot.rotation);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(server, "Move Slide Start");
            slideStartOffset.vector3Value = slot.InverseTransformPoint(newSlideStart);
            serializedObject.ApplyModifiedProperties();
        }

        // Slide start marker
        Handles.color = Color.green;
        float markerSize = HandleUtility.GetHandleSize(slideStartWorld) * 0.06f;
        Handles.DrawWireCube(slideStartWorld, Vector3.one * markerSize);
        var greenStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.green } };
        Handles.Label(slideStartWorld + Vector3.up * 0.1f, "Slide Start", greenStyle);

        // ── Slide end (slot position, not draggable) ──
        Handles.color = Color.cyan;
        float endSize = HandleUtility.GetHandleSize(slot.position) * 0.06f;
        Handles.DrawWireCube(slot.position, Vector3.one * endSize);
        var cyanStyle = new GUIStyle(EditorStyles.boldLabel) { normal = { textColor = Color.cyan } };
        Handles.Label(slot.position + Vector3.up * 0.1f, "Slide End (Slot)", cyanStyle);

        // ── Arrow from slide start → end ──
        Handles.color = Color.yellow;
        Handles.DrawDottedLine(slideStartWorld, slot.position, 4f);
        DrawArrowCap(slideStartWorld, slot.position);
    }

    static void DrawArrowCap(Vector3 from, Vector3 to)
    {
        Vector3 dir = (to - from);
        if (dir.sqrMagnitude < 0.0001f) return;
        dir.Normalize();

        float size = HandleUtility.GetHandleSize(to) * 0.08f;
        Vector3 right = Vector3.Cross(dir, Vector3.up);
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(dir, Vector3.right);
        right = right.normalized * size;

        Vector3 tip = to;
        Vector3 back = to - dir * size * 2f;
        Handles.DrawLine(tip, back + right);
        Handles.DrawLine(tip, back - right);
    }

    /// <summary>
    /// Returns only feature edges: boundary edges and edges between non-coplanar faces.
    /// Filters out the diagonal edges from quad triangulation.
    /// </summary>
    static List<(int a, int b)> ComputeFeatureEdges(Vector3[] verts, int[] tris)
    {
        int triCount = tris.Length / 3;

        // Face normals
        var normals = new Vector3[triCount];
        for (int i = 0; i < triCount; i++)
        {
            int i0 = tris[i * 3], i1 = tris[i * 3 + 1], i2 = tris[i * 3 + 2];
            normals[i] = Vector3.Cross(verts[i1] - verts[i0], verts[i2] - verts[i0]).normalized;
        }

        // Edge → faces map
        var edgeFaces = new Dictionary<long, List<int>>();
        for (int face = 0; face < triCount; face++)
        {
            int b = face * 3;
            AddFace(edgeFaces, tris[b], tris[b + 1], face);
            AddFace(edgeFaces, tris[b + 1], tris[b + 2], face);
            AddFace(edgeFaces, tris[b + 2], tris[b], face);
        }

        // Filter: keep boundary and hard edges
        const float threshold = 0.99f;
        var result = new List<(int, int)>();

        foreach (var kvp in edgeFaces)
        {
            int a = (int)(kvp.Key >> 32);
            int b2 = (int)(kvp.Key & 0xFFFFFFFF);
            var faces = kvp.Value;

            if (faces.Count == 1)
            {
                result.Add((a, b2));
            }
            else
            {
                bool hard = false;
                for (int i = 0; i < faces.Count && !hard; i++)
                    for (int j = i + 1; j < faces.Count && !hard; j++)
                        if (Vector3.Dot(normals[faces[i]], normals[faces[j]]) < threshold)
                            hard = true;
                if (hard)
                    result.Add((a, b2));
            }
        }

        return result;
    }

    static void AddFace(Dictionary<long, List<int>> map, int a, int b, int face)
    {
        int lo = Mathf.Min(a, b);
        int hi = Mathf.Max(a, b);
        long key = ((long)lo << 32) | (uint)hi;
        if (!map.TryGetValue(key, out var list))
        {
            list = new List<int>(2);
            map[key] = list;
        }
        list.Add(face);
    }
}
