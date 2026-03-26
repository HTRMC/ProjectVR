using UnityEngine;

public class ServerBlinkingLights : MonoBehaviour
{
    [Header("Blink Settings")]
    public float minBlinkInterval = 0.05f;
    public float maxBlinkInterval = 0.5f;
    public Color[] lightColors = new Color[]
    {
        new Color(0f, 1f, 0f, 1f),       // green
        new Color(0f, 0.8f, 1f, 1f),     // cyan
        new Color(1f, 0.5f, 0f, 1f),     // orange
        new Color(1f, 0f, 0f, 1f),       // red
        new Color(0.2f, 0.6f, 1f, 1f),   // blue
    };

    private Renderer[] childRenderers;
    private MaterialPropertyBlock[] propBlocks;
    private float[] nextBlinkTimes;
    private bool[] lightStates;
    private int[] colorIndices;

    void Start()
    {
        childRenderers = GetComponentsInChildren<Renderer>();
        propBlocks = new MaterialPropertyBlock[childRenderers.Length];
        nextBlinkTimes = new float[childRenderers.Length];
        lightStates = new bool[childRenderers.Length];
        colorIndices = new int[childRenderers.Length];

        for (int i = 0; i < childRenderers.Length; i++)
        {
            propBlocks[i] = new MaterialPropertyBlock();
            nextBlinkTimes[i] = Time.time + Random.Range(0f, maxBlinkInterval);
            lightStates[i] = Random.value > 0.5f;
            colorIndices[i] = Random.Range(0, lightColors.Length);
        }
    }

    void Update()
    {
        float time = Time.time;
        for (int i = 0; i < childRenderers.Length; i++)
        {
            if (time >= nextBlinkTimes[i])
            {
                lightStates[i] = !lightStates[i];
                nextBlinkTimes[i] = time + Random.Range(minBlinkInterval, maxBlinkInterval);

                if (Random.value < 0.1f)
                    colorIndices[i] = Random.Range(0, lightColors.Length);

                childRenderers[i].GetPropertyBlock(propBlocks[i]);
                Color c = lightStates[i] ? lightColors[colorIndices[i]] : Color.black;
                propBlocks[i].SetColor("_EmissionColor", c * 2f);
                propBlocks[i].SetColor("_BaseColor", lightStates[i] ? lightColors[colorIndices[i]] * 0.8f : new Color(0.05f, 0.05f, 0.05f));
                childRenderers[i].SetPropertyBlock(propBlocks[i]);
            }
        }
    }
}
