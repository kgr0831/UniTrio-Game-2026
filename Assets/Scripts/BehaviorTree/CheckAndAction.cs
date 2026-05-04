using System.Collections;
using UnityEngine;

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

// 플레이어 거리 체크 노드
public class CheckPlayerDistance : Node
{
    private float range;
    public CheckPlayerDistance(BossBlackboard bb, float range) : base(bb) => this.range = range;

    public override NodeState Evaluate()
    {
        if (blackboard.playerTarget == null) return NodeState.FAILURE;
        
        float dist = Vector2.Distance(blackboard.bossTransform.position, blackboard.playerTarget.position);
        return dist <= range ? NodeState.SUCCESS : NodeState.FAILURE;
    }
}

// 2D 이동 및 애니메이션 노드
public class FollowPlayerNode : Node
{
    public FollowPlayerNode(BossBlackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        if (blackboard.playerTarget == null) return NodeState.FAILURE;

        Vector2 direction = (blackboard.playerTarget.position - blackboard.bossTransform.position).normalized;
        blackboard.rb.linearVelocity = direction * blackboard.moveSpeed;
        
        // 애니메이션 파라미터 업데이트
        blackboard.anim.SetFloat("Speed", blackboard.rb.linearVelocity.magnitude);
        
        // 목표 근처에 도달할 때까지 계속 실행 중임을 알림
        return NodeState.RUNNING;
    }
}

// 랜덤 스킬 노드 (애니메이션 태그 활용)
public class RandomSkillNode : Node
{
    public RandomSkillNode(BossBlackboard bb) : base(bb) { }

    public override NodeState Evaluate()
    {
        // 이미 공격 애니메이션 재생 중이면 끝날 때까지 RUNNING
        if (blackboard.anim.GetCurrentAnimatorStateInfo(0).IsTag("Attack") &&
            blackboard.anim.GetCurrentAnimatorStateInfo(0).normalizedTime < 1.0f)
        {
            blackboard.rb.linearVelocity = Vector2.zero; // 공격 중 이동 정지
            return NodeState.RUNNING;
        }
        blackboard.bossAI.RandomSkill(); // 랜덤 스킬 실시, 코루틴이 해당 클래스에서 상속받지 못했기에 일반 함수 출력, 추가로 코루틴이 돌아가는지에 따라 Running 상태 바꿔줘야 함
        // 새로운 공격 시작
        
        return NodeState.SUCCESS;
    }
}
