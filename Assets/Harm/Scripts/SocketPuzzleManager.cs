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
    }

    void Update()
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
