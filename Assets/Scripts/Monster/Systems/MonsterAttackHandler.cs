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
    private Collider2D[] _hitResults = new Collider2D[10];

    private void Awake()
    {
        _runtime        = GetComponent<MonsterRuntimeData>();
        _animController = GetComponent<MonsterAnimatorController>();
        _visualizer     = GetComponent<AttackRangeVisualizer>();
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

        // 시간 지연 후 데미지 판정 (StartCoroutine 할당 방지를 위해 커스텀 타이머 가능하지만 임시 코루틴)
        StartCoroutine(AttackDelayRoutine(aimDirection));
    }

    private IEnumerator AttackDelayRoutine(Vector2 aimDirection)
    {
        yield return new WaitForSeconds(_runtime.AttackDelay);

        if (_runtime.IsStaggered) yield break; // 경직 시 공격 취소

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
        var health = targetObj.GetComponent<HealthSystem>();
        if (health != null)
        {
            health.ApplyDamage((int)_runtime.BaseATK); // float->int 캐스팅 필요 시
        }
    }
}
