using UnityEngine;

/// <summary>
/// 모든 몹의 공통 기반 클래스.
/// BT 루프 구동 및 공통 컴포넌트 캐싱 담당.
/// IDamageable을 구현하여 무기로부터 데미지를 정상적으로 받을 수 있습니다.
/// </summary>
[RequireComponent(typeof(MonsterRuntimeData))]
[RequireComponent(typeof(HealthSystem))]
[RequireComponent(typeof(DebuffReceiver))]
[RequireComponent(typeof(MonsterHPBar))]
[RequireComponent(typeof(DebuffOverlayController))]
[RequireComponent(typeof(DebuffIconDisplay))]
public abstract class MonsterBase : MonoBehaviour, IDamageable
{
    protected BTNode             _rootNode;
    protected MonsterRuntimeData _runtime;
    protected HealthSystem       _health;
    protected DebuffReceiver     _debuffReceiver;

    public bool IsAlive => _health != null && _health.IsAlive;

    /// <summary>이 몬스터의 효과음 종류. 파생 클래스에서 오버라이드하면 피격/사망/경계음이 재생됩니다.</summary>
    protected virtual MonsterSfxKind SfxKind => MonsterSfxKind.None;

    private DetectionSystem _sfxDetection;

    public virtual void TakeDamage(float damage, GameObject source = null)
    {
        if (_health != null && _health.IsAlive)
        {
            // 땅 디버프에 의한 받는 피해 증가 배율 적용
            float multiplier = _debuffReceiver != null ? _debuffReceiver.GetDamageMultiplier() : 1f;
            _health.ApplyDamage(damage * multiplier);
        }
    }

    protected virtual void Awake()
    {
        _runtime        = GetComponent<MonsterRuntimeData>();
        _health         = GetComponent<HealthSystem>();
        _debuffReceiver = GetComponent<DebuffReceiver>();

        // 종별 피격/사망/경계음 구독 (풀링 재사용 대비 Awake 1회 구독, OnDestroy 해제)
        if (_health != null)
        {
            _health.OnHit  += HandleSfxHit;
            _health.OnDied += HandleSfxDeath;
        }

        _sfxDetection = GetComponent<DetectionSystem>();
        if (_sfxDetection != null)
            _sfxDetection.OnTargetAcquired += HandleSfxAlert;
    }

    protected virtual void OnDestroy()
    {
        if (_health != null)
        {
            _health.OnHit  -= HandleSfxHit;
            _health.OnDied -= HandleSfxDeath;
        }
        if (_sfxDetection != null)
            _sfxDetection.OnTargetAcquired -= HandleSfxAlert;
    }

    // ── 종별 효과음 핸들러 ─────────────────────────────────────────
    private void HandleSfxHit()
    {
        // 치명타(사망)일 때는 ApplyDamage에서 IsAlive가 먼저 false로 바뀌므로 사망음만 재생됨
        if (_health != null && _health.IsAlive && AudioManager.Instance != null)
            AudioManager.Instance.PlayMonsterHit(SfxKind);
    }

    private void HandleSfxDeath()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMonsterDeath(SfxKind);
    }

    private void HandleSfxAlert()
    {
        if (AudioManager.Instance != null)
            AudioManager.Instance.PlayMonsterAlert(SfxKind);
    }

    protected virtual void OnEnable()
    {
        // 풀에서 꺼내질 때마다 BT 초기화
        _rootNode = BuildBT();

        // 런타임 데이터 초기화가 SO 할당 이후에 이뤄지도록 Manager나 Spawner에서 주입하지만 
        // 여기서도 초기화 보장이 안되면 Spawner가 Init 해줍니다.
    }

    protected virtual void Update()
    {
        // 회피 저스트 카운터 중에는 모든 몬스터 행동 정지
        if (MonsterFreezeManager.IsFrozen) return;

        // 체력이 없거나 사망 상태라면 BT 정지
        if (!_health.IsAlive || _runtime.CurrentState == MonsterState.Death) return;

        // 경직 상태 시에는 행동 중지 (HitStaggerHandler가 경직 해제를 담당)
        if (_runtime.IsStaggered) return;

        // 공격 쿨다운 처리
        if (_runtime.AttackCooldownTimer > 0f)
            _runtime.AttackCooldownTimer -= Time.deltaTime;

        // BT 실행
        if (_rootNode != null)
        {
            _rootNode.Execute();
        }
    }

    /// <summary>각 파생 클래스에서 고유의 BT(행동 패턴)를 구축하여 반환</summary>
    protected abstract BTNode BuildBT();
}
