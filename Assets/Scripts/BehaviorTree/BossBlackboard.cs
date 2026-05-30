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

    /// <summary>현재 실행 중인 스킬. 사망 등으로 중단 시 OnEnd로 인디케이터를 정리하기 위해 추적.</summary>
    public BaseSkillAction currentSkill = null;
}
