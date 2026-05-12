using UnityEngine;

public class ArenaCamera : MonoBehaviour
{
    [Header("Target")]
    public Transform player; // Trage Maimuța aici în Inspector

    [Header("Camera Feeling")]
    public float smoothSpeed = 5f; // Cât de moale/lină e mișcarea (mai mic = mai lent)
    public float panMultiplier = 0.5f; // Cât de mult urmărește jucătorul (1 = 1:1, 0.5 = urmărește doar pe jumătate)

    [Header("Camera Limits (X Axis)")]
    public float minX = -3f; // Limita din stânga a camerei
    public float maxX = 3f;  // Limita din dreapta a camerei

    private float fixedY;
    private float fixedZ;

    void Start()
    {
        // Salvăm înălțimea și adâncimea EXACTE pe care le-ai setat tu manual în Unity Editor
        fixedY = transform.position.y;
        fixedZ = transform.position.z;
    }

    // Folosim LateUpdate pentru camere, ca să fim siguri că jucătorul s-a mișcat deja în frame-ul curent
    void LateUpdate()
    {
        if (player == null) return;

        // 1. Calculăm unde AR TREBUI să fie camera (urmărind doar pe X, cu un multiplicator)
        float targetX = player.position.x * panMultiplier;

        // 2. Tăiem mișcarea (Clamp) ca să nu depășească marginile ecranului/arenei
        targetX = Mathf.Clamp(targetX, minX, maxX);

        // 3. Poziția finală dorită (Y și Z rămân blocate)
        Vector3 desiredPosition = new Vector3(targetX, fixedY, fixedZ);

        // 4. Mișcarea lină (Interpolare) de unde suntem acum spre poziția dorită
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
    }
}