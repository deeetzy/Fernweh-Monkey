using UnityEngine;

public class ParallaxEffect : MonoBehaviour
{
    [Header("Referințe")]
    public Transform cameraTransform; // Trage Main Camera aici

    [Header("Setări Parallax")]
    [Tooltip("0 = fix de cameră, 1 = se mișcă normal, peste 1 = se mișcă mai repede")]
    public float parallaxFactor;

    private Vector3 lastCameraPosition;

    void Start()
    {
        if (cameraTransform == null)
            cameraTransform = Camera.main.transform;

        lastCameraPosition = cameraTransform.position;
    }

    void LateUpdate() // Folosim LateUpdate pentru fluiditate maximă
    {
        // Calculăm cât s-a mișcat camera între cadre
        Vector3 deltaMovement = cameraTransform.position - lastCameraPosition;

        // Aplicăm mișcarea obiectului multiplicată cu factorul nostru
        transform.position += new Vector3(deltaMovement.x * parallaxFactor, deltaMovement.y * parallaxFactor, 0);

        // Actualizăm ultima poziție a camerei
        lastCameraPosition = cameraTransform.position;
    }
}