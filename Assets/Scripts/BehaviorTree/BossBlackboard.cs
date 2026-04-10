using UnityEngine;

public class BossBlackboard
{
    public Transform bossTransform;
    public Rigidbody2D rb;
    public Animator anim;
    public Transform playerTarget;
    
    // 설정값
    public float moveSpeed = 4f;
    public float attackRange = 2f;
    public float detectionRange = 10f;

    // 상태값
    public bool isAttacking = false; 
}
