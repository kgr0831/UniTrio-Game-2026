using UnityEngine;

public class SlamSkill : BaseSkillAction
{
    private enum SlamPhase { Charging, Slamming, Freeze, Done }
    private SlamPhase slamPhase;

    protected override float IndicatorDuration => 0f;
    protected override float DelayDuration => 0f;
    protected override string AnimTriggerName => "Attack01";
    protected override GameObject IndicatorPrefab => bb.indicator;
    protected override Vector3 IndicatorScale => new Vector3(AreaScale, AreaScale, 1f);

    private const float AreaScale = 12f;
    private const float ChargeDuration = 1.1f;
    private const float SlamAnimTime = 0.2f;
    private const float PostImpactFreeze = 2.0f;

    private GameObject outerIndicator;
    private GameObject redIndicator;
    private GameObject chargingParticles;
    private float chargeTimer;

    private static readonly float[] PrepFrames = { 0f, 0.1087f, 0.2174f };
    private const float SlamStartNorm = 0.326f;
    private const float FrameCycleDuration = 0.25f;

    protected override void OnExecuteStart() { }
    protected override NodeState OnExecuteUpdate() { return NodeState.SUCCESS; }

    public override void OnStart()
    {
        IndicatorRotation = Quaternion.identity;
        timer = 0f;
        chargeTimer = 0f;
        slamPhase = SlamPhase.Charging;

        bb.isAttacking = true;
        bb.rb.linearVelocity = Vector2.zero;
        bb.anim.SetBool("isMove", false);

        SpawnIndicators();
        SpawnChargingParticles();

        bb.anim.Play("Attack01", 0, 0f);
        bb.anim.Update(0f);
        bb.anim.speed = 0f;
    }

    public override NodeState OnUpdate()
    {
        bb.rb.linearVelocity = Vector2.zero;

        // 인디케이터를 보스 위치에 따라감
        Vector3 pos = owner.transform.position;
        if (outerIndicator != null && outerIndicator.activeSelf) outerIndicator.transform.position = pos;
        if (redIndicator != null && redIndicator.activeSelf) redIndicator.transform.position = pos;

        switch (slamPhase)
        {
            case SlamPhase.Charging:
                return UpdateCharging();
            case SlamPhase.Slamming:
                return UpdateSlamming();
            case SlamPhase.Freeze:
                return UpdateFreeze();
            case SlamPhase.Done:
                return NodeState.SUCCESS;
        }
        return NodeState.SUCCESS;
    }

    private NodeState UpdateCharging()
    {
        chargeTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(chargeTimer / ChargeDuration);

        // 스프라이트 1~3 순환 반복
        float cycleTime = chargeTimer % (FrameCycleDuration * PrepFrames.Length);
        int frameIndex = Mathf.Clamp((int)(cycleTime / FrameCycleDuration), 0, PrepFrames.Length - 1);
        bb.anim.Play("Attack01", 0, PrepFrames[frameIndex]);
        bb.anim.speed = 0f;
        bb.anim.Update(0f);

        // 빨간 원 선형 확장 (월드 스케일)
        if (redIndicator != null)
        {
            float scale = Mathf.Lerp(0.3f, AreaScale, progress);
            redIndicator.transform.localScale = new Vector3(scale, scale, 1f);
        }

        if (progress >= 1f)
        {
            // 원 다 참 → 0.2초간 슬램 애니메이션 재생
            slamPhase = SlamPhase.Slamming;
            timer = 0f;

            // 슬램 부분(0.326~1.0)을 0.2초에 재생하기 위한 속도 계산
            float clipRemaining = (1f - SlamStartNorm) * 3.833f; // ~2.58초
            float slamSpeed = clipRemaining / SlamAnimTime;
            bb.anim.Play("Attack01", 0, SlamStartNorm);
            bb.anim.speed = slamSpeed;

            if (outerIndicator != null) outerIndicator.SetActive(false);
            if (redIndicator != null) redIndicator.SetActive(false);
            if (chargingParticles != null)
            {
                var ps = chargingParticles.GetComponent<ParticleSystem>();
                if (ps != null) ps.Stop();
            }
        }

        return NodeState.RUNNING;
    }

