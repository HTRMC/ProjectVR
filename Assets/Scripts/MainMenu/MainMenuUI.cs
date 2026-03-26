using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class MainMenuUI : MonoBehaviour
{
    [Header("References")]
    public Canvas menuCanvas;

    private void Start()
    {
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }

    public void OnLevelsClicked()
    {
        Debug.Log("Levels clicked");
    }

    public void OnOptionsClicked()
    {
        Debug.Log("Options clicked");
    }

    public void OnQuitClicked()
    {
        Debug.Log("Quit clicked");
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
