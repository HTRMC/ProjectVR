using UnityEngine;
using TMPro;

public class SocketPuzzleManager : MonoBehaviour
{
    [Header("References")]
    [SerializeField] ServerSocket[] sockets;
    [SerializeField] ServerSocket[] socketsB;
    [SerializeField] GameObject winTextObject;
    [SerializeField] GameObject winConfetti;

    bool hasWon;

    void Start()
    {
        if (winTextObject != null)
            winTextObject.SetActive(false);

        SubscribeSockets(sockets);
        SubscribeSockets(socketsB);
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
        if (hasWon) return;

        if (CheckAllCorrect())
        {
            hasWon = true;
            ShowWin();
        }
    }

    bool CheckAllCorrect()
    {
        return CheckSide(sockets) && CheckSide(socketsB);
    }

    bool CheckSide(ServerSocket[] arr)
    {
        if (arr == null || arr.Length == 0) return false;
        for (int i = 0; i < arr.Length; i++)
        {
            if (!arr[i].IsConnected) return false;
            var plug = arr[i].ConnectedPlug;
            if (plug == null || plug.ColorID != arr[i].ColorID) return false;
        }
        return true;
    }

    void ShowWin()
    {
        Debug.Log("[SocketPuzzleManager] YOU WIN!");
        if (winTextObject != null)
            winTextObject.SetActive(true);

        if (winConfetti != null)
        {
            winConfetti.SetActive(true);
            var ps = winConfetti.GetComponent<ParticleSystem>();
            if (ps != null)
                ps.Play();
        }
    }
}