    private NodeState UpdateSlamming()
    {
        timer += Time.deltaTime;

        if (timer >= SlamAnimTime)
        {
            // 마지막 프레임 고정 + 임팩트
            bb.anim.Play("Attack01", 0, 0.99f);
            bb.anim.speed = 0f;
            bb.anim.Update(0f);

            OnSlamImpact();

            slamPhase = SlamPhase.Freeze;
            timer = 0f;
        }

        return NodeState.RUNNING;
    }

    private NodeState UpdateFreeze()
    {
        timer += Time.deltaTime;

        if (timer >= PostImpactFreeze)
        {
            // Idle로 명시적 복귀
            bb.anim.speed = 1f;
            bb.anim.Play("Idle", 0, 0f);
            bb.anim.Update(0f);
            return NodeState.SUCCESS;
        }
        return NodeState.RUNNING;
    }

    private void OnSlamImpact()
    {
        CameraShakeController.Instance?.Shake(0.4f, 1.0f);
        HitStopManager.Instance?.TriggerHitStop(0.12f);
        ScreenFlashEffect.Instance?.Flash(new Color(1f, 1f, 1f, 0.6f), 0.15f);

        if (bb.bossAI.slamImpactPrefab != null)
        {
            GameObject fx = Object.Instantiate(bb.bossAI.slamImpactPrefab, owner.transform.position, Quaternion.identity);
            fx.transform.localScale = Vector3.one * 3f;
            Object.Destroy(fx, 2f);
        }

        if (bb.bossAI.groundCrackPrefab != null)
        {
            GameObject crack = Object.Instantiate(bb.bossAI.groundCrackPrefab, owner.transform.position, Quaternion.identity);
            crack.transform.localScale = Vector3.one * (AreaScale * 1.28f / 4f);
        }
    }

    private void SpawnIndicators()
    {
        // 외곽 원: 주황, 전체 크기 — 골렘 뒤(sortingOrder < 10)
        outerIndicator = Object.Instantiate(bb.indicator, owner.transform.position, Quaternion.identity);
        outerIndicator.transform.localScale = new Vector3(AreaScale, AreaScale, 1f);
        var outerSR = outerIndicator.GetComponent<SpriteRenderer>();
        outerSR.sprite = bossAI.circleSprite;
        outerSR.color = new Color(1f, 0.5f, 0.2f, 0.55f);
        outerSR.sortingOrder = 2;

        // 빨간 원: 작게 시작, 점점 커짐 — 플레이어(5) 아래
        redIndicator = Object.Instantiate(bb.indicator, owner.transform.position, Quaternion.identity);
        redIndicator.transform.localScale = new Vector3(0.3f, 0.3f, 1f);
        var redSR = redIndicator.GetComponent<SpriteRenderer>();
        redSR.sprite = bossAI.circleSprite;
        redSR.color = new Color(1f, 0.15f, 0.1f, 0.55f);
        redSR.sortingOrder = 3;
    }

    private void SpawnChargingParticles()
    {
        var particleGO = new GameObject("SlamChargingParticles");
        particleGO.transform.position = owner.transform.position;
        particleGO.transform.SetParent(owner.transform);

        var ps = particleGO.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.playOnAwake = false;
        main.duration = ChargeDuration;
        main.startLifetime = 1.2f;
        main.startSpeed = 1.5f;
        main.startSize = 0.15f;
        main.startColor = new Color(0.6f, 0.5f, 0.35f, 0.8f);
        main.gravityModifier = -0.5f;
        main.maxParticles = 30;
        main.loop = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = ps.emission;
        emission.rateOverTime = 12f;

        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 2f;

        var sizeOverLifetime = ps.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0, 0.5f), new Keyframe(0.5f, 1f), new Keyframe(1, 0f)
        ));

        var renderer = particleGO.GetComponent<ParticleSystemRenderer>();
        renderer.material = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));

        chargingParticles = particleGO;
        ps.Play();
    }

    public override void OnEnd()
    {
        bb.isAttacking = false;
        bb.cooldownTimer = bb.attackCooldown;
        bb.anim.speed = 1f;

        if (outerIndicator != null) Object.Destroy(outerIndicator);
        if (redIndicator != null) Object.Destroy(redIndicator);
        if (chargingParticles != null) Object.Destroy(chargingParticles);
    }
}

