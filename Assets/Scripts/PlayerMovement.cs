using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    /// <summary>
    /// 외부(활 차징 등)에서 임시로 이동속도를 조절합니다. 정상 = 1.0f
    /// </summary>
    [HideInInspector] public float SpeedMultiplier = 1f;

    private Rigidbody2D rb;
    private Vector2     movement;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");
        movement = movement.normalized;
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + movement * moveSpeed * SpeedMultiplier * Time.fixedDeltaTime);
    }
}