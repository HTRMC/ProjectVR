using UnityEngine;
using TMPro;

public class LaptopScreen : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI instructionsText;
    [SerializeField] ServerSocket[] sockets;

    struct Entry
    {
        public string colorID;
        public string colorTag;
        public int serverNum;
    }

    Entry[] entries;

    void Start()
    {
        if (sockets == null || sockets.Length == 0) return;

        entries = new Entry[sockets.Length];
        for (int i = 0; i < sockets.Length; i++)
        {
            entries[i].colorID = sockets[i].ColorID;
            entries[i].serverNum = i + 1;
            entries[i].colorTag = GetColorTag(sockets[i].ColorID);

            sockets[i].plugConnected.AddListener(OnConnectionChanged);
            sockets[i].plugDisconnected.AddListener(OnConnectionChanged);
        }

        UpdateText();
    }

    void OnDestroy()
    {
        if (sockets == null) return;
        for (int i = 0; i < sockets.Length; i++)
        {
            if (sockets[i] == null) continue;
            sockets[i].plugConnected.RemoveListener(OnConnectionChanged);
            sockets[i].plugDisconnected.RemoveListener(OnConnectionChanged);
        }
    }

    void OnConnectionChanged(CablePlug plug)
    {
        UpdateText();
    }

    void UpdateText()
    {
        if (instructionsText == null || entries == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Plug each cable into the");
        sb.AppendLine("matching colored socket:\n");

        for (int i = 0; i < entries.Length; i++)
        {
            bool correct = IsCorrect(sockets[i]);
            string check = correct ? "<color=green>[x]</color>" : "[ ]";
            string line = $"{check} {entries[i].colorTag}  \u2192  Server {entries[i].serverNum}";
            sb.AppendLine(line);
        }

        instructionsText.text = sb.ToString();
    }

    bool IsCorrect(ServerSocket socket)
    {
        if (!socket.IsConnected) return false;
        var plug = socket.ConnectedPlug;
        if (plug == null) return false;
        return plug.ColorID == socket.ColorID;
    }

    static string GetColorTag(string colorID)
    {
        switch (colorID)
        {
            case "Red":    return "<color=red>\u25cf Red</color>";
            case "Blue":   return "<color=#4444FF>\u25cf Blue</color>";
            case "Green":  return "<color=green>\u25cf Green</color>";
            case "Yellow": return "<color=yellow>\u25cf Yellow</color>";
            case "Purple": return "<color=#9933EE>\u25cf Purple</color>";
            case "Orange": return "<color=orange>\u25cf Orange</color>";
            default:       return colorID;
        }
    }
}
