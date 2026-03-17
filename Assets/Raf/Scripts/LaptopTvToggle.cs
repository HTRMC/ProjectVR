using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class LaptopTvToggle : MonoBehaviour
{
    [SerializeField] GameObject tvScreen;

    XRBaseInteractable interactable;

    void Awake()
    {
        interactable = GetComponent<XRBaseInteractable>();
        if (interactable != null)
            interactable.selectEntered.AddListener(OnSelect);

        if (tvScreen != null)
            tvScreen.SetActive(false);
    }

    void OnDestroy()
    {
        if (interactable != null)
            interactable.selectEntered.RemoveListener(OnSelect);
    }

    void OnSelect(SelectEnterEventArgs args)
    {
        if (tvScreen != null)
            tvScreen.SetActive(!tvScreen.activeSelf);
    }
}