public class RushSkill : BaseSkillAction
{
    protected override float IndicatorDuration => 1.5f;
    protected override float DelayDuration => 0.2f;
    protected override string AnimTriggerName => "Attack03";
    protected override GameObject IndicatorPrefab => bb.indicator;
    protected override Vector3 IndicatorScale => new Vector3(2f, 10f, 1f);
    protected override Sprite IndicatorSprite => owner.GetComponent<BossAI>().squareSprite;

    private Vector3 targetPosition;
    private bool isInitialized = false;
    private float rushSpeed = 15f;

    protected override void OnExecuteStart() { }

    protected override NodeState OnExecuteUpdate() {
        if (!isInitialized)
        {
            InitializeMove();
        }
        owner.transform.position = Vector3.MoveTowards(
            owner.transform.position,
            targetPosition,
            rushSpeed * Time.deltaTime
        );

        if (Vector3.Distance(owner.transform.position, targetPosition) <= 0.01f)
        {
            owner.transform.position = targetPosition;
            isInitialized = false;

            CameraShakeController.Instance?.Shake(0.2f, 0.4f);
            SpawnImpactEffect(owner.transform.position);

            return NodeState.SUCCESS;
        }

        return NodeState.RUNNING;
    }

    private void InitializeMove()
    {
        if (activeIndicator != null)
        {
            var sr = activeIndicator.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                float heightOffset = sr.bounds.size.y;
                targetPosition = owner.transform.position + (activeIndicator.transform.up * heightOffset);
            }
        }
        isInitialized = true;
    }

    public override void OnStart()
    {
        IndicatorRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        base.OnStart();
    }

    private void SpawnImpactEffect(Vector3 position)
    {
        if (bb.bossAI.rushImpactPrefab != null)
        {
            GameObject fx = Object.Instantiate(bb.bossAI.rushImpactPrefab, position, Quaternion.identity);
            Object.Destroy(fx, 2f);
        }
    }
}

public class ThrowSkill : BaseSkillAction
{
    protected override float IndicatorDuration => 1f;
    protected override float DelayDuration => 0.3f;
    protected override string AnimTriggerName => "Attack02";
    protected override GameObject IndicatorPrefab => bb.indicator;
    protected override Vector3 IndicatorScale => new Vector3(2f, 20f, 1f);
    protected override Sprite IndicatorSprite => owner.GetComponent<BossAI>().squareSprite;

    protected override void OnExecuteStart() { }

    protected override NodeState OnExecuteUpdate() {
        if (timer >= 0.5f) return NodeState.SUCCESS;
        return NodeState.RUNNING;
    }

    protected override void HandleActionSignal()
    {
        CameraShakeController.Instance?.Shake(0.15f, 0.3f);

        Vector3 spawnPos = new Vector3(
            owner.transform.position.x - 0.24f,
            owner.transform.position.y + 0.66f,
            owner.transform.position.z);

        GameObject rock = Object.Instantiate(
            owner.GetComponent<BossAI>().rockPrefeb,
            spawnPos,
            owner.transform.rotation);

        var rc = rock.GetComponent<RockController>();
        rc.moveRotation = IndicatorRotation;

        base.HandleActionSignal();
    }

    public override void OnStart()
    {
        IndicatorRotation = Quaternion.Euler(0, 0, Random.Range(0f, 360f));
        base.OnStart();
    }
}
