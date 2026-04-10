using System.Collections.Generic;
using UnityEngine;

public class GolemAI : MonoBehaviour
{
    private Node rootNode;
    private BossBlackboard blackboard;

    [Header("Settings")]
    public float moveSpeed = 3.0f;
    public float attackRange = 2.5f;
    public float detectionRange = 10.0f;
    public string playerTag = "Player";

    void Start()
    {
        // 1. 블랙보드(데이터 주머니) 생성 및 초기화
        blackboard = new BossBlackboard
        {
            bossTransform = transform,
            rb = GetComponent<Rigidbody2D>(),
            anim = GetComponent<Animator>(),
            moveSpeed = this.moveSpeed,
            attackRange = this.attackRange,
            detectionRange = this.detectionRange
        };

        // 플레이어 타겟 설정
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null) blackboard.playerTarget = player.transform;

        // 2. 행동 트리 조립 (우선순위: 공격 > 추적 > 대기)
        // [공격 시퀀스]: 사거리 체크 -> 랜덤 스킬
        Sequence attackSequence = new Sequence(blackboard, new List<Node>
        {
            new CheckPlayerDistance(blackboard, blackboard.attackRange),
            new RandomSkillNode(blackboard)
        });

        // [추적 시퀀스]: 인식 범위 체크 -> 플레이어 따라가기
        Sequence followSequence = new Sequence(blackboard, new List<Node>
        {
            new CheckPlayerDistance(blackboard, blackboard.detectionRange),
            new FollowPlayerNode(blackboard)
        });

        // [최상위 선택자]: 공격할 수 있으면 하고, 안 되면 쫓아가고, 둘 다 아니면 대기
        rootNode = new Selector(blackboard, new List<Node>
        {
            attackSequence,
            followSequence,
            new ActionNode_Idle(blackboard) // 아래 보너스 코드 참고
        });
    }

    void Update()
    {
        // 3. 매 프레임 트리의 'Evaluate'를 호출하여 AI 가동
        if (rootNode != null)
        {
            rootNode.Evaluate();
        }
    }
}

// 덤: 아무것도 안 할 때의 Idle 노드
public class ActionNode_Idle : Node
{
    public ActionNode_Idle(BossBlackboard bb) : base(bb) { }
    public override NodeState Evaluate()
    {
        blackboard.rb.linearVelocity = Vector2.zero;
        blackboard.anim.SetFloat("Speed", 0);
        return NodeState.SUCCESS;
    }
}