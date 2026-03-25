using System.Collections;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 5f;

    private Rigidbody2D rb;
    private Vector2 movement;
    

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    void Update()
    {
        // 1. 입력 받기 (WASD 또는 방향키)
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");

        // 대각선 이동 시 속도가 빨라지지 않도록 정규화
        movement = movement.normalized;
    }

    void FixedUpdate()
    {
        // 2. 물리 엔진을 이용한 실제 이동 처리
        rb.MovePosition(rb.position + movement * moveSpeed * Time.fixedDeltaTime);
    }

    // image_3.png의 코루틴 함수 부분을 아래와 같이 수정
    
}