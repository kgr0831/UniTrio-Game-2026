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

    private const float AreaScale = 18f; // 시각 텔레그래프 크기(기존 12의 1.5배). 균열도 이에 비례해 커짐
    private const float ChargeDuration = 1.1f;
    private const float SlamAnimTime = 0.2f;
    private const float PostImpactFreeze = 2.0f;

    private const float SlamDamage = 75f;
    private const float SlamDamageRadius = 9f; // 명중 반경(AreaScale 1.5배에 맞춰 6→9). 시각 텔레그래프와 정합
    private const float SlamKnockback = 12f;   // 약한 방사형 넉백

    private GameObject outerIndicator;
    private GameObject redIndicator;
    private GameObject chargingParticles;
    private float chargeTimer;

    private const float RedStartScale = 0.3f; // 빨간 원 시작 반지름 스케일

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

        // 빨간 원: 반지름 확장 (시작 반지름 → 풀)
        if (redIndicator != null)
        {
            float scale = Mathf.Lerp(RedStartScale, AreaScale, progress);
            redIndicator.transform.localScale = new Vector3(scale, scale, 1f);
        }

        if (progress >= 1f)
        {
            // 빨간 원 가득 채움 (내려치는 동안 유지)
            if (redIndicator != null) redIndicator.transform.localScale = new Vector3(AreaScale, AreaScale, 1f);
            // 원 다 참 → 0.2초간 슬램 애니메이션 재생
            slamPhase = SlamPhase.Slamming;
            timer = 0f;

            // 슬램 부분(0.326~1.0)을 0.2초에 재생하기 위한 속도 계산
            float clipRemaining = (1f - SlamStartNorm) * 3.833f; // ~2.58초
            float slamSpeed = clipRemaining / SlamAnimTime;
            bb.anim.Play("Attack01", 0, SlamStartNorm);
            bb.anim.speed = slamSpeed;

            // 원은 끄지 않고 내려치는 모션(0.2초) 동안 가득 찬 상태로 유지 → 임팩트 때 제거
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
        // 임팩트 순간에 원 제거 (차징 완료~타격까지 가득 찬 원 유지)
        if (outerIndicator != null) outerIndicator.SetActive(false);
        if (redIndicator != null) redIndicator.SetActive(false);

        CameraShakeController.Instance?.Shake(0.4f, 1.0f);
        HitStopManager.Instance?.TriggerHitStop(0.12f);
        ScreenFlashEffect.Instance?.Flash(new Color(1f, 1f, 1f, 0.6f), 0.15f);

        // 반경 내 플레이어에게 데미지 + 보스 반대 방향 약한 넉백
        if (bb.playerTarget != null)
        {
            Vector2 toPlayer = bb.playerTarget.position - owner.transform.position;
            if (toPlayer.magnitude <= SlamDamageRadius)
            {
                Vector2 dir = toPlayer.sqrMagnitude > 0.0001f ? toPlayer.normalized : Vector2.up;
                DamagePlayer(SlamDamage, dir * SlamKnockback);
            }
        }

        if (bb.bossAI.slamImpactPrefab != null)
        {
            GameObject fx = Object.Instantiate(bb.bossAI.slamImpactPrefab, owner.transform.position, Quaternion.identity);
            fx.transform.localScale = Vector3.one * 4.5f; // 1.5배 확대(슬램 크기와 정합)
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
        // 외곽 원: 주황, 전체 크기 — 바닥 Tilemap(order 2) 위, 골렘(10)/플레이어(5) 아래(order 3)에 깔아 바닥 AOE 텔레그래프
        outerIndicator = Object.Instantiate(bb.indicator, owner.transform.position, Quaternion.identity);
        outerIndicator.transform.localScale = new Vector3(AreaScale, AreaScale, 1f);
        var outerSR = outerIndicator.GetComponent<SpriteRenderer>();
        outerSR.sprite = bossAI.circleSprite;
        outerSR.color = new Color(1f, 0.5f, 0.2f, 0.5f);
        outerSR.sortingOrder = 3;

        // 빨간 원: 반지름 확장(0→풀) — 외곽(3) 위, 골렘(10)/플레이어(5) 아래(order 4).
        // 차오름 초반엔 반경이 골렘보다 작아 중앙부가 가려지고 가장자리부터 보임(바닥 AOE의 자연스러운 모습)
        redIndicator = Object.Instantiate(bb.indicator, owner.transform.position, Quaternion.identity);
        redIndicator.transform.localScale = new Vector3(RedStartScale, RedStartScale, 1f);
        var redSR = redIndicator.GetComponent<SpriteRenderer>();
        redSR.sprite = bossAI.circleSprite;
        redSR.color = new Color(1f, 0.15f, 0.1f, 0.4f);
        redSR.sortingOrder = 4;
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
    private enum RushPhase { Charging, Rushing, Done }
    private RushPhase rushPhase;

    // BaseSkillAction의 표준 흐름은 쓰지 않고 OnStart/OnUpdate를 직접 구동한다.
    protected override float IndicatorDuration => 0f;
    protected override float DelayDuration => 0f;
    protected override string AnimTriggerName => "Attack03";
    protected override GameObject IndicatorPrefab => bb.indicator;

    private const float RushSpeed = 40f;
    private const float RushDistance = 14f;
    private const float ChargeDuration = 1.0f;
    private const float WidthScale = 2f;
    private const float RushAnimLoops = 2f; // 돌진 동안 Attack03 반복 횟수

    private const float RushDamage = 75f;
    private const float RushHitRadius = 1.8f; // 돌진 폭(WidthScale 2) 기준
    private const float RushKnockback = 35f;  // 강한 옆 넉백
    private bool rushHit;                      // 돌진 1회당 1번만 타격

    private GameObject staticIndicator;
    private GameObject fillIndicator;

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Vector3 rushDir;
    private Quaternion rushRotation;
    private float rushDistance; // 실제 돌진 거리(아레나 경계로 클램프될 수 있음)
    private float lengthScale;
    private float nearEdgeLocalY; // 스프라이트 피벗 기준 근접 끝단(min.y) 오프셋
    private float chargeTimer;

    protected override void OnExecuteStart() { }
    protected override NodeState OnExecuteUpdate() { return NodeState.SUCCESS; }

    public override void OnStart()
    {
        timer = 0f;
        chargeTimer = 0f;
        rushHit = false;
        rushPhase = RushPhase.Charging;

        // 시작 시점의 플레이어 방향으로 돌진 방향 고정 (텔레그래프)
        startPosition = owner.transform.position;
        if (bb.playerTarget != null)
        {
            Vector3 diff = bb.playerTarget.position - startPosition;
            diff.z = 0f;
            rushDir = diff.sqrMagnitude > 0.0001f ? diff.normalized : (Vector3)owner.transform.right;
        }
        else
        {
            rushDir = owner.transform.right;
        }
        // 골렘 구역 안이면 벽을 넘지 않도록 돌진 거리를 원 경계로 클램프
        rushDistance = RushDistance;
        if (bossAI != null && bossAI.hasArenaBounds)
            rushDistance = ClampDistanceToArena(startPosition, rushDir, RushDistance,
                                                bossAI.arenaCenter, bossAI.arenaRadius);
        targetPosition = startPosition + rushDir * rushDistance;

        // 돌진 방향으로 좌우 반전 (수직에 가까우면 유지)
        if (bb.sr != null && Mathf.Abs(rushDir.x) > 0.01f)
            bb.sr.flipX = rushDir.x < 0f;

        // 스프라이트 단위높이(스케일 1) 위에 위치하도록 회전 (로컬 up → rushDir)
        float angle = Mathf.Atan2(rushDir.y, rushDir.x) * Mathf.Rad2Deg - 90f;
        rushRotation = Quaternion.Euler(0f, 0f, angle);

        bb.isAttacking = true;
        bb.rb.linearVelocity = Vector2.zero;
        bb.anim.SetBool("isMove", false);

        SpawnIndicators();

        // Attack03 첫 프레임 고정
        bb.anim.Play("Attack03", 0, 0f);
        bb.anim.Update(0f);
        bb.anim.speed = 0f;
    }

    public override NodeState OnUpdate()
    {
        switch (rushPhase)
        {
            case RushPhase.Charging:
                return UpdateCharging();
            case RushPhase.Rushing:
                return UpdateRushing();
            case RushPhase.Done:
                return NodeState.SUCCESS;
        }
        return NodeState.SUCCESS;
    }

    private NodeState UpdateCharging()
    {
        bb.rb.linearVelocity = Vector2.zero;

        chargeTimer += Time.deltaTime;
        float progress = Mathf.Clamp01(chargeTimer / ChargeDuration);

        // 채우는 인디케이터: 근접 끝단을 골렘에 고정한 채 길이만 0→풀로 신장 (골렘→목적지 방향으로 차오름)
        if (fillIndicator != null)
        {
            float curScale = lengthScale * progress;
            fillIndicator.transform.localScale = new Vector3(WidthScale, curScale, 1f);
            fillIndicator.transform.position = startPosition - rushDir * (nearEdgeLocalY * curScale);
        }

        if (progress >= 1f)
        {
            rushPhase = RushPhase.Rushing;
            // 다 차면 인디케이터 2개 모두 제거
            if (staticIndicator != null) staticIndicator.SetActive(false);
            if (fillIndicator != null) fillIndicator.SetActive(false);
            // 돌진 애니메이션은 UpdateRushing에서 진행도에 맞춰 수동 재생
            bb.anim.Play("Attack03", 0, 0f);
            bb.anim.speed = 0f;
        }

        return NodeState.RUNNING;
    }

    private NodeState UpdateRushing()
    {
        owner.transform.position = Vector3.MoveTowards(
            owner.transform.position,
            targetPosition,
            RushSpeed * Time.deltaTime);

        // 돌진 경로상에서 플레이어와 겹치면 데미지 + 옆(경로 수직) 넉백 (1회)
        if (!rushHit && bb.playerTarget != null)
        {
            Vector2 toPlayer = bb.playerTarget.position - owner.transform.position;
            if (toPlayer.magnitude <= RushHitRadius)
            {
                rushHit = true;
                // 돌진 방향에 수직인 좌측 벡터, 플레이어가 있는 쪽으로 부호 결정
                Vector2 perp = new Vector2(-rushDir.y, rushDir.x);
                float side = Vector2.Dot(toPlayer, perp) >= 0f ? 1f : -1f;
                DamagePlayer(RushDamage, perp * side * RushKnockback);
            }
        }

        // 돌진 진행도(이동 거리)에 맞춰 Attack03를 RushAnimLoops회 반복 재생
        float traveled = Vector3.Distance(startPosition, owner.transform.position);
        float progress = Mathf.Clamp01(traveled / Mathf.Max(0.01f, rushDistance));
        float normTime = (progress * RushAnimLoops) % 1f;
        bb.anim.Play("Attack03", 0, normTime);
        bb.anim.speed = 0f;
        bb.anim.Update(0f);

        if (Vector3.Distance(owner.transform.position, targetPosition) <= 0.05f)
        {
            owner.transform.position = targetPosition;
            OnRushImpact();
            rushPhase = RushPhase.Done;
            return NodeState.SUCCESS;
        }

        return NodeState.RUNNING;
    }

    private void OnRushImpact()
    {
        CameraShakeController.Instance?.Shake(0.2f, 0.4f);

        if (bb.bossAI.rushImpactPrefab != null)
        {
            GameObject fx = Object.Instantiate(bb.bossAI.rushImpactPrefab, owner.transform.position, Quaternion.identity);
            Object.Destroy(fx, 2f);
        }
    }

    // start에서 dir 방향으로 maxDist만큼 갈 때, 중심 center·반지름 R 원을 벗어나지 않는 최대 거리를 반환.
    // (start가 원 안에 있다고 가정. 레이-원 교차의 양의 근 = 원을 빠져나가는 지점)
    private static float ClampDistanceToArena(Vector3 start, Vector3 dir, float maxDist, Vector2 center, float R)
    {
        Vector2 s = (Vector2)(Vector3)start - center;
        Vector2 d = ((Vector2)(Vector3)dir).normalized;
        float b = Vector2.Dot(s, d);
        float c = Vector2.Dot(s, s) - R * R;
        float disc = b * b - c;
        if (disc < 0f) return maxDist;          // 교차 없음(이론상 원 안이면 발생 안 함)
        float tExit = -b + Mathf.Sqrt(disc);    // 원을 빠져나가는 거리
        return Mathf.Clamp(maxDist, 0f, Mathf.Max(0f, tExit));
    }

    private void SpawnIndicators()
    {
        // 정적 인디케이터: 전체 길이, 주황 — 그대로 유지
        staticIndicator = Object.Instantiate(bb.indicator, startPosition, rushRotation);
        var staticSR = staticIndicator.GetComponent<SpriteRenderer>();
        staticSR.sprite = bossAI.squareSprite;
        staticSR.color = new Color(1f, 0.5f, 0.2f, 0.45f);
        staticSR.sortingOrder = 3; // 바닥 Tilemap(2) 위, 골렘/플레이어 아래

        // 스프라이트 단위높이/피벗 기준 근접 끝단 산출 (피벗이 끝단/중앙 어디든 골렘에 정렬)
        float unitHeight = staticSR.sprite.bounds.size.y;
        lengthScale = unitHeight > 0.0001f ? rushDistance / unitHeight : 1f;
        nearEdgeLocalY = staticSR.sprite.bounds.min.y;

        staticIndicator.transform.localScale = new Vector3(WidthScale, lengthScale, 1f);
        // 근접 끝단이 골렘(startPosition)에 오도록 피벗 오프셋 보정
        staticIndicator.transform.position = startPosition - rushDir * (nearEdgeLocalY * lengthScale);

        // 채우는 인디케이터: 길이 0에서 시작, 빨강
        fillIndicator = Object.Instantiate(bb.indicator, startPosition, rushRotation);
        var fillSR = fillIndicator.GetComponent<SpriteRenderer>();
        fillSR.sprite = bossAI.squareSprite;
        fillSR.color = new Color(1f, 0.15f, 0.1f, 0.55f);
        fillSR.sortingOrder = 4; // static(3) 위, 골렘/플레이어 아래
        fillIndicator.transform.localScale = new Vector3(WidthScale, 0f, 1f);
        fillIndicator.transform.position = startPosition;
    }

    public override void OnEnd()
    {
        bb.isAttacking = false;
        bb.cooldownTimer = bb.attackCooldown;

        bb.anim.speed = 1f;
        bb.anim.Play("Idle", 0, 0f);
        bb.anim.Update(0f);

        if (staticIndicator != null) Object.Destroy(staticIndicator);
        if (fillIndicator != null) Object.Destroy(fillIndicator);
    }
}

public class ThrowSkill : BaseSkillAction
{
    private enum ThrowPhase { Charging, Holding, Done }
    private ThrowPhase throwPhase;

    // BaseSkillAction의 표준 흐름(Indicator/Delay)은 쓰지 않고 OnStart/OnUpdate를 직접 구동한다.
    protected override float IndicatorDuration => 0f;
    protected override float DelayDuration => 0f;
    protected override string AnimTriggerName => "Attack02";
    protected override GameObject IndicatorPrefab => bb.indicator;
    protected override Sprite IndicatorSprite => bossAI.squareSprite;

    private const float ThrowDamage = 50f;
    private const float ThrowKnockback = 10f; // 약한 진행방향 넉백
    private const float ThrowAnimSpeed = 0.7f; // 투척 애니메이션 재생 속도 (기존 1.0의 0.7배)
    private const float RockSpeed = 28f;       // 40 × 0.7 (느려진 투사체)
    private const float AimResponse = 2.2f;    // 조준 추적 응답성(지수 감쇠). 낮을수록 플레이어를 더 늦게(지연) 따라감
    private const float BoxLength = 12.5f; // 25 × 0.5
    private const float BoxWidth = 2.25f;  // 1.5 × 1.5
    private const float PostThrowHold = 0.5f; // 발사 후 마지막 프레임 유지하며 정지하는 시간
    private const float ChargeTimeout = 5f;   // 안전장치: 애니 이벤트 누락 시 강제 발사까지의 최대 준비 시간

    private GameObject staticIndicator;
    private GameObject fillIndicator;

    private Vector3 throwDir;          // 현재 조준 방향 (매 프레임 갱신)
    private Quaternion throwRotation;  // 로컬 up이 throwDir을 향하는 회전
    private float lengthScale;         // 스프라이트 단위높이 → BoxLength 스케일
    private float nearEdgeLocalY;      // 피벗 기준 근접 끝단(min.y) 오프셋
    private float holdTimer;
    private float chargeTimer;         // 준비 페이즈 경과 시간(안전장치용)
    private float lastProgress;        // 직전 프레임 진행도(전이 중 0 리셋 방지)
    private bool thrown;
    private Vector3 aimDir = Vector3.zero; // 현재(보간된) 조준 방향 — 느린 추적용

    protected override void OnExecuteStart() { }
    protected override NodeState OnExecuteUpdate() { return NodeState.SUCCESS; }

    public override void OnStart()
    {
        timer = 0f;
        holdTimer = 0f;
        chargeTimer = 0f;
        lastProgress = 0f;
        thrown = false;
        throwPhase = ThrowPhase.Charging;

        bb.isAttacking = true;
        bb.rb.linearVelocity = Vector2.zero;
        bb.anim.SetBool("isMove", false);

        // 초기 조준 (즉시 플레이어 정렬, 이후 Charging 동안 느리게 추적)
        AimAtPlayer(true);

        SpawnIndicators();
        UpdateIndicatorTransforms(0f);

        // 프레임 40의 TriggerAttack 애니메이션 이벤트로 발사 시점을 받는다.
        if (bossAI != null)
            bossAI.OnAttackPoint = OnThrowSignal;

        // Attack02 자연 재생(0.7배 속도) → 프레임 40에서 TriggerAttack 이벤트 발동
        bb.anim.Play("Attack02", 0, 0f);
        bb.anim.speed = ThrowAnimSpeed;
        bb.anim.Update(0f);
    }

    public override NodeState OnUpdate()
    {
        bb.rb.linearVelocity = Vector2.zero; // 스킬 내내 이동 금지

        switch (throwPhase)
        {
            case ThrowPhase.Charging:
                return UpdateCharging();
            case ThrowPhase.Holding:
                return UpdateHolding();
            case ThrowPhase.Done:
                return NodeState.SUCCESS;
        }
        return NodeState.SUCCESS;
    }

    private NodeState UpdateCharging()
    {
        // 매 프레임 플레이어 추적: flipX/박스 방향이 플레이어를 향한다.
        AimAtPlayer();

        // 준비 진행도 = Attack02 정규화 시간 (프레임 40에서 1)
        // 전이/첫 프레임에 Attack02가 아니면 직전 진행도 유지(0으로 튀어 깜빡임 방지)
        var st = bb.anim.GetCurrentAnimatorStateInfo(0);
        float progress = st.IsName("Attack02") ? Mathf.Clamp01(st.normalizedTime) : lastProgress;
        lastProgress = progress;

        UpdateIndicatorTransforms(progress);

        // 안전장치: 프레임 40 TriggerAttack 이벤트가 누락되어도(Exit Time 등)
        // 진행도 도달 또는 최대 준비시간 초과 시 강제 발사 → 무한 정지 방지.
        chargeTimer += Time.deltaTime;
        if (!thrown && (progress >= 0.999f || chargeTimer >= ChargeTimeout))
            OnThrowSignal();

        return NodeState.RUNNING;
    }

    private NodeState UpdateHolding()
    {
        // 마지막 프레임 고정 유지
        bb.anim.Play("Attack02", 0, 1f);
        bb.anim.speed = 0f;
        bb.anim.Update(0f);

        holdTimer += Time.deltaTime;
        if (holdTimer >= PostThrowHold)
        {
            throwPhase = ThrowPhase.Done;
            return NodeState.SUCCESS;
        }
        return NodeState.RUNNING;
    }

    // 프레임 40 TriggerAttack 이벤트 → 그 순간의 경로로 돌 발사
    private void OnThrowSignal()
    {
        if (thrown) return;
        thrown = true;

        // 발사 순간의 플레이어 방향으로 경로 확정
        AimAtPlayer();

        CameraShakeController.Instance?.Shake(0.15f, 0.3f);

        Vector3 spawnPos = new Vector3(
            owner.transform.position.x - 0.24f,
            owner.transform.position.y + 0.66f,
            owner.transform.position.z);

        GameObject rock = Object.Instantiate(bossAI.rockPrefeb, spawnPos, owner.transform.rotation);
        var rc = rock.GetComponent<RockController>();
        rc.moveRotation = throwRotation;
        rc.speed = RockSpeed;
        rc.damage = ThrowDamage;
        rc.knockback = ThrowKnockback;
        rc.source = owner;
        rc.maxRange = BoxLength; // 텔레그래프 길이 = 실제 사거리

        // 인디케이터 제거, 마지막 프레임 고정 + 정지 페이즈 진입
        if (staticIndicator != null) staticIndicator.SetActive(false);
        if (fillIndicator != null) fillIndicator.SetActive(false);

        bb.anim.speed = 0f;
        bb.anim.Play("Attack02", 0, 1f);
        bb.anim.Update(0f);

        holdTimer = 0f;
        throwPhase = ThrowPhase.Holding;
    }

    // 플레이어 방향으로 throwDir/throwRotation/flipX 갱신.
    // instant=false면 AimTurnSpeed(도/초)로 천천히 추적(즉시 스냅 X) → 느린 조준.
    private void AimAtPlayer(bool instant = false)
    {
        Vector3 target = (aimDir != Vector3.zero) ? aimDir : owner.transform.up;
        if (bb.playerTarget != null)
        {
            Vector3 diff = bb.playerTarget.position - owner.transform.position;
            diff.z = 0f;
            if (diff.sqrMagnitude > 0.0001f) target = diff.normalized;
        }

        if (instant || aimDir == Vector3.zero)
            aimDir = target;
        else
        {
            // 지수 감쇠 보간: 거리와 무관하게 일정한 지연으로 플레이어를 트레일링(늦게 따라감)
            float k = 1f - Mathf.Exp(-AimResponse * Time.deltaTime);
            aimDir = Vector3.Slerp(aimDir, target, k).normalized;
        }

        Vector3 dir = aimDir;
        throwDir = dir;

        // 로컬 up이 throwDir을 향하도록 (RockController는 moveRotation*Vector3.up으로 이동)
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        throwRotation = Quaternion.Euler(0f, 0f, angle);

        if (bb.sr != null && Mathf.Abs(dir.x) > 0.01f)
            bb.sr.flipX = dir.x < 0f;
    }

    private void SpawnIndicators()
    {
        // 정적 인디케이터: 전체 길이, 주황 (돌진과 동일 메커니즘, 크기만 다름)
        staticIndicator = Object.Instantiate(bb.indicator, owner.transform.position, throwRotation);
        var staticSR = staticIndicator.GetComponent<SpriteRenderer>();
        staticSR.sprite = bossAI.squareSprite;
        staticSR.color = new Color(1f, 0.5f, 0.2f, 0.45f);
        staticSR.sortingOrder = 3; // 바닥 Tilemap(2) 위, 골렘/플레이어 아래

        float unitHeight = staticSR.sprite.bounds.size.y;
        lengthScale = unitHeight > 0.0001f ? BoxLength / unitHeight : 1f;
        nearEdgeLocalY = staticSR.sprite.bounds.min.y;

        // 채우는 인디케이터: 길이 0→풀로 신장, 빨강
        fillIndicator = Object.Instantiate(bb.indicator, owner.transform.position, throwRotation);
        var fillSR = fillIndicator.GetComponent<SpriteRenderer>();
        fillSR.sprite = bossAI.squareSprite;
        fillSR.color = new Color(1f, 0.15f, 0.1f, 0.55f);
        fillSR.sortingOrder = 4; // static(3) 위, 골렘/플레이어 아래
    }

    // 박스 2개를 골렘에 근접 끝단 고정, throwRotation 정렬, fill은 progress만큼 신장
    private void UpdateIndicatorTransforms(float progress)
    {
        Vector3 pos = owner.transform.position;

        if (staticIndicator != null)
        {
            staticIndicator.transform.rotation = throwRotation;
            staticIndicator.transform.localScale = new Vector3(BoxWidth, lengthScale, 1f);
            staticIndicator.transform.position = pos - throwDir * (nearEdgeLocalY * lengthScale);
        }

        if (fillIndicator != null)
        {
            float curScale = lengthScale * progress;
            fillIndicator.transform.rotation = throwRotation;
            fillIndicator.transform.localScale = new Vector3(BoxWidth, curScale, 1f);
            fillIndicator.transform.position = pos - throwDir * (nearEdgeLocalY * curScale);
        }
    }

    public override void OnEnd()
    {
        bb.isAttacking = false;
        bb.cooldownTimer = bb.attackCooldown;

        if (bossAI != null) bossAI.OnAttackPoint = null;

        bb.anim.speed = 1f;
        bb.anim.Play("Idle", 0, 0f);
        bb.anim.Update(0f);

        if (staticIndicator != null) Object.Destroy(staticIndicator);
        if (fillIndicator != null) Object.Destroy(fillIndicator);
    }
}


