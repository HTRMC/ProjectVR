using UnityEngine;
using TMPro;

public class SocketPuzzleManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] ServerSocket[] sockets;
    [SerializeField] GameObject winTextObject;

    bool hasWon;

    void Start()
    {
        if (winTextObject != null)
            winTextObject.SetActive(false);

        if (sockets == null) return;
        for (int i = 0; i < sockets.Length; i++)
        {
            sockets[i].plugConnected.AddListener(OnConnectionChanged);
            sockets[i].plugDisconnected.AddListener(OnConnectionChanged);
        }
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
        if (hasWon) return;

        if (CheckAllCorrect())
        {
            hasWon = true;
            ShowWin();
        }
    }

    bool CheckAllCorrect()
    {
        if (sockets == null || sockets.Length == 0) return false;

        for (int i = 0; i < sockets.Length; i++)
        {
            var socket = sockets[i];
            if (!socket.IsConnected) return false;

            var plug = socket.ConnectedPlug;
            if (plug == null) return false;

            if (plug.ColorID != socket.ColorID) return false;
        }

        return true;
    }

    void ShowWin()
    {
        Debug.Log("[SocketPuzzleManager] YOU WIN!");
        if (winTextObject != null)
            winTextObject.SetActive(true);
    }
}
