using UnityEngine;

/// <summary>
/// 개별 몹 인스턴스의 런타임 상태.
/// MonsterData(SO)에서 초기값을 복사하여 사용.
/// HealthSystem은 HP 전담, 이 클래스는 그 외 동적 상태만 관리 (SRP).
/// </summary>
public sealed class MonsterRuntimeData : MonoBehaviour
{
    [Header("Reference")]
    [Tooltip("이 몹의 정적 데이터 (Inspector에서 할당)")]
    [SerializeField] private MonsterData _data;

    // ── 정적 데이터 접근 (읽기 전용) ────────────────────────────
    public MonsterData Data         => _data;
    public int         MonsterId    => _data.MonsterId;
    public MonsterType Type         => _data.Type;
    public float       BaseATK      => _data.ATK;
    public float       BaseDEF      => _data.DEF;
    public float       BaseSpeed    => _data.Speed;
    public float       AttackSpeed  => _data.AttackSpeed;
    public float       AttackDelay  => _data.AttackDelay;

    /// <summary>
    /// 이번 타격에 사용할 공격력을 반환.
    /// ATKMax가 ATKMin보다 크면 [ATKMin, ATKMax] 범위에서 랜덤, 아니면 단일 ATK.
    /// </summary>
    public float RollAttackDamage()
    {
        if (_data != null && _data.ATKMax > _data.ATKMin)
            return Random.Range(_data.ATKMin, _data.ATKMax);
        return BaseATK;
    }

    // ── 속성 저항 ──────────────────────────────────────────
    public float FireResistance  => _data != null ? _data.FireResistance  : 0f;
    public float IceResistance   => _data != null ? _data.IceResistance   : 0f;
    public float EarthResistance => _data != null ? _data.EarthResistance : 0f;

    /// <summary>ElementType에 대응하는 속성 저항을 반환합니다.</summary>
    public float GetElementalResistance(ElementType element)
    {
        switch (element)
        {
            case ElementType.Fire:  return FireResistance;
            case ElementType.Ice:   return IceResistance;
            case ElementType.Earth: return EarthResistance;
            default:                return 0f;
        }
    }

    // ── 동적 상태 ─────────────────────────────────────────────
    /// <summary>현재 이동속도 (디버프 등으로 변동 가능)</summary>
    public float CurrentSpeed { get; set; }

    /// <summary>피격 경직 중 여부</summary>
    public bool IsStaggered { get; set; }

    /// <summary>현재 공격 쿨다운 타이머</summary>
    public float AttackCooldownTimer { get; set; }

    /// <summary>현재 몹의 BT 상태</summary>
    public MonsterState CurrentState { get; set; }

    /// <summary>현재 바라보는 방향 (애니메이션 블렌드트리용, 기본 하단)</summary>
    public Vector2 CurrentDirection { get; set; }

    /// <summary>감지된 플레이어 Transform (null이면 미감지)</summary>
    public Transform DetectedPlayer { get; set; }

    /// <summary>추적 해제 타이머 (적대적 몹용, 4초 카운트)</summary>
    public float LoseAggroTimer { get; set; }

    // ── 초기화 ─────────────────────────────────────────────────

    /// <summary>풀에서 활성화될 때마다 호출. SO 데이터로 초기값 리셋.</summary>
    public void Initialize(MonsterData data)
    {
        _data              = data;
        if(data != null) {
            CurrentSpeed   = data.Speed;
        }
        IsStaggered        = false;
        AttackCooldownTimer = 0f;
        CurrentState       = MonsterState.Idle;
        CurrentDirection   = Vector2.down;
        DetectedPlayer     = null;
        LoseAggroTimer     = 0f;
    }
}
