using UnityEngine;
using System.Collections;

/// <summary>
/// 몹의 실제 공격 판정을 수행 (NonAlloc 활용 최적화).
/// AttackDelay에 맞춘 비동기(혹은 코루틴/타이머) 판정 지원.
/// </summary>
[RequireComponent(typeof(MonsterRuntimeData))]
public sealed class MonsterAttackHandler : MonoBehaviour
{
    private MonsterRuntimeData        _runtime;
    private MonsterAnimatorController _animController;
    private AttackRangeVisualizer     _visualizer;

    [SerializeField] private LayerMask _playerMask;

    [Header("Animation Event Mode")]
    [Tooltip("true면 AttackDelay 코루틴 대신 공격 애니메이션의 Animation Event(OnAttackHitFrame)에서 타격 판정. " +
             "곰처럼 특정 프레임에 정확히 판정해야 하는 몹에 사용.")]
    [SerializeField] private bool _useAnimationEvent = false;

    [Header("Hitbox Collider Mode (optional)")]
    [Tooltip("true면 AttackShape 대신 좌/우 히트박스 콜라이더로 타격 판정한다. 곰처럼 방향별 히트박스를 쓰는 몹에 사용.")]
    [SerializeField] private bool _useHitboxColliders = false;
    [Tooltip("왼쪽을 바라볼 때 사용할 히트박스 콜라이더.")]
    [SerializeField] private Collider2D _leftHitbox;
    [Tooltip("오른쪽을 바라볼 때 사용할 히트박스 콜라이더.")]
    [SerializeField] private Collider2D _rightHitbox;

    private Collider2D[] _hitResults = new Collider2D[10];
    private ContactFilter2D _hitboxFilter;

    // Animation Event 모드용: 공격 시작 시 조준 방향을 저장해두고 이벤트 프레임에 사용
    private Vector2 _pendingAimDirection;
    private bool    _attackArmed;

    private void Awake()
    {
        _runtime        = GetComponent<MonsterRuntimeData>();
        _animController = GetComponent<MonsterAnimatorController>();
        _visualizer     = GetComponent<AttackRangeVisualizer>();

        _hitboxFilter = new ContactFilter2D { useTriggers = true };
        _hitboxFilter.SetLayerMask(_playerMask);
    }

    /// <summary>피격 등으로 진행 중인 공격을 취소한다(아직 판정 안 난 타격을 무효화).</summary>
    public void CancelAttack()
    {
        _attackArmed = false;
        StopAllCoroutines();
    }

    /// <summary>
    /// 공격 로직 진입점.
    /// 애니메이션 실행 -> 시각화 -> Delay 후 판정
    /// </summary>
    public void ExecuteAttack(Vector2 targetPosition)
    {
        if (_runtime == null || _runtime.Data == null) return;

        Vector2 aimDirection = (targetPosition - (Vector2)transform.position).normalized;
        _runtime.AttackCooldownTimer = 1f / _runtime.AttackSpeed;

        // 애니메이션 및 인디케이터
        _animController?.PlayAttack();
        _visualizer?.ShowIndicator(aimDirection, _runtime.AttackDelay);

        if (_useAnimationEvent)
        {
            // Animation Event(OnAttackHitFrame)가 판정을 호출할 때까지 조준 방향만 보관
            _pendingAimDirection = aimDirection;
            _attackArmed = true;
            return;
        }

        // 시간 지연 후 데미지 판정 (StartCoroutine 할당 방지를 위해 커스텀 타이머 가능하지만 임시 코루틴)
        StartCoroutine(AttackDelayRoutine(aimDirection));
    }

    /// <summary>
    /// 공격 애니메이션의 타격 프레임(곰: 10프레임 중 7프레임)에 걸린 Animation Event에서 호출.
    /// _useAnimationEvent가 true이고 공격이 armed 상태일 때만 판정.
    /// </summary>
    public void OnAttackHitFrame()
    {
        if (!_useAnimationEvent || !_attackArmed) return;
        _attackArmed = false;

        if (_runtime.IsStaggered) return; // 경직 시 공격 취소
        PerformHitDetection(_pendingAimDirection);
    }

    private IEnumerator AttackDelayRoutine(Vector2 aimDirection)
    {
        yield return new WaitForSeconds(_runtime.AttackDelay);

        if (_runtime.IsStaggered) yield break; // 경직 시 공격 취소

        PerformHitDetection(aimDirection);
    }

    /// <summary>AttackShape(또는 히트박스 콜라이더) 기준으로 플레이어를 찾아 데미지 적용.</summary>
    private void PerformHitDetection(Vector2 aimDirection)
    {
        // ── 히트박스 콜라이더 모드: 조준 방향(플레이어 쪽)에 맞는 좌/우 박스를 사용 ──
        if (_useHitboxColliders && (_leftHitbox != null || _rightHitbox != null))
        {
            bool faceRight = aimDirection.x >= 0f;
            Collider2D box = faceRight ? _rightHitbox : _leftHitbox;
            if (box == null) box = faceRight ? _leftHitbox : _rightHitbox;

            if (box != null)
            {
                _hitboxFilter.SetLayerMask(_playerMask);
                int n = box.Overlap(_hitboxFilter, _hitResults);
                for (int i = 0; i < n; i++)
                    ApplyDamage(_hitResults[i]);
                return;
            }
        }

        var shape = _runtime.Data.AttackShape;
        int hitCount = 0;

        // 형태별 NonAlloc 판정
        switch (shape.ShapeType)
        {
            case AttackShapeType.Circle:
                hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, shape.CircleRadius, _hitResults, _playerMask);
                break;
            case AttackShapeType.Rectangle:
                float angle = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
                Vector2 size = new Vector2(shape.RectWidth, shape.RectHeight);
                hitCount = Physics2D.OverlapBoxNonAlloc((Vector2)transform.position + aimDirection * (shape.RectWidth/2), size, angle, _hitResults, _playerMask);
                break;
            case AttackShapeType.Fan:
                // 원형으로 먼저 잡고, 각도 비교 (내적) 으로 필터링
                hitCount = Physics2D.OverlapCircleNonAlloc(transform.position, shape.FanRadius, _hitResults, _playerMask);
                for (int i = 0; i < hitCount; i++)
                {
                    Vector2 dirToHit = (_hitResults[i].transform.position - transform.position).normalized;
                    if (Vector2.Angle(aimDirection, dirToHit) <= shape.FanAngle / 2f)
                    {
                        ApplyDamage(_hitResults[i]);
                    }
                }
                hitCount = 0; // 이미 데미지 줌
                break;
        }

        // 데미지 적용
        for (int i = 0; i < hitCount; i++)
        {
            ApplyDamage(_hitResults[i]);
        }
    }

    private void ApplyDamage(Collider2D targetObj)
    {
        float dmg = _runtime.RollAttackDamage(); // ATKMin/ATKMax 설정 시 랜덤, 아니면 단일 ATK

        // IDamageable 경로 우선 → 대시 무적(IsInvincible) 등 수신측 보정을 존중
        var damageable = targetObj.GetComponentInParent<IDamageable>();
        if (damageable != null)
        {
            damageable.TakeDamage(dmg, gameObject);
            return;
        }

        // 폴백: IDamageable이 없으면 HealthSystem에 직접 적용
        var health = targetObj.GetComponentInParent<HealthSystem>();
        if (health != null)
            health.ApplyDamage(dmg);
    }
}
