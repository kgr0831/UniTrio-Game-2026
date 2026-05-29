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

    private GameObject staticIndicator;
    private GameObject fillIndicator;

    // 돌진 VFX(가산) — Casual_Hit Trail_1(스피드라인) + Ring_1(확산 충격파). 본체 스프라이트는 건드리지 않는다.
    private Material vfxMat;        // 공유 가산 머티리얼(런타임)
    private Sprite streakSprite;    // Trail_1 런타임 스프라이트
    private Sprite ringSprite;      // Ring_1 런타임 스프라이트
    private float streakTimer;
    private float ringTimer;
    private static readonly Color RushGlowColor = new Color(1f, 0.5f, 0.12f); // 주황 (HDR 배율로 발광)
    private const float VfxHdrBoost = 1.8f;             // HDR 배율(Bloom 유도, 과하면 흰색)
    private const float StreakInterval = 0.025f;        // 스피드라인 생성 간격(초)
    private const float RingInterval = 0.09f;           // 충격파 링 생성 간격(초)
    private const float VfxScale = 0.3f;                // 전체 VFX 크기 배율(작게)
    private float golemSizeCached;                      // 골렘 월드 크기(VfxScale 적용 전 기준)

    private Vector3 startPosition;
    private Vector3 targetPosition;
    private Vector3 rushDir;
    private Quaternion rushRotation;
    private float lengthScale;
    private float nearEdgeLocalY; // 스프라이트 피벗 기준 근접 끝단(min.y) 오프셋
    private float chargeTimer;

    protected override void OnExecuteStart() { }
    protected override NodeState OnExecuteUpdate() { return NodeState.SUCCESS; }

    public override void OnStart()
    {
        timer = 0f;
        chargeTimer = 0f;
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
        targetPosition = startPosition + rushDir * RushDistance;

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
            // 돌진 VFX 이미터 초기화
            InitRushVfx();
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

        // 돌진 진행도(이동 거리)에 맞춰 Attack03를 RushAnimLoops회 반복 재생
        float traveled = Vector3.Distance(startPosition, owner.transform.position);
        float progress = Mathf.Clamp01(traveled / RushDistance);
        float normTime = (progress * RushAnimLoops) % 1f;
        bb.anim.Play("Attack03", 0, normTime);
        bb.anim.speed = 0f;
        bb.anim.Update(0f);

        // 이동 중 스피드라인 + 충격파 링 방출
        EmitRushVfx();

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

    // 돌진 VFX 초기화: 공유 가산 머티리얼 + 텍스처→스프라이트 준비. 돌진 진입 시 1회 호출.
    private void InitRushVfx()
    {
        golemSizeCached = (bb.sr != null) ? Mathf.Max(bb.sr.bounds.size.x, bb.sr.bounds.size.y) : 2f;
        streakTimer = 0f;
        ringTimer = 0f;

        Shader add = Shader.Find("Custom/AdditiveSpriteVFX");
        if (add != null)
        {
            vfxMat = new Material(add);
            vfxMat.SetColor("_Color", RushGlowColor * VfxHdrBoost); // HDR 주황 → Bloom 발광
        }

        if (bossAI.rushStreakTex != null)
        {
            var t = bossAI.rushStreakTex;
            streakSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        }
        if (bossAI.rushRingTex != null)
        {
            var t = bossAI.rushRingTex;
            ringSprite = Sprite.Create(t, new Rect(0, 0, t.width, t.height), new Vector2(0.5f, 0.5f), 100f);
        }
    }

    // 이동 중 일정 간격으로 스피드라인/충격파 링을 방출한다.
    private void EmitRushVfx()
    {
        float dt = Time.deltaTime;

        streakTimer += dt;
        if (streakTimer >= StreakInterval)
        {
            streakTimer = 0f;
            SpawnStreak();
        }

        ringTimer += dt;
        if (ringTimer >= RingInterval)
        {
            ringTimer = 0f;
            SpawnRing();
        }
    }

    // 스피드라인: 진행 축을 따라 길쭉한 Trail_1을, 골렘 주변 임의 위치에 월드 고정으로 남긴다(이동하면 뒤로 흘러감).
    private void SpawnStreak()
    {
        if (vfxMat == null || streakSprite == null) return;

        var go = new GameObject("RushStreak");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = streakSprite;
        sr.sharedMaterial = vfxMat;
        sr.sortingLayerID = bb.sr != null ? bb.sr.sortingLayerID : 0;
        sr.sortingOrder = (bb.sr != null ? bb.sr.sortingOrder : 10) - 1; // 본체 뒤
        sr.color = new Color(1f, 1f, 1f, 1f); // 페이드 시작 알파(가산: 흰색=머티리얼 색 그대로)

        // 진행 방향(+X)을 rushDir로 정렬 (Trail_1은 가로로 누운 빛줄기)
        float angle = Mathf.Atan2(rushDir.y, rushDir.x) * Mathf.Rad2Deg;
        go.transform.rotation = Quaternion.Euler(0f, 0f, angle);

        float s = golemSizeCached * VfxScale;

        // 골렘 중심에서 진행축에 수직으로 임의 분산
        Vector3 perp = new Vector3(-rushDir.y, rushDir.x, 0f);
        float side = Random.Range(-0.45f, 0.45f) * s;
        float along = Random.Range(-0.2f, 0.2f) * s;
        go.transform.position = owner.transform.position + perp * side + rushDir * along;

        // 길쭉하고 얇게
        float len = s * Random.Range(1.1f, 1.7f);
        float thin = s * Random.Range(0.08f, 0.16f);
        go.transform.localScale = new Vector3(len, thin, 1f);

        // 월드 고정(vel=0) → 골렘이 앞으로 가면 상대적으로 뒤로 흘러 스피드라인
        go.AddComponent<RushFxPiece>().Init(0.16f, Random.Range(0.5f, 0.8f), 0f, Vector3.zero);
    }

    // 충격파 링: 골렘 앞에서 Ring_1을 진행 방향으로 밀어내며 확대+페이드.
    private void SpawnRing()
    {
        if (vfxMat == null || ringSprite == null) return;

        var go = new GameObject("RushRing");
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ringSprite;
        sr.sharedMaterial = vfxMat;
        sr.sortingLayerID = bb.sr != null ? bb.sr.sortingLayerID : 0;
        sr.sortingOrder = (bb.sr != null ? bb.sr.sortingOrder : 10) + 1; // 본체 앞
        sr.color = new Color(1f, 1f, 1f, 1f);

        float s = golemSizeCached * VfxScale;
        go.transform.position = owner.transform.position + rushDir * (s * 0.4f);
        float start = s * 0.5f;
        go.transform.localScale = new Vector3(start, start, 1f);

        // 진행 방향으로 밀려나가며 커지고 옅어짐
        go.AddComponent<RushFxPiece>().Init(0.3f, 0.85f, 1.6f, rushDir * (s * 1.2f));
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
        lengthScale = unitHeight > 0.0001f ? RushDistance / unitHeight : 1f;
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

        // 이미 방출된 스피드라인/링은 각자 페이드 후 자가 소멸한다(공유 머티리얼은 그들이 사라진 뒤 정리).
        if (vfxMat != null) Object.Destroy(vfxMat, 1f);
        if (streakSprite != null) Object.Destroy(streakSprite, 1f);
        if (ringSprite != null) Object.Destroy(ringSprite, 1f);
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

// 돌진 VFX 한 조각(스피드라인/링)의 확대+이동+페이드를 스스로 처리하고 끝나면 소멸한다.
public class RushFxPiece : MonoBehaviour
{
    private SpriteRenderer sr;
    private float life, elapsed, startAlpha, scaleGrowth;
    private Vector3 vel, baseScale;

    public void Init(float lifetime, float startAlpha, float scaleGrowth, Vector3 worldVel)
    {
        sr = GetComponent<SpriteRenderer>();
        life = Mathf.Max(0.01f, lifetime);
        this.startAlpha = startAlpha;
        this.scaleGrowth = scaleGrowth;
        vel = worldVel;
        baseScale = transform.localScale;
        elapsed = 0f;

        if (sr != null)
        {
            Color c = sr.color;
            c.a = startAlpha;
            sr.color = c;
        }
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float k = elapsed / life;
        if (k >= 1f || sr == null)
        {
            Destroy(gameObject);
            return;
        }

        transform.localScale = baseScale * (1f + scaleGrowth * k);
        transform.position += vel * Time.deltaTime;

        Color c = sr.color;
        c.a = Mathf.Lerp(startAlpha, 0f, k);
        sr.color = c;
    }
}
