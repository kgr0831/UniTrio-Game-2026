using UnityEngine;

public abstract class BaseSkillAction
{
    protected enum Phase { Indicator, Delay, Execute }
    protected Phase currentPhase;
    protected float timer;
    protected GameObject owner;
    protected BossBlackboard bb;
    protected GameObject activeIndicator;
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
        isAnimationSignalReceived = false;
        currentPhase = Phase.Indicator;

        bb.isAttacking = true;
        bb.rb.linearVelocity = Vector2.zero;
        bb.anim.SetBool("isMove", false);

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

    public virtual NodeState OnUpdate() {
        timer += Time.deltaTime;

        bb.rb.linearVelocity = Vector2.zero;

        switch (currentPhase) {
            case Phase.Indicator:
                if (timer >= IndicatorDuration) {
                    if (activeIndicator != null) activeIndicator.SetActive(false);
                    timer = 0f;
                    currentPhase = Phase.Delay;
                }
                return NodeState.RUNNING;

            case Phase.Delay:
                if (timer >= DelayDuration) {
                    bb.anim.SetTrigger(AnimTriggerName);
                    timer = 0f;
                    currentPhase = Phase.Execute;
                }
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
        bb.isAttacking = false;
        bb.cooldownTimer = bb.attackCooldown;
        if (activeIndicator != null) Object.Destroy(activeIndicator);
    }

    protected abstract void OnExecuteStart();
    protected abstract NodeState OnExecuteUpdate();

    /// <summary>
    /// 플레이어에게 데미지와 넉백을 적용한다. knockback이 zero면 넉백 없음.
    /// 넉백은 PlayerMovement.ApplyRecoil(속도 단위)로 가해진다.
    /// </summary>
    protected void DamagePlayer(float damage, Vector2 knockback)
    {
        // 골렘 근접 공격 시 주변 채집물(나무/돌)을 한 방에 파괴
        BreakNearbyGatherables();

        if (bb.playerTarget == null) return;

        var target = bb.playerTarget.GetComponentInParent<IDamageable>();
        if (target == null || !target.IsAlive) return;

        target.TakeDamage(damage, owner);

        if (knockback != Vector2.zero)
        {
            var move = bb.playerTarget.GetComponentInParent<PlayerMovement>();
            if (move != null) move.ApplyRecoil(knockback);
        }
    }

    // 골렘 공격 시 주변 채집물을 한 방에 파괴 (owner=골렘, BossAI 보유 → GatherableNode가 즉시 파괴)
    private const float GatherableBreakRadius = 5f;
    private static int _gatherableMask = -1;
    private static readonly Collider2D[] _gatherBuf = new Collider2D[16];

    protected void BreakNearbyGatherables()
    {
        if (owner == null) return;
        if (_gatherableMask == -1) _gatherableMask = LayerMask.GetMask("Gatherable");
        if (_gatherableMask == 0) return;

        int n = Physics2D.OverlapCircleNonAlloc(owner.transform.position, GatherableBreakRadius, _gatherBuf, _gatherableMask);
        for (int i = 0; i < n; i++)
        {
            if (_gatherBuf[i] == null) continue;
            var g = _gatherBuf[i].GetComponentInParent<IDamageable>();
            if (g != null && g.IsAlive)
                g.TakeDamage(9999f, owner);
        }
    }
}
