using UnityEngine;
using TMPro;

public class LaptopScreen : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI instructionsText;
    [SerializeField] ServerSocket[] sockets;
    [SerializeField] ServerSocket[] socketsB;

    string[] colorIDs;
    string[] colorTags;

    void Start()
    {
        if (sockets == null || sockets.Length == 0) return;

        colorIDs = new string[sockets.Length];
        colorTags = new string[sockets.Length];

        for (int i = 0; i < sockets.Length; i++)
        {
            colorIDs[i] = sockets[i].ColorID;
            colorTags[i] = GetColorTag(sockets[i].ColorID);
        }

        SubscribeSockets(sockets);
        SubscribeSockets(socketsB);
        UpdateText();
    }

    void OnDestroy()
    {
        UnsubscribeSockets(sockets);
        UnsubscribeSockets(socketsB);
    }

    void SubscribeSockets(ServerSocket[] arr)
    {
        if (arr == null) return;
        for (int i = 0; i < arr.Length; i++)
        {
            arr[i].plugConnected.AddListener(OnConnectionChanged);
            arr[i].plugDisconnected.AddListener(OnConnectionChanged);
        }
    }

    void UnsubscribeSockets(ServerSocket[] arr)
    {
        if (arr == null) return;
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i] == null) continue;
            arr[i].plugConnected.RemoveListener(OnConnectionChanged);
            arr[i].plugDisconnected.RemoveListener(OnConnectionChanged);
        }
    }

    void OnConnectionChanged(CablePlug plug)
    {
        UpdateText();
    }

    void UpdateText()
    {
        if (instructionsText == null || colorIDs == null) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Connect all servers together:\n");

        for (int i = 0; i < colorIDs.Length; i++)
        {
            bool sideA = IsColorCorrectOnSide(sockets, colorIDs[i]);
            bool sideB = socketsB != null && IsColorCorrectOnSide(socketsB, colorIDs[i]);
            bool done = sideA && sideB;

            string status = done
                ? " <color=green>Done</color>"
                : "";
            sb.AppendLine($"{colorTags[i]}{status}");
        }

        instructionsText.text = sb.ToString();
    }

    bool IsColorCorrectOnSide(ServerSocket[] arr, string colorID)
    {
        if (arr == null) return false;
        for (int i = 0; i < arr.Length; i++)
        {
            if (arr[i].ColorID != colorID) continue;
            if (!arr[i].IsConnected) return false;
            var plug = arr[i].ConnectedPlug;
            return plug != null && plug.ColorID == colorID;
        }
        return false;
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
