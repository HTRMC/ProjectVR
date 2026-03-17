using UnityEngine;
using TMPro;

public class TvScreenSync : MonoBehaviour
{
    [SerializeField] TextMeshProUGUI laptopInstructions;
    [SerializeField] TextMeshProUGUI tvInstructions;

    string lastText;

    void Update()
    {
        if (laptopInstructions == null || tvInstructions == null) return;
        if (laptopInstructions.text == lastText) return;

        lastText = laptopInstructions.text;
        tvInstructions.text = lastText;
    }
}
