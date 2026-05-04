using System.Collections.Generic;
using UnityEngine;
using System.Collections;

public class BossAI : MonoBehaviour
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
        blackboard = new BossBlackboard
        {
            bossTransform = transform,
            rb = GetComponent<Rigidbody2D>(),
            anim = GetComponent<Animator>(),
            moveSpeed = this.moveSpeed,
            attackRange = this.attackRange,
            detectionRange = this.detectionRange,
            bossAI = this
        };
        
        GameObject player = GameObject.FindGameObjectWithTag(playerTag);
        if (player != null) blackboard.playerTarget = player.transform;
        
        Sequence attackSequence = new Sequence(blackboard, new List<Node>
        {
            new CheckPlayerDistance(blackboard, blackboard.attackRange),
            new RandomSkillNode(blackboard)
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

    public void RandomSkill()
    {
        int skillIdx = Random.Range(1, 3);
        switch (skillIdx)
        {
            case 1:
                StartCoroutine(Skill1());
                break;
            case 2:
                StartCoroutine(Skill2());
                break;
            case 3:
                StartCoroutine(Skill3());
                break;
        }
    }


    public virtual IEnumerator Skill1()
    {
        return null;
    } 
    
    public virtual IEnumerator Skill2()
    {
        return null;
    } 
    
    public virtual IEnumerator Skill3()
    {
        return null;
    } 
}