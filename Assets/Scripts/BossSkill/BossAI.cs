using System.Collections.Generic;
using UnityEngine;
using System;

public class BossAI : MonoBehaviour
{
    private Node rootNode;
    private BossBlackboard blackboard;

    [Header("Settings")]
    public float moveSpeed = 6.0f;
    public float attackRange = 2.5f;
    public float detectionRange = 10.0f;
    public float attackCooldown = 1.5f;
    public string playerTag = "Player";
    public GameObject indicator;
    public Action OnAttackPoint;

    [Header("Indicator Sprites")]
    public Sprite circleSprite;
    public Sprite squareSprite;
    public Sprite halfCircleSprite;
    public Sprite halfAndhalfCircleSprite;
    public GameObject rockPrefeb;

    [Header("Impact Effects")]
    public GameObject slamImpactPrefab;
    public GameObject rushImpactPrefab;
    public GameObject groundCrackPrefab;

    public virtual void Start()
    {
        blackboard = new BossBlackboard
        {
            bossTransform = transform,
            rb = GetComponent<Rigidbody2D>(),
            anim = GetComponent<Animator>(),
            moveSpeed = this.moveSpeed,
            attackRange = this.attackRange,
            detectionRange = this.detectionRange,
            attackCooldown = this.attackCooldown,
            bossAI = this,
            indicator = this.indicator,
        };

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null) blackboard.playerTarget = player.transform;

        // TODO: RushSkill, ThrowSkill 임시 비활성화 — 내려찍기 테스트용
        List<Node> skillPool = new List<Node>
        {
            new SkillExecuteNode(blackboard, new SlamSkill()),
            // new SkillExecuteNode(blackboard, new RushSkill()),
            // new SkillExecuteNode(blackboard, new ThrowSkill()),
        };

        // 공격 중이면 거리/쿨다운 무시하고 스킬 계속 실행
        Selector attackCondition = new Selector(blackboard, new List<Node>
        {
            new CheckIsAttacking(blackboard),
            new Sequence(blackboard, new List<Node>
            {
                new CheckPlayerDistance(blackboard, blackboard.attackRange),
                new CooldownGateNode(blackboard),
            })
        });

        Sequence attackSequence = new Sequence(blackboard, new List<Node>
        {
            attackCondition,
            new RandomSkillSelector(blackboard, skillPool)
        });

        Sequence followSequence = new Sequence(blackboard, new List<Node>
        {
            new CheckPlayerDistance(blackboard, blackboard.detectionRange),
            new FollowPlayerNode(blackboard)
        });

        rootNode = new Selector(blackboard, new List<Node>
        {
            attackSequence,
            followSequence,
            new ActionNode_Idle(blackboard)
        });
    }

    void Update()
    {
        if (blackboard.cooldownTimer > 0f)
            blackboard.cooldownTimer -= Time.deltaTime;

        if (rootNode != null)
            rootNode.Evaluate();
    }

    public void TriggerAttack()
    {
        OnAttackPoint?.Invoke();
    }
}
