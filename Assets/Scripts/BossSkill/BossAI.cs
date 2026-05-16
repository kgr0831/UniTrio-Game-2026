using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using System;

public class BossAI : MonoBehaviour
{
    private Node rootNode;
    private BossBlackboard blackboard;

    [Header("Settings")]
    public float moveSpeed = 3.0f;
    public float attackRange = 2.5f;
    public float detectionRange = 10.0f;
    public string playerTag = "Player";
    public GameObject indicator;
    public Action OnAttackPoint;
    
    [Header("Indicator Sprites")]
    public Sprite circleSprite;
    public Sprite squareSprite;
    public Sprite halfCircleSprite;
    public Sprite halfAndhalfCircleSprite;
    public GameObject rockPrefeb;

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
            bossAI = this,
            indicator = this.indicator,
        };
        
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null) blackboard.playerTarget = player.transform;
        
        // 스킬 추가
        List<Node> skillPool = new List<Node> 
        {
            new SkillExecuteNode(blackboard, new SlamSkill()),
            new SkillExecuteNode(blackboard, new RushSkill()),
            new SkillExecuteNode(blackboard, new ThrowSkill()),
        };
        
        Sequence attackSequence = new Sequence(blackboard, new List<Node>
        {
            new CheckPlayerDistance(blackboard, blackboard.attackRange),
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
        if (rootNode != null)
        {
            rootNode.Evaluate();
        }
        
        float direction = blackboard.playerTarget.position.x - transform.position.x;
        
    }
    
    public void TriggerAttack()
    {
        OnAttackPoint?.Invoke();
    }
    
}