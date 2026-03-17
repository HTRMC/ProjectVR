using UnityEngine;

public class Billboard : MonoBehaviour
{
    Transform cam;

    void Start()
    {
        cam = Camera.main != null ? Camera.main.transform : null;
    }

    void LateUpdate()
    {
        if (cam == null)
        {
            if (Camera.main != null)
                cam = Camera.main.transform;
            else
                return;
        }

        Vector3 dir = transform.position - cam.position;
        dir.y = 0;
        if (dir.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}
