using UnityEngine;

public class BossBlackboard
{
    public Transform bossTransform;
    public Rigidbody2D rb;
    public Animator anim;
    public SpriteRenderer sr;
    public Transform playerTarget;
    public BossAI bossAI;
    public GameObject indicator;

    public float moveSpeed = 4f;
    public float attackRange = 2f;
    public float detectionRange = 10f;
    public float attackCooldown = 1.5f;

    public bool isAttacking = false;
    public float cooldownTimer = 0f;
}
