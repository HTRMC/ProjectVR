using UnityEngine;
using TMPro;
using System.Collections.Generic;

public class ServerPuzzleSystem : MonoBehaviour
{
    [Header("TV Display")]
    [SerializeField] TextMeshProUGUI tvInstructions;
    [SerializeField] TextMeshProUGUI tvTitle;

    [System.Serializable]
    public class CableRoute
    {
        public string cableColor;
        public Color displayColor;
        public string sideA;
        public int serverA;
        public int portA;
        public string sideB;
        public int serverB;
        public int portB;

        [System.NonSerialized] public ServerSocket socketARef;
        [System.NonSerialized] public ServerSocket socketBRef;
        [System.NonSerialized] public TextMeshProUGUI labelARef;
        [System.NonSerialized] public TextMeshProUGUI labelBRef;
        [System.NonSerialized] public bool aConnected;
        [System.NonSerialized] public bool bConnected;

        public string LabelA => $"{sideA}-S{serverA}-P{portA}";
        public string LabelB => $"{sideB}-S{serverB}-P{portB}";
    }

    List<CableRoute> routes = new List<CableRoute>();
    Dictionary<ServerSocket, CableRoute> socketLookup = new Dictionary<ServerSocket, CableRoute>();
    bool allConnected;

    void Start()
    {
        DefineRoutes();
        AssignSocketColors();
        FindSocketLabels();
        SubscribeToSockets();
        UpdateTvDisplay();
        UpdateSocketLabels();
    }

    void OnDestroy()
    {
        foreach (var route in routes)
        {
            if (route.socketARef != null)
            {
                route.socketARef.plugConnected.RemoveListener(OnPlugChanged);
                route.socketARef.plugDisconnected.RemoveListener(OnPlugChanged);
            }
            if (route.socketBRef != null)
            {
                route.socketBRef.plugConnected.RemoveListener(OnPlugChanged);
                route.socketBRef.plugDisconnected.RemoveListener(OnPlugChanged);
            }
        }
    }

    void DefineRoutes()
    {
        routes.Add(new CableRoute { cableColor = "Red",    displayColor = Color.red,                          sideA = "A1", serverA = 3, portA = 2, sideB = "B2", serverB = 1, portB = 1 });
        routes.Add(new CableRoute { cableColor = "Blue",   displayColor = new Color(0.26f, 0.26f, 1f),        sideA = "A2", serverA = 5, portA = 1, sideB = "B4", serverB = 7, portB = 3 });
        routes.Add(new CableRoute { cableColor = "Green",  displayColor = Color.green,                        sideA = "A3", serverA = 1, portA = 3, sideB = "B1", serverB = 4, portB = 2 });
        routes.Add(new CableRoute { cableColor = "Yellow", displayColor = Color.yellow,                       sideA = "A1", serverA = 7, portA = 1, sideB = "B3", serverB = 2, portB = 3 });
        routes.Add(new CableRoute { cableColor = "Purple", displayColor = new Color(0.6f, 0.2f, 0.93f),       sideA = "A4", serverA = 2, portA = 2, sideB = "B5", serverB = 8, portB = 1 });
        routes.Add(new CableRoute { cableColor = "Orange", displayColor = new Color(1f, 0.65f, 0f),           sideA = "A5", serverA = 9, portA = 1, sideB = "B2", serverB = 6, portB = 2 });
        routes.Add(new CableRoute { cableColor = "White",  displayColor = Color.white,                        sideA = "A2", serverA = 4, portA = 3, sideB = "B1", serverB = 9, portB = 1 });
        routes.Add(new CableRoute { cableColor = "Cyan",   displayColor = Color.cyan,                         sideA = "A3", serverA = 6, portA = 2, sideB = "B4", serverB = 3, portB = 3 });
        routes.Add(new CableRoute { cableColor = "Pink",   displayColor = new Color(1f, 0.41f, 0.71f),        sideA = "A5", serverA = 1, portA = 1, sideB = "B3", serverB = 5, portB = 2 });
        routes.Add(new CableRoute { cableColor = "Black",  displayColor = new Color(0.3f, 0.3f, 0.3f),        sideA = "A4", serverA = 8, portA = 3, sideB = "B5", serverB = 2, portB = 1 });
    }

