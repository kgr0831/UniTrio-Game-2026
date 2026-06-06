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
    [Tooltip("이 거리 이내면 내려찍기(근거리 AOE)를 선택지에 포함")]
    public float slamRange = 7.5f;
    [Tooltip("이 거리 이내면 근거리 밖이라도 돌진·투척 등 원거리 스킬을 사용")]
    public float rangedAttackRange = 13.0f;
    public float detectionRange = 10.0f;
    public float attackCooldown = 1.5f;
    public string playerTag = "Player";
    public GameObject indicator;
    public Action OnAttackPoint;

    // ── 골렘 구역(아레나) 경계 — 돌진이 벽 밖으로 나가지 않도록 제한 ──
    [HideInInspector] public bool hasArenaBounds;
    [HideInInspector] public Vector2 arenaCenter;
    [HideInInspector] public float arenaRadius;
    /// <summary>돌진 등 이동 스킬이 이 원(중심·반지름) 밖으로 나가지 않도록 경계를 설정한다.</summary>
    public void SetArenaBounds(Vector2 center, float radius)
    {
        hasArenaBounds = true;
        arenaCenter = center;
        arenaRadius = radius;
    }
    public void ClearArenaBounds() => hasArenaBounds = false;

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

    [Header("Rush VFX Textures (Casual_Hit)")]
    public Texture2D rushStreakTex; // Trail_1: 스피드라인
    public Texture2D rushRingTex;   // Ring_1: 확산 충격파 링

    public virtual void Start()
    {
        blackboard = new BossBlackboard
        {
            bossTransform = transform,
            rb = GetComponent<Rigidbody2D>(),
            anim = GetComponent<Animator>(),
            sr = GetComponentInChildren<SpriteRenderer>(),
            moveSpeed = this.moveSpeed,
            attackRange = this.attackRange,
            slamRange = this.slamRange,
            rangedAttackRange = this.rangedAttackRange,
            detectionRange = this.detectionRange,
            attackCooldown = this.attackCooldown,
            bossAI = this,
            indicator = this.indicator,
        };

        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null) blackboard.playerTarget = player.transform;

        // 스킬 노드 (인스턴스 공유 — 한 번에 하나만 실행되므로 두 풀에 같은 노드를 넣어도 안전)
        var slam  = new SkillExecuteNode(blackboard, new SlamSkill());
        var rush  = new SkillExecuteNode(blackboard, new RushSkill());
        var throwSkill = new SkillExecuteNode(blackboard, new ThrowSkill());

        // 근거리: 전 스킬 / 원거리: 돌진·투척만 (내려찍기는 보스 주변 AOE라 근거리 전용)
        List<Node> nearSkills = new List<Node> { slam, rush, throwSkill };
        List<Node> farSkills  = new List<Node> { rush, throwSkill };

        // 공격 중이면 거리/쿨다운 무시하고 스킬 계속 실행.
        // 그 외엔 원거리 사거리(rangedAttackRange) 이내 + 쿨다운이면 공격 진입.
        Selector attackCondition = new Selector(blackboard, new List<Node>
        {
            new CheckIsAttacking(blackboard),
            new Sequence(blackboard, new List<Node>
            {
                new CheckPlayerDistance(blackboard, blackboard.rangedAttackRange),
                new CooldownGateNode(blackboard),
            })
        });

        Sequence attackSequence = new Sequence(blackboard, new List<Node>
        {
            attackCondition,
            // slamRange 이내면 전 스킬(내려찍기 포함), 그 밖이면 돌진·투척만 무작위 실행
            new DistanceSkillSelector(blackboard, nearSkills, farSkills, blackboard.slamRange)
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

    /// <summary>
    /// 현재 실행 중인 스킬을 강제 종료한다(사망 등). 스킬의 OnEnd가 호출되어
    /// 인디케이터(원/박스)·파티클 등이 정리된다. 중복 호출에 안전.
    /// </summary>
    public void AbortCurrentSkill()
    {
        if (blackboard != null && blackboard.currentSkill != null)
        {
            blackboard.currentSkill.OnEnd();
            blackboard.currentSkill = null;
        }
    }
}
