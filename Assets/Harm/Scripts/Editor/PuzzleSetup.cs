using UnityEngine;
using UnityEditor;
using TMPro;

public static class PuzzleSetup
{
    struct ColorDef
    {
        public string id;
        public Color color;
        public ColorDef(string id, Color color) { this.id = id; this.color = color; }
    }

    [MenuItem("Tools/Setup Socket Puzzle")]
    public static void Setup()
    {
        var colors = new ColorDef[]
        {
            new ColorDef("Red",    Color.red),
            new ColorDef("Blue",   Color.blue),
            new ColorDef("Green",  Color.green),
            new ColorDef("Yellow", Color.yellow),
            new ColorDef("Purple", new Color(0.6f, 0.1f, 0.9f)),
            new ColorDef("Orange", new Color(1f, 0.5f, 0f)),
        };

        // ── Side A: existing "Servers" ──────────────────────────────────
        var serversA = GameObject.Find("Servers");
        if (serversA == null) { Debug.LogError("No 'Servers' GameObject found"); return; }

        var socketsA = new ServerSocket[6];
        for (int i = 0; i < 6; i++)
        {
            string serverName = i == 0 ? "Server" : $"Server ({i})";
            var server = serversA.transform.Find(serverName);
            if (server == null) { Debug.LogError($"Missing {serverName}"); return; }

            var socketTf = server.Find("Socket");
            if (socketTf == null) { Debug.LogError($"Missing Socket on {serverName}"); return; }

            var socket = socketTf.GetComponent<ServerSocket>();
            if (socket == null) { Debug.LogError($"Missing ServerSocket component on {serverName}/Socket"); return; }

            socket.SetColorID(colors[i].id);
            socket.SetSocketColor(colors[i].color);
            EditorUtility.SetDirty(socket);

            socketsA[i] = socket;
        }

        // ── Side B: duplicate servers on opposite side ──────────────────
        var existingB = GameObject.Find("Servers_B");
        if (existingB != null)
            Undo.DestroyObjectImmediate(existingB);

        var serversB = Object.Instantiate(serversA);
        serversB.name = "Servers_B";

        // Position on opposite side of the room, rotated 180 to face inward
        Vector3 posA = serversA.transform.position;
        serversB.transform.position = new Vector3(posA.x, posA.y, posA.z + 5f);
        serversB.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

        Undo.RegisterCreatedObjectUndo(serversB, "Create Servers B");

        // Same colors as side A — just a copy, rotated 180
        var socketsB = new ServerSocket[6];
        for (int i = 0; i < 6; i++)
        {
            string serverName = i == 0 ? "Server" : $"Server ({i})";
            var server = serversB.transform.Find(serverName);
            var socketTf = server.Find("Socket");
            var socket = socketTf.GetComponent<ServerSocket>();
            EditorUtility.SetDirty(socket);
            socketsB[i] = socket;
        }

        // ── Destroy existing cables ─────────────────────────────────────
        Material cableMat = null;

        // Find any existing cable to get material reference
        var existingCables = Object.FindObjectsByType<PhysicsCable>(FindObjectsSortMode.None);
        foreach (var ec in existingCables)
        {
            if (cableMat == null)
            {
                var so = new SerializedObject(ec);
                var matProp = so.FindProperty("cableMaterial");
                cableMat = matProp.objectReferenceValue as Material;
            }
            Undo.DestroyObjectImmediate(ec.gameObject);
        }

        // ── Create 6 cables (longer, for spanning between server racks) ─
        int[] shuffledCables = { 3, 0, 5, 2, 4, 1 };
        float cableLen = 7f;
        int cableSegments = 40;

        // Position cables between the two racks
        Vector3 cableBasePos = new Vector3(posA.x, posA.y + 0.6f, posA.z + 2.5f);

        var cables = new PhysicsCable[6];
        for (int i = 0; i < 6; i++)
        {
            int colorIdx = shuffledCables[i];
            var go = new GameObject($"Cable_{colors[colorIdx].id}");
            go.transform.position = cableBasePos + new Vector3(i * 1f, 0f, 0f);

            var pc = go.AddComponent<PhysicsCable>();
            pc.SetColorID(colors[colorIdx].id);
            pc.SetPlugColor(colors[colorIdx].color);

            var so = new SerializedObject(pc);
            so.FindProperty("cableLength").floatValue = cableLen;
            so.FindProperty("segments").intValue = cableSegments;
            so.FindProperty("solverIterations").intValue = 50;

            if (cableMat != null)
                so.FindProperty("cableMaterial").objectReferenceValue = cableMat;

            so.ApplyModifiedProperties();
            EditorUtility.SetDirty(go);
            Undo.RegisterCreatedObjectUndo(go, "Create Cable");
            cables[i] = pc;
        }

        // ── Win UI Canvas ───────────────────────────────────────────────
        var existingWinCanvas = GameObject.Find("WinCanvas");
        if (existingWinCanvas != null)
            Undo.DestroyObjectImmediate(existingWinCanvas);

        var canvasGo = new GameObject("WinCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();

        var rt = canvasGo.GetComponent<RectTransform>();
        rt.position = new Vector3(posA.x + 2.5f, posA.y + 2.5f, posA.z + 2.5f);
        rt.sizeDelta = new Vector2(200f, 60f);
        rt.localScale = Vector3.one * 0.01f;

        var textGo = new GameObject("WinText");
        textGo.transform.SetParent(canvasGo.transform, false);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "U WIN";
        tmp.fontSize = 80;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.color = Color.green;
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.sizeDelta = Vector2.zero;

        canvasGo.SetActive(false);
        Undo.RegisterCreatedObjectUndo(canvasGo, "Create Win Canvas");

        // ── Puzzle Manager ──────────────────────────────────────────────
        var existingManager = Object.FindFirstObjectByType<SocketPuzzleManager>();
        if (existingManager != null)
            Undo.DestroyObjectImmediate(existingManager.gameObject);

        var managerGo = new GameObject("SocketPuzzleManager");
        var manager = managerGo.AddComponent<SocketPuzzleManager>();

        var managerSo = new SerializedObject(manager);
        var sockAProp = managerSo.FindProperty("sockets");
        sockAProp.arraySize = 6;
        for (int i = 0; i < 6; i++)
            sockAProp.GetArrayElementAtIndex(i).objectReferenceValue = socketsA[i];

        var sockBProp = managerSo.FindProperty("socketsB");
        sockBProp.arraySize = 6;
        for (int i = 0; i < 6; i++)
            sockBProp.GetArrayElementAtIndex(i).objectReferenceValue = socketsB[i];

        managerSo.FindProperty("winTextObject").objectReferenceValue = canvasGo;
        managerSo.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(managerGo, "Create Puzzle Manager");

        // ── Wire up Laptop Screen ───────────────────────────────────────
        var laptop = Object.FindFirstObjectByType<LaptopScreen>();
        if (laptop != null)
        {
            var laptopSo = new SerializedObject(laptop);
            var lSockA = laptopSo.FindProperty("sockets");
            lSockA.arraySize = 6;
            for (int i = 0; i < 6; i++)
                lSockA.GetArrayElementAtIndex(i).objectReferenceValue = socketsA[i];

            var lSockB = laptopSo.FindProperty("socketsB");
            lSockB.arraySize = 6;
            for (int i = 0; i < 6; i++)
                lSockB.GetArrayElementAtIndex(i).objectReferenceValue = socketsB[i];

            laptopSo.ApplyModifiedProperties();
            Debug.Log("[PuzzleSetup] LaptopScreen wired up with both sides.");
        }

        // ── Flashing Alarm Lights ──────────────────────────────────────
        var existingLights = GameObject.Find("AlarmLights");
        if (existingLights != null)
            Undo.DestroyObjectImmediate(existingLights);

        var lightsParent = new GameObject("AlarmLights");
        Undo.RegisterCreatedObjectUndo(lightsParent, "Create Alarm Lights");

        var flashShader = Shader.Find("Custom/FlashingLight");
        var flashMat = new Material(flashShader != null ? flashShader : Shader.Find("Universal Render Pipeline/Unlit"));
        flashMat.name = "FlashingRedLight";
        if (flashShader != null)
        {
            flashMat.SetColor("_Color", new Color(1f, 0.1f, 0.05f));
            flashMat.SetColor("_EmissionIntensity", new Color(4f, 0.4f, 0.2f));
            flashMat.SetFloat("_Speed", 2f);
            flashMat.SetFloat("_MinBrightness", 0.05f);
        }
        else
        {
            flashMat.SetColor("_BaseColor", Color.red);
        }

        // Place 4 lights on ceiling: 2 near each server rack
        float ceilingY = posA.y + 3f;
        float midZ = posA.z + 2.5f;
        Vector3[] lightPositions = new Vector3[]
        {
            new Vector3(posA.x + 1f, ceilingY, midZ - 1f),
            new Vector3(posA.x + 4f, ceilingY, midZ - 1f),
            new Vector3(posA.x + 1f, ceilingY, midZ + 1f),
            new Vector3(posA.x + 4f, ceilingY, midZ + 1f),
        };

        for (int i = 0; i < lightPositions.Length; i++)
        {
            var lightGo = new GameObject($"AlarmLight_{i}");
            lightGo.transform.SetParent(lightsParent.transform, false);
            lightGo.transform.position = lightPositions[i];

            // Mesh bulb
            var bulb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bulb.name = "Bulb";
            bulb.transform.SetParent(lightGo.transform, false);
            bulb.transform.localScale = Vector3.one * 0.15f;
            Object.DestroyImmediate(bulb.GetComponent<Collider>());
            bulb.GetComponent<Renderer>().sharedMaterial = flashMat;

            // Point light
            var pointLightGo = new GameObject("PointLight");
            pointLightGo.transform.SetParent(lightGo.transform, false);
            var pl = pointLightGo.AddComponent<Light>();
            pl.type = LightType.Point;
            pl.color = new Color(1f, 0.15f, 0.05f);
            pl.intensity = 3f;
            pl.range = 6f;
            pl.shadows = LightShadows.None;

            // FlashingLight script to sync point light with shader
            var flash = lightGo.AddComponent<FlashingLight>();
            var flashSo = new SerializedObject(flash);
            flashSo.FindProperty("pointLight").objectReferenceValue = pl;
            flashSo.FindProperty("speed").floatValue = 2f;
            flashSo.FindProperty("maxIntensity").floatValue = 3f;
            flashSo.FindProperty("minIntensity").floatValue = 0f;
            flashSo.ApplyModifiedProperties();
        }

        // ── Confetti ────────────────────────────────────────────────────
        var confetti = GameObject.Find("WinConfetti");
        if (confetti != null)
        {
            var mso = new SerializedObject(manager);
            mso.FindProperty("winConfetti").objectReferenceValue = confetti;
            mso.ApplyModifiedProperties();
        }

        Debug.Log("[PuzzleSetup] Done! Side A + Side B servers, 6 cables (7m), puzzle manager, laptop, win UI, and 4 alarm lights created.");
    }

    [MenuItem("Tools/Wire Lights to Nearest Sockets")]
    public static void WireLightsToSockets()
    {
        var lights = Object.FindObjectsByType<FlashingLight>(FindObjectsSortMode.None);
        var sockets = Object.FindObjectsByType<ServerSocket>(FindObjectsSortMode.None);

        if (lights.Length == 0) { Debug.LogError("No FlashingLight components found"); return; }
        if (sockets.Length == 0) { Debug.LogError("No ServerSocket components found"); return; }

        foreach (var light in lights)
        {
            float minDist = float.MaxValue;
            ServerSocket nearest = null;
            Vector3 lightPos = light.transform.position;

            foreach (var socket in sockets)
            {
                float dist = (socket.transform.position - lightPos).sqrMagnitude;
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = socket;
                }
            }

            if (nearest != null)
            {
                var so = new SerializedObject(light);
                so.FindProperty("socket").objectReferenceValue = nearest;
                so.ApplyModifiedProperties();
                EditorUtility.SetDirty(light);
                Debug.Log($"[WireLights] {light.name} -> {nearest.name} ({nearest.ColorID})");
            }
        }

        Debug.Log($"[WireLights] Wired {lights.Length} lights to nearest sockets.");
    }
}