    ServerSocket FindSocket(string side, int server, int port)
    {
        string rackSide = side.StartsWith("A") ? "Side_A" : "Side_B";
        string rackName = "Rack_" + side;
        string socketName = $"CableSocket{server}_{port}";

        var rack = GameObject.Find($"Servers/{rackSide}/{rackName}");
        if (rack == null) { Debug.LogError($"Rack not found: Servers/{rackSide}/{rackName}"); return null; }

        var socketTf = rack.transform.Find(socketName);
        if (socketTf == null) { Debug.LogError($"Socket not found: {socketName} in {rackName}"); return null; }

        return socketTf.GetComponent<ServerSocket>();
    }

    void AssignSocketColors()
    {
        foreach (var route in routes)
        {
            route.socketARef = FindSocket(route.sideA, route.serverA, route.portA);
            route.socketBRef = FindSocket(route.sideB, route.serverB, route.portB);

            if (route.socketARef != null)
            {
                route.socketARef.SetColorID(route.cableColor);
                socketLookup[route.socketARef] = route;
            }
            if (route.socketBRef != null)
            {
                route.socketBRef.SetColorID(route.cableColor);
                socketLookup[route.socketBRef] = route;
            }
        }
    }

    void FindSocketLabels()
    {
        foreach (var route in routes)
        {
            route.labelARef = FindLabel(route.LabelA);
            route.labelBRef = FindLabel(route.LabelB);
        }
    }

    TextMeshProUGUI FindLabel(string labelName)
    {
        var labelObj = GameObject.Find("SocketLabel_" + labelName);
        if (labelObj != null)
            return labelObj.GetComponentInChildren<TextMeshProUGUI>();
        return null;
    }

    void SubscribeToSockets()
    {
        foreach (var route in routes)
        {
            if (route.socketARef != null)
            {
                route.socketARef.plugConnected.AddListener(OnPlugChanged);
                route.socketARef.plugDisconnected.AddListener(OnPlugChanged);
            }
            if (route.socketBRef != null)
            {
                route.socketBRef.plugConnected.AddListener(OnPlugChanged);
                route.socketBRef.plugDisconnected.AddListener(OnPlugChanged);
            }
        }
    }

    void OnPlugChanged(CablePlug plug)
    {
        foreach (var route in routes)
        {
            route.aConnected = route.socketARef != null && route.socketARef.IsConnected
                && route.socketARef.ConnectedPlug != null
                && route.socketARef.ConnectedPlug.ColorID == route.cableColor;

            route.bConnected = route.socketBRef != null && route.socketBRef.IsConnected
                && route.socketBRef.ConnectedPlug != null
                && route.socketBRef.ConnectedPlug.ColorID == route.cableColor;
        }

        UpdateTvDisplay();
        UpdateSocketLabels();
        CheckWin();
    }

    void UpdateTvDisplay()
    {
        if (tvInstructions == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<size=80%>Cable Routing Instructions:</size>");
        sb.AppendLine("<size=60%>───────────────────────────</size>\n");

        foreach (var route in routes)
        {
            string hex = ColorUtility.ToHtmlStringRGB(route.displayColor);
            string colorTag = $"<color=#{hex}>";

            string statusA = route.aConnected ? "<color=green> ✓</color>" : "<color=red> ✗</color>";
            string statusB = route.bConnected ? "<color=green> ✓</color>" : "<color=red> ✗</color>";

            sb.AppendLine($"{colorTag}■ {route.cableColor}</color>");
            sb.AppendLine($"  {route.LabelA}{statusA}  →  {route.LabelB}{statusB}");
            sb.AppendLine();
        }

        tvInstructions.text = sb.ToString();

        var laptopInstr = GameObject.Find("Laptop/LaptopCanvas/Instructions");
        if (laptopInstr != null)
        {
            var tmp = laptopInstr.GetComponent<TextMeshProUGUI>();
            if (tmp != null)
                tmp.text = sb.ToString();
        }
    }

    void UpdateSocketLabels()
    {
        foreach (var route in routes)
        {
            if (route.labelARef != null)
                route.labelARef.color = route.aConnected ? Color.green : Color.yellow;
            if (route.labelBRef != null)
                route.labelBRef.color = route.bConnected ? Color.green : Color.yellow;
        }
    }

    void CheckWin()
    {
        if (allConnected) return;

        foreach (var route in routes)
        {
            if (!route.aConnected || !route.bConnected) return;
        }

        allConnected = true;
        Debug.Log("[ServerPuzzleSystem] ALL CONNECTIONS COMPLETE!");

        if (tvTitle != null)
            tvTitle.text = "<color=green>ALL CONNECTED!</color>";
    }
}
