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

        // Find all sockets in order
        var serversParent = GameObject.Find("Servers");
        if (serversParent == null) { Debug.LogError("No 'Servers' GameObject found"); return; }

        var sockets = new ServerSocket[6];
        for (int i = 0; i < 6; i++)
        {
            string serverName = i == 0 ? "Server" : $"Server ({i})";
            var server = serversParent.transform.Find(serverName);
            if (server == null) { Debug.LogError($"Missing {serverName}"); return; }

            var socketTf = server.Find("Socket");
            if (socketTf == null) { Debug.LogError($"Missing Socket on {serverName}"); return; }

            var socket = socketTf.GetComponent<ServerSocket>();
            if (socket == null) { Debug.LogError($"Missing ServerSocket component on {serverName}/Socket"); return; }

            socket.SetColorID(colors[i].id);
            socket.SetSocketColor(colors[i].color);
            EditorUtility.SetDirty(socket);

            sockets[i] = socket;
        }

        // Find the existing PhysicsCable or figure out its position
        var existingCable = GameObject.Find("PhysicsCable");
        Vector3 cableBasePos = existingCable != null ? existingCable.transform.position : new Vector3(-2f, 0.5f, 0f);
        Material cableMat = null;

        if (existingCable != null)
        {
            var pc = existingCable.GetComponent<PhysicsCable>();
            if (pc != null)
            {
                // Get cable material via SerializedObject
                var so = new SerializedObject(pc);
                var matProp = so.FindProperty("cableMaterial");
                cableMat = matProp.objectReferenceValue as Material;
            }
        }

        // Destroy existing cable
        if (existingCable != null)
        {
            Undo.DestroyObjectImmediate(existingCable);
        }

        // Create 6 cables with shuffled colors
        // Cables get colors in shuffled order so plugs don't match sockets by default
        int[] shuffled = { 3, 0, 5, 2, 4, 1 }; // Yellow, Red, Orange, Green, Purple, Blue

        var cables = new PhysicsCable[6];
        for (int i = 0; i < 6; i++)
        {
            int colorIdx = shuffled[i];
            var go = new GameObject($"Cable_{colors[colorIdx].id}");
            go.transform.position = cableBasePos + new Vector3(i * 0.4f, 0f, 0.5f);

            var pc = go.AddComponent<PhysicsCable>();
            pc.SetColorID(colors[colorIdx].id);
            pc.SetPlugColor(colors[colorIdx].color);

            if (cableMat != null)
            {
                var so = new SerializedObject(pc);
                var matProp = so.FindProperty("cableMaterial");
                matProp.objectReferenceValue = cableMat;
                so.ApplyModifiedProperties();
            }

            EditorUtility.SetDirty(go);
            Undo.RegisterCreatedObjectUndo(go, "Create Cable");
            cables[i] = pc;
        }

        // Create Win UI Canvas
        var canvasGo = new GameObject("WinCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasGo.AddComponent<UnityEngine.UI.CanvasScaler>();

        var rt = canvasGo.GetComponent<RectTransform>();
        rt.position = new Vector3(0f, 2.5f, -3f);
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

        // Create Puzzle Manager
        var managerGo = new GameObject("SocketPuzzleManager");
        var manager = managerGo.AddComponent<SocketPuzzleManager>();

        var managerSo = new SerializedObject(manager);
        var socketsProp = managerSo.FindProperty("sockets");
        socketsProp.arraySize = 6;
        for (int i = 0; i < 6; i++)
        {
            socketsProp.GetArrayElementAtIndex(i).objectReferenceValue = sockets[i];
        }
        var winProp = managerSo.FindProperty("winTextObject");
        winProp.objectReferenceValue = canvasGo;
        managerSo.ApplyModifiedProperties();

        Undo.RegisterCreatedObjectUndo(managerGo, "Create Puzzle Manager");

        Debug.Log("[PuzzleSetup] Done! 6 colored sockets, 6 colored cables, puzzle manager + win UI created.");
    }
}
