using UnityEngine;

public abstract class BaseSkillAction
{
    protected enum Phase { Ready, Indicator, Delay, Execute }
    protected Phase currentPhase;
    protected float timer;
    protected GameObject owner;
    protected BossBlackboard bb;
    protected GameObject activeIndicator;
    protected virtual float ReadyDuration => 2.0f;
    protected virtual Vector3 IndicatorScale => Vector3.one;
    protected Quaternion IndicatorRotation;
    
    protected abstract float IndicatorDuration { get; }
    protected abstract float DelayDuration { get; }
    protected abstract string AnimTriggerName { get; }
    protected abstract GameObject IndicatorPrefab { get; }
    protected BossAI bossAI;
    protected bool isAnimationSignalReceived = false;
    protected virtual Sprite IndicatorSprite => owner.GetComponent<BossAI>().circleSprite;

    public void Initialize(GameObject owner, BossBlackboard bb) {
        this.owner = owner;
        this.bb = bb;
        this.bossAI = owner.GetComponent<BossAI>();
    }

    public virtual void OnStart() {
        timer = 0f;
        currentPhase = Phase.Indicator;
        if (IndicatorPrefab != null)
            activeIndicator = Object.Instantiate(IndicatorPrefab, owner.transform.position, Quaternion.identity);
        activeIndicator.transform.localScale = IndicatorScale;
        activeIndicator.transform.SetParent(owner.transform);
        activeIndicator.GetComponent<SpriteRenderer>().sprite = IndicatorSprite;
        activeIndicator.transform.rotation = IndicatorRotation;

        if (bossAI != null)
            bossAI.OnAttackPoint = HandleActionSignal;
    }

    protected virtual void HandleActionSignal()
    {
        isAnimationSignalReceived = true;
        timer = 0;
        OnExecuteStart();
    }

    public NodeState OnUpdate() {
        timer += Time.deltaTime;
        switch (currentPhase) {
            case Phase.Ready:
                if (timer >= ReadyDuration)
                {
                    if (IndicatorPrefab != null)
                    {
                        activeIndicator = Object.Instantiate(IndicatorPrefab, owner.transform.position, IndicatorRotation);
                    }
                    else 
                        activeIndicator.SetActive(true);

                    timer = 0;
                    currentPhase = Phase.Indicator;
                }
                return NodeState.RUNNING;
            case Phase.Indicator:
                if (timer >= IndicatorDuration) {
                    if (activeIndicator != null) activeIndicator.SetActive(false);
                    currentPhase = Phase.Delay;
                }
                return NodeState.RUNNING;
            case Phase.Delay:
                bb.anim.SetTrigger(AnimTriggerName);
                currentPhase = Phase.Execute;
                return NodeState.RUNNING;
            case Phase.Execute:
                if (!isAnimationSignalReceived)
                {
                    return NodeState.RUNNING;
                }
                return OnExecuteUpdate(); 
        }
        return NodeState.SUCCESS;
    }

    public virtual void OnEnd() {
        if (activeIndicator != null) Object.Destroy(activeIndicator);
    }

    protected abstract void OnExecuteStart();
    protected abstract NodeState OnExecuteUpdate();
}