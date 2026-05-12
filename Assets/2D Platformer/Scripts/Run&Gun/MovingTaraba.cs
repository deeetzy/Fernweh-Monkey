using UnityEngine;

public class MovingTaraba : MonoBehaviour
{
    public float moveDistance = 2f;
    public float speed = 1.5f;
    public bool isMoving = true; // NOU: Controlăm dacă se mai mișcă

    private Vector3 startPosition;

    void Start()
    {
        startPosition = transform.position;
    }

    void Update()
    {
        if (!isMoving) return; // Dacă e oprită, nu mai execută codul de mișcare

        float offset = Mathf.PingPong(Time.time * speed, moveDistance * 2) - moveDistance;
        transform.position = startPosition + new Vector3(offset, 0, 0);
    }
}