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
    private Animator    anim;
    private Camera      _mainCamera;
    private float       _camToWorldZ;
    private static readonly int HashIsMoving = Animator.StringToHash("IsMoving");
    private static readonly int HashDirX     = Animator.StringToHash("DirX");
    private static readonly int HashDirY     = Animator.StringToHash("DirY");

    void Start()
    {
        rb   = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();
        _mainCamera = Camera.main;
        _camToWorldZ = Mathf.Abs(_mainCamera.transform.position.z - transform.position.z);
    }

    void Update()
    {
        movement.x = Input.GetAxisRaw("Horizontal");
        movement.y = Input.GetAxisRaw("Vertical");
        movement = movement.normalized;

        if (anim != null)
        {
            anim.SetBool(HashIsMoving, movement.sqrMagnitude > 0.001f);

            // 마우스 커서 방향 계산 (바라보는 방향 애니메이션용)
            Vector3 mouseScreenPos = Input.mousePosition;
            mouseScreenPos.z       = _camToWorldZ;
            Vector3 mouseWorld     = _mainCamera.ScreenToWorldPoint(mouseScreenPos);

            Vector2 lookDir = ((Vector2)mouseWorld - (Vector2)transform.position).normalized;
            
            anim.SetFloat(HashDirX, lookDir.x);
            anim.SetFloat(HashDirY, lookDir.y);
        }
    }

    void FixedUpdate()
    {
        rb.MovePosition(rb.position + movement * moveSpeed * SpeedMultiplier * Time.fixedDeltaTime);
    }
}