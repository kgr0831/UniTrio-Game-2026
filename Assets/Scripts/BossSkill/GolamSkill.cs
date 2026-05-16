using UnityEngine;

public class SlamSkill : BaseSkillAction
{
    protected override float IndicatorDuration => 1.2f;
    protected override float DelayDuration => 3;
    protected override string AnimTriggerName => "Attack01";
    protected override GameObject IndicatorPrefab => bb.indicator;
    protected override Vector3 IndicatorScale => new Vector3(5f, 5f, 1f); // 반지름 5짜리 큰 원

    protected override void OnExecuteStart() {
        Debug.Log("쾅! 주변 데미지 판정");
    }

    protected override NodeState OnExecuteUpdate() {
        if (timer >= IndicatorDuration + DelayDuration ) return NodeState.SUCCESS;
        return NodeState.RUNNING;
    }
}

public class RushSkill : BaseSkillAction
{
    protected override float IndicatorDuration => 2f;
    protected override float DelayDuration => 2;
    protected override string AnimTriggerName => "Attack03";
    protected override GameObject IndicatorPrefab => bb.indicator;
    protected override Vector3 IndicatorScale => new Vector3(2f, 10f, 1f);
    protected override Sprite IndicatorSprite => owner.GetComponent<BossAI>().squareSprite;
    
    private Vector3 targetPosition;
    private bool isInitialized = false;
    public float moveSpeed = 10f; 


    protected override void OnExecuteStart() {
        Debug.Log("쾅! 주변 데미지 판정");
    }

    protected override NodeState OnExecuteUpdate() {
        if (!isInitialized) 
        {
            InitializeMove();
        }
        owner.transform.position = Vector3.MoveTowards(
            owner.transform.position, 
            targetPosition, 
            moveSpeed * Time.deltaTime
        );

        // 3. 목적지 도착 판정 및 결과 반환
        if (Vector3.Distance(owner.transform.position, targetPosition) <= 0.01f) 
        {
            owner.transform.position = targetPosition; // 위치 보정
            isInitialized = false; // 다음 실행을 위해 플래그 초기화
            return NodeState.SUCCESS; // 이동 완료
        }
        
        return NodeState.RUNNING;
    }
    
    private void InitializeMove() 
    {
        var spriteRenderer = activeIndicator.GetComponent<SpriteRenderer>();
        if (spriteRenderer != null) 
        {
            float heightOffset = spriteRenderer.bounds.size.y;
            targetPosition = new Vector3(owner.transform.position.x, owner.transform.position.y + heightOffset, owner.transform.position.z);
            targetPosition = owner.transform.position + (activeIndicator.transform.up * heightOffset);
        }
    
        isInitialized = true;
    }

    public override void OnStart()
    {
        IndicatorRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f)); 
        base.OnStart();
    }
    
}

public class ThrowSkill : BaseSkillAction
{
    protected override float IndicatorDuration => 1f;
    protected override float DelayDuration => 3;
    protected override string AnimTriggerName => "Attack02";
    protected override GameObject IndicatorPrefab => bb.indicator;
    protected override Vector3 IndicatorScale => new Vector3(2f, 20f, 1f);
    protected override Sprite IndicatorSprite => owner.GetComponent<BossAI>().squareSprite;


    protected override void OnExecuteStart() {
        Debug.Log("쾅! 주변 데미지 판정");
    }
    
    
    protected override NodeState OnExecuteUpdate() {
        if (timer >= IndicatorDuration + DelayDuration ) return NodeState.SUCCESS;
        return NodeState.RUNNING;
    }

    protected override void HandleActionSignal()
    {
        GameObject rock = Object.Instantiate(owner.GetComponent<BossAI>().rockPrefeb, new Vector3(owner.transform.position.x-0.24f, owner.transform.position.y+0.66f, owner.transform.position.z), owner.transform.rotation);
        rock.GetComponent<RockController>().moveRotation = IndicatorRotation;
        base.HandleActionSignal();
    }

    public override void OnStart()
    {
        IndicatorRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f)); 
        base.OnStart();
    }
    
    
}