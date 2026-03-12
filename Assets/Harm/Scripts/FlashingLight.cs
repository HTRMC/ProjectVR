using UnityEngine;

public class FlashingLight : MonoBehaviour
{
    [SerializeField] Light pointLight;
    [SerializeField] float speed = 2f;
    [SerializeField] float maxIntensity = 3f;
    [SerializeField] float minIntensity = 0f;
    [SerializeField] ServerSocket socket;

    float timeOffset;
    bool connected;
    Renderer bulbRenderer;
    Material bulbMat;

    Color redColor = new Color(1f, 0.1f, 0.05f);
    Color redEmission = new Color(4f, 0.4f, 0.2f);
    Color greenColor = new Color(0.05f, 1f, 0.1f);
    Color greenEmission = new Color(0.2f, 4f, 0.4f);
    Color greenLight = new Color(0.1f, 1f, 0.15f);
    Color redLight = new Color(1f, 0.15f, 0.05f);

    void Start()
    {
        Vector3 pos = transform.position;
        timeOffset = (pos.x * 7.3f + pos.y * 13.1f + pos.z * 5.7f) % 10f;

        bulbRenderer = GetComponentInChildren<Renderer>();
        if (bulbRenderer != null)
        {
            bulbMat = bulbRenderer.material;
            bulbMat.SetFloat("_TimeOffset", timeOffset);
        }

        if (socket != null)
        {
            socket.plugConnected.AddListener(OnPlugConnected);
            socket.plugDisconnected.AddListener(OnPlugDisconnected);
        }
    }

    void OnDestroy()
    {
        if (socket != null)
        {
            socket.plugConnected.RemoveListener(OnPlugConnected);
            socket.plugDisconnected.RemoveListener(OnPlugDisconnected);
        }
    }

    void OnPlugConnected(CablePlug plug)
    {
        if (plug != null && plug.ColorID == socket.ColorID)
        {
            connected = true;
            SetGreen();
        }
    }

    void OnPlugDisconnected(CablePlug plug)
    {
        connected = false;
        SetRed();
    }

    void SetGreen()
    {
        if (pointLight != null)
            pointLight.color = greenLight;

        if (bulbMat != null)
        {
            bulbMat.SetColor("_Color", greenColor);
            bulbMat.SetColor("_EmissionIntensity", greenEmission);
            bulbMat.SetFloat("_Speed", 0f);
            bulbMat.SetFloat("_MinBrightness", 1f);
        }
    }

    void SetRed()
    {
        if (pointLight != null)
            pointLight.color = redLight;

        if (bulbMat != null)
        {
            bulbMat.SetColor("_Color", redColor);
            bulbMat.SetColor("_EmissionIntensity", redEmission);
            bulbMat.SetFloat("_Speed", speed);
            bulbMat.SetFloat("_MinBrightness", 0.05f);
        }
    }

    void Update()
    {
        if (pointLight == null) return;

        if (connected)
        {
            // Steady green
            pointLight.intensity = maxIntensity;
        }
        else
        {
            // Flashing red
            float wave = Mathf.Sin((Time.time + timeOffset) * speed * Mathf.PI * 2f);
            float t = Mathf.SmoothStep(0f, 1f, (wave + 1f) * 0.5f);
            pointLight.intensity = Mathf.Lerp(minIntensity, maxIntensity, t);
        }
    }
}
