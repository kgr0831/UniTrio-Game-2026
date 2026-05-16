# Enemy System 설계 문서 (SOLID 준수)

> **프로젝트**: UniTrio-Game-2026-fork  
> **작성 일시**: 2026-04-10  
> **참조 문서**: `Monster System.md`, `Docs/EnemySystemAnalysis.md`  
> **설계 원칙**: SOLID (Single Responsibility, Open/Closed, Liskov Substitution, Interface Segregation, Dependency Inversion)

---

## 목차

1. [전체 아키텍처 개요](#1-전체-아키텍처-개요)
2. [MonsterData (ScriptableObject)](#2-monsterdata-scriptableobject)
3. [MonsterRuntimeData (동적 스탯)](#3-monsterruntimedata-동적-스탯)
4. [MonsterBase 및 Behavior Tree](#4-monsterbase-및-behavior-tree)
5. [피격 경직 시스템 (HitStagger)](#5-피격-경직-시스템-hitstagger)
6. [감지 시스템 (DetectionSystem)](#6-감지-시스템-detectionsystem)
7. [첫 처치 보너스 시스템 (FirstKillBonus)](#7-첫-처치-보너스-시스템-firstkillbonus)
8. [행동 및 네비게이션 시스템](#8-행동-및-네비게이션-시스템)
9. [비감지 배회 시스템 (WanderSystem)](#9-비감지-배회-시스템-wandersystem)
10. [오브젝트 풀링 및 스폰 시스템](#10-오브젝트-풀링-및-스폰-시스템)
11. [MobManager (전역 관리)](#11-mobmanager-전역-관리)
12. [아이템 드롭 및 획득 시스템](#12-아이템-드롭-및-획득-시스템)
13. [공격 범위 시각화 시스템](#13-공격-범위-시각화-시스템)
14. [SOLID 원칙 적용 요약](#14-solid-원칙-적용-요약)
15. [파일 구조](#15-파일-구조)

---

## 1. 전체 아키텍처 개요

### 1.1 클래스 상속 구조

```
IDamageable (기존 인터페이스)
│
└── LivingEntity (기존 추상 클래스)
    │
    └── CharacterBase (기존 - StatSystem 보유)
        │
        ├── PlayerEntity (기존)
        │
        └── MonsterBase (★ 신규 추상 클래스)
            ├── HostileMonster     (적대적 몹)
            ├── NeutralMonster     (중립 몹)
            └── BossMonster        (보스 몹)
```

### 1.2 시스템 의존 관계도

```
┌─────────────────────────────────────────────────────────────────┐
│                        MobManager (전역 싱글톤)                   │
│  ├─ MobSpawner (풀링 + 랜덤 생성)                                 │
│  ├─ List<MonsterBase> _allMonsters (전체 몹 추적)                  │
│  └─ FirstKillRegistry (첫 처치 기록)                               │
└────────────────────────┬────────────────────────────────────────┘
                         │ 등록/해제
┌────────────────────────▼────────────────────────────────────────┐
│                     MonsterBase (개별 몹)                         │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐   │
│  │ MonsterData  │  │MonsterRuntime│  │ BehaviorTree (BT)    │   │
│  │ (SO 정적)    │  │ Data (동적)  │  │ ├─ DetectionSystem   │   │
│  │              │  │              │  │ ├─ WanderSystem      │   │
│  │ - 스탯       │→│ - CurrentHP  │  │ ├─ ChaseSystem       │   │
│  │ - 드롭 테이블 │  │ - IsStagger  │  │ ├─ FleeSystem       │   │
│  │ - 공격 범위   │  │ - State      │  │ └─ AttackSystem     │   │
│  │ - 애니메이션  │  │              │  │                      │   │
│  └──────────────┘  └──────────────┘  └──────────────────────┘   │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────────────┐   │
│  │HealthSystem  │  │ HitStagger   │  │ MonsterAnimator      │   │
│  │ (기존 재사용) │  │ Handler      │  │ Controller           │   │
│  └──────────────┘  └──────────────┘  └──────────────────────┘   │
│                                                                  │
│  ┌──────────────┐  ┌──────────────┐                              │
│  │ MonsterAttack│  │ MonsterLoot  │                              │
│  │ Handler      │  │ Dropper      │                              │
│  └──────────────┘  └──────────────┘                              │
└──────────────────────────────────────────────────────────────────┘
```

### 1.3 적용 SOLID 원칙 요약

| 원칙 | 적용 |
|------|------|
| **S**RP | 각 컴포넌트가 단일 책임만 담당 (감지, 이동, 공격, 드롭 등 분리) |
| **O**CP | ScriptableObject 기반 데이터로 코드 수정 없이 새 몹 추가 가능 |
| **L**SP | `MonsterBase`의 파생 클래스(Hostile/Neutral/Boss)가 동일한 인터페이스로 교체 가능 |
| **I**SP | `IDetectable`, `IStaggerable` 등 세분화된 인터페이스 |
| **D**IP | 상위 모듈(MobManager)이 추상(`MonsterBase`)에 의존, 구체 타입에 의존하지 않음 |

---

## 2. MonsterData (ScriptableObject)

> **요구사항 1**: 몹의 정적 데이터를 관리하는 ScriptableObject

### 2.1 설계 원칙

- 기존 프로젝트의 `ItemData` 패턴(`ScriptableObject` + `[CreateAssetMenu]`)을 따름
- `MonsterData`를 **추상 기반**으로 두고, 파생 SO로 확장 가능 (OCP)

### 2.2 Enum 정의

```csharp
/// <summary>몹 종류 구분</summary>
public enum MonsterType
{
    Neutral  = 0,  // 중립
    Hostile  = 1,  // 적대적
    Boss     = 2   // 보스
}

/// <summary>공격 범위 형태 구분</summary>
public enum AttackShapeType
{
    Rectangle = 0,  // 사각형 (Width x Height)
    Fan       = 1,  // 부채꼴 (Angle + Radius)
    Circle    = 2   // 원형 (Radius)
}
```

### 2.3 공격 범위 데이터 구조

```csharp
/// <summary>
/// 공격 범위 정의. 크기 1 = Unity 단위 1.
/// </summary>
[System.Serializable]
public struct AttackShapeData
{
    [Tooltip("범위 형태")]
    public AttackShapeType ShapeType;

    [Header("Rectangle (사각형)")]
    [Tooltip("사각형 가로 크기 (Unity 단위). 예: 3 = 3칸")]
    public float RectWidth;
    [Tooltip("사각형 세로 크기 (Unity 단위). 예: 1 = 1칸")]
    public float RectHeight;

    [Header("Fan (부채꼴)")]
    [Tooltip("부채꼴 중심각 (도 단위). 예: 90 = 90°")]
    public float FanAngle;
    [Tooltip("부채꼴 반지름 (Unity 단위)")]
    public float FanRadius;

    [Header("Circle (원형)")]
    [Tooltip("원형 반지름 (Unity 단위)")]
    public float CircleRadius;
}
```

### 2.4 드롭 테이블 구조

```csharp
/// <summary>
/// 개별 드롭 아이템 엔트리.
/// 수량별 확률을 배열로 지정할 수 있어, "1개 80%, 2개 15%, 3개 5%" 같은 세밀한 설정이 가능.
/// </summary>
[System.Serializable]
public struct DropEntry
{
    [Tooltip("드롭할 아이템 데이터 (기존 ItemData 참조)")]
    public ItemData Item;

    [Tooltip("이 아이템이 드롭될 기본 확률 (0~1). 예: 0.8 = 80%")]
    [Range(0f, 1f)]
    public float DropChance;

    [Tooltip("최대 드롭 개수")]
    public int MaxCount;

    [Tooltip("개수별 가중치 배열. 인덱스 0 = 1개의 가중치, 인덱스 1 = 2개의 가중치, ...")]
    public float[] CountWeights;
}
```

**DropEntry 사용 예시 (Inspector)**:

| 필드 | 값 | 설명 |
|------|-----|------|
| Item | `IngredientData_낡은 가죽` | 드롭 아이템 |
| DropChance | 0.8 | 80% 확률로 드롭 |
| MaxCount | 3 | 최대 3개 |
| CountWeights | `[70, 20, 10]` | 1개=70%, 2개=20%, 3개=10% |

### 2.5 MonsterData 본체

```csharp
/// <summary>
/// 몹의 정적 데이터 (ScriptableObject).
/// 기존 프로젝트의 ItemData 패턴을 따르며, CreateAssetMenu로 에디터에서 생성.
/// </summary>
[CreateAssetMenu(fileName = "NewMonsterData", menuName = "Data/Monster")]
public class MonsterData : ScriptableObject
{
    [Header("═══ Identity ═══")]
    public int       MonsterId;         // 고유 ID (FirstKillRegistry에서 사용)
    public string    MonsterName;       // 표시명
    public MonsterType Type;            // Neutral / Hostile / Boss

    [Header("═══ Base Stats ═══")]
    public float MaxHP;                 // 최대 체력
    public float ATK;                   // 공격력
    public float DEF;                   // 방어력
    public float Speed;                 // 이동속도 (Unity 단위/초)
    [Tooltip("공격 속도 배율. 1.0 = 1초, 2.0 = 0.5초, 0.5 = 2초. 공식: 쿨다운 = 1 / AttackSpeed")]
    public float AttackSpeed;           // 공격 속도 배율
    [Tooltip("공격 범위 표시 후 실제 타격까지의 지연 시간 (초)")]
    public float AttackDelay;           // 공격 딜레이 (초)

    [Header("═══ Attack Range ═══")]
    public AttackShapeData AttackShape; // 공격 범위 데이터

    [Header("═══ Detection ═══")]
    [Tooltip("플레이어 감지 반경 (Unity 단위). 보스는 무한으로 별도 처리.")]
    public float DetectionRadius;       // 감지 반경

    [Header("═══ Drop Table ═══")]
    public DropEntry[] DropTable;       // 드롭 아이템 테이블

    [Header("═══ Visuals ═══")]
    public Sprite     IdleSprite;       // 기본 스프라이트
    public RuntimeAnimatorController AnimController; // Animator Controller

    [Header("═══ Prefab ═══")]
    [Tooltip("풀링에 사용되는 몹 프리팹")]
    public GameObject MonsterPrefab;    // 몹 프리팹
}
```

### 2.6 공격 속도 공식

```
쿨다운(초) = 1.0 / AttackSpeed

예시:
  AttackSpeed = 1.0  →  1.0 / 1.0 = 1.0초
  AttackSpeed = 2.0  →  1.0 / 2.0 = 0.5초
  AttackSpeed = 0.5  →  1.0 / 0.5 = 2.0초
```

---

## 3. MonsterRuntimeData (동적 스탯)

> **요구사항 2**: 몹의 동적 데이터를 관리하는 런타임 스크립트

### 3.1 설계 원칙

- `MonsterData`(SO)는 **읽기 전용 정적 데이터**, `MonsterRuntimeData`는 **인스턴스별 가변 상태**
- 기존 프로젝트의 `Item` 클래스(정적 `ItemData` + 동적 `Tier/Level/Count`) 패턴을 따름
- `HealthSystem`과 연동하여 HP는 기존 시스템에 위임 (SRP)

```csharp
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

    // ── 동적 상태 ─────────────────────────────────────────────
    /// <summary>현재 이동속도 (디버프 등으로 변동 가능)</summary>
    public float CurrentSpeed { get; set; }

    /// <summary>피격 경직 중 여부</summary>
    public bool IsStaggered { get; set; }

    /// <summary>현재 공격 쿨다운 타이머</summary>
    public float AttackCooldownTimer { get; set; }

    /// <summary>현재 몹의 BT 상태</summary>
    public MonsterState CurrentState { get; set; }

    /// <summary>감지된 플레이어 Transform (null이면 미감지)</summary>
    public Transform DetectedPlayer { get; set; }

    /// <summary>추적 해제 타이머 (적대적 몹용, 4초 카운트)</summary>
    public float LoseAggroTimer { get; set; }

    // ── 초기화 ─────────────────────────────────────────────────

    /// <summary>풀에서 활성화될 때마다 호출. SO 데이터로 초기값 리셋.</summary>
    public void Initialize(MonsterData data)
    {
        _data              = data;
        CurrentSpeed       = data.Speed;
        IsStaggered        = false;
        AttackCooldownTimer = 0f;
        CurrentState       = MonsterState.Idle;
        DetectedPlayer     = null;
        LoseAggroTimer     = 0f;
    }
}

/// <summary>몹의 행동 상태 enum (BT 노드 판별용)</summary>
public enum MonsterState
{
    Idle,       // 대기
    Wander,     // 자유 배회
    Chase,      // 추격 (적대적)
    Flee,       // 도망 (중립)
    Attack,     // 공격 중
    Stagger,    // 피격 경직
    Death       // 사망
}
```

---

## 4. MonsterBase 및 Behavior Tree

> **요구사항 3**: 애니메이션 전환, BT, 공격 처리를 관리하는 스크립트

### 4.1 MonsterBase (추상 기반 클래스)

```csharp
/// <summary>
/// 모든 몹의 공통 기반.
/// CharacterBase를 상속하여 HealthSystem + StatSystem 자동 부착.
/// 파생 클래스(HostileMonster, NeutralMonster, BossMonster)에서
/// ConfigureBehaviorTree()를 구현하여 행동 패턴을 결정.
/// </summary>
[RequireComponent(typeof(MonsterRuntimeData))]
[RequireComponent(typeof(HitStaggerHandler))]
[RequireComponent(typeof(MonsterAnimatorController))]
[RequireComponent(typeof(MonsterAttackHandler))]
[RequireComponent(typeof(MonsterLootDropper))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Collider2D))]
public abstract class MonsterBase : CharacterBase
{
    // ── 컴포넌트 참조 ─────────────────────────────────
    public MonsterRuntimeData      RuntimeData  { get; private set; }
    public HitStaggerHandler       Stagger      { get; private set; }
    public MonsterAnimatorController AnimCtrl   { get; private set; }
    public MonsterAttackHandler    Attack       { get; private set; }
    public Rigidbody2D             Rb           { get; private set; }

    // ── BT 루트 노드 ─────────────────────────────────
    protected BTNode _btRoot;

    protected override void Awake()
    {
        base.Awake();  // CharacterBase → LivingEntity → HealthSystem 초기화
        RuntimeData = GetComponent<MonsterRuntimeData>();
        Stagger     = GetComponent<HitStaggerHandler>();
        AnimCtrl    = GetComponent<MonsterAnimatorController>();
        Attack      = GetComponent<MonsterAttackHandler>();
        Rb          = GetComponent<Rigidbody2D>();
    }

    protected virtual void Start()
    {
        _btRoot = ConfigureBehaviorTree();
        MobManager.Instance.Register(this);
    }

    protected virtual void Update()
    {
        if (!IsAlive || RuntimeData.IsStaggered) return;
        _btRoot?.Execute();
    }

    /// <summary>
    /// 파생 클래스에서 몹 유형별 BT 트리를 구성하여 반환.
    /// </summary>
    protected abstract BTNode ConfigureBehaviorTree();

    protected override float CalculateIncomingDamage(float rawDamage)
    {
        return DamageCalculator.CalcDamageTaken(rawDamage, Stats.TotalDef);
    }

    protected override void OnDeath()
    {
        RuntimeData.CurrentState = MonsterState.Death;
        AnimCtrl.PlayDeath();
        GetComponent<MonsterLootDropper>().DropLoot();
        MobManager.Instance.HandleMonsterDeath(this);
    }

    protected virtual void OnDestroy()
    {
        base.OnDestroy();
        MobManager.Instance?.Unregister(this);
    }
}
```

### 4.2 Behavior Tree 노드 구조

경량 BT 구현 — 코루틴/GC 제로, 매 프레임 `Execute()` 호출.

```csharp
/// <summary>BT 노드 실행 결과</summary>
public enum BTStatus { Success, Failure, Running }

/// <summary>BT 노드 추상 기반</summary>
public abstract class BTNode
{
    public abstract BTStatus Execute();
}

/// <summary>자식을 왼쪽부터 순서대로 실행. 실패 시 즉시 중단.</summary>
public class BTSequence : BTNode { /* children 순회 */ }

/// <summary>자식 중 하나라도 성공하면 즉시 반환.</summary>
public class BTSelector : BTNode { /* children 순회 */ }

/// <summary>조건 판별 노드 (람다 기반).</summary>
public class BTCondition : BTNode
{
    private readonly System.Func<bool> _condition;
    public BTCondition(System.Func<bool> condition) => _condition = condition;
    public override BTStatus Execute() => _condition() ? BTStatus.Success : BTStatus.Failure;
}

/// <summary>실행 노드 (람다 기반).</summary>
public class BTAction : BTNode
{
    private readonly System.Func<BTStatus> _action;
    public BTAction(System.Func<BTStatus> action) => _action = action;
    public override BTStatus Execute() => _action();
}
```

### 4.3 HostileMonster (적대적 몹)

```csharp
/// <summary>
/// 적대적 몹: 감지 → 추격 → 공격 범위 진입 → 범위 표시 + 딜레이 → 공격
/// </summary>
public class HostileMonster : MonsterBase
{
    private DetectionSystem _detection;
    private MonsterNavigator _navigator;

    protected override void Awake()
    {
        base.Awake();
        _detection = GetComponent<DetectionSystem>();
        _navigator = GetComponent<MonsterNavigator>();
    }

    protected override BTNode ConfigureBehaviorTree()
    {
        return new BTSelector(new BTNode[]
        {
            // 우선순위 1: 사망
            new BTCondition(() => !IsAlive),

            // 우선순위 2: 피격 경직
            new BTCondition(() => RuntimeData.IsStaggered),

            // 우선순위 3: 공격 범위 내 → 공격 시퀀스
            new BTSequence(new BTNode[]
            {
                new BTCondition(() => Attack.IsPlayerInAttackRange()),
                new BTAction(() => Attack.ExecuteAttack())
            }),

            // 우선순위 4: 감지됨 → 추격
            new BTSequence(new BTNode[]
            {
                new BTCondition(() => _detection.HasTarget),
                new BTAction(() =>
                {
                    RuntimeData.CurrentState = MonsterState.Chase;
                    _navigator.MoveToward(RuntimeData.DetectedPlayer.position);
                    AnimCtrl.PlayWalk();
                    return BTStatus.Running;
                })
            }),

            // 우선순위 5: 비감지 → 자유 배회
            new BTAction(() =>
            {
                RuntimeData.CurrentState = MonsterState.Wander;
                GetComponent<WanderSystem>().Wander();
                AnimCtrl.PlayWalk();
                return BTStatus.Running;
            })
        });
    }
}
```

### 4.4 NeutralMonster (중립 몹)

```csharp
/// <summary>
/// 중립 몹: 감지 → 도망 (감지범위 × 1.5 벗어나면 돌아감) → 비감지 시 배회
/// </summary>
public class NeutralMonster : MonsterBase
{
    private DetectionSystem _detection;
    private MonsterNavigator _navigator;
    private static readonly float FleeMultiplier = 1.5f;

    protected override BTNode ConfigureBehaviorTree()
    {
        return new BTSelector(new BTNode[]
        {
            new BTCondition(() => !IsAlive),
            new BTCondition(() => RuntimeData.IsStaggered),

            // 감지됨 → 도망
            new BTSequence(new BTNode[]
            {
                new BTCondition(() => _detection.HasTarget),
                new BTAction(() =>
                {
                    RuntimeData.CurrentState = MonsterState.Flee;
                    Vector2 fleeDir = ((Vector2)transform.position
                                     - (Vector2)RuntimeData.DetectedPlayer.position).normalized;
                    _navigator.MoveInDirection(fleeDir);
                    AnimCtrl.PlayWalk();

                    // 감지범위 × 1.5 벗어났으면 도망 종료
                    float dist = Vector2.Distance(transform.position,
                                                  RuntimeData.DetectedPlayer.position);
                    if (dist > RuntimeData.Data.DetectionRadius * FleeMultiplier)
                    {
                        _detection.ForceRelease();
                    }
                    return BTStatus.Running;
                })
            }),

            // 비감지 → 배회
            new BTAction(() =>
            {
                RuntimeData.CurrentState = MonsterState.Wander;
                GetComponent<WanderSystem>().Wander();
                AnimCtrl.PlayWalk();
                return BTStatus.Running;
            })
        });
    }
}
```

### 4.5 BossMonster (보스 몹)

```csharp
/// <summary>
/// 보스 몹: 감지범위 없음. 활성화 즉시 무한 추격 + 공격.
/// 보스 또는 플레이어 사망 시까지 추격을 멈추지 않음.
/// </summary>
public class BossMonster : MonsterBase
{
    protected override void Start()
    {
        base.Start();
        // 보스는 활성화 즉시 플레이어를 감지
        RuntimeData.DetectedPlayer = FindPlayerTransform();
    }

    protected override BTNode ConfigureBehaviorTree()
    {
        return new BTSelector(new BTNode[]
        {
            new BTCondition(() => !IsAlive),
            new BTCondition(() => RuntimeData.IsStaggered),

            // 공격 범위 내 → 공격
            new BTSequence(new BTNode[]
            {
                new BTCondition(() => Attack.IsPlayerInAttackRange()),
                new BTAction(() => Attack.ExecuteAttack())
            }),

            // 항상 추격 (감지범위 무시)
            new BTAction(() =>
            {
                RuntimeData.CurrentState = MonsterState.Chase;
                var nav = GetComponent<MonsterNavigator>();
                if (RuntimeData.DetectedPlayer != null)
                    nav.MoveToward(RuntimeData.DetectedPlayer.position);
                AnimCtrl.PlayWalk();
                return BTStatus.Running;
            })
        });
    }

    private Transform FindPlayerTransform()
    {
        GameObject player = GameObject.FindGameObjectWithTag("Player");
        return player != null ? player.transform : null;
    }
}
```

### 4.6 MonsterAnimatorController

```csharp
/// <summary>
/// 애니메이션 상태 전환 전담 (SRP).
/// MonsterData.AnimController로 Animator를 동적 할당.
/// Animator Parameter: "State" (int), "Attack" (trigger), "Death" (trigger)
/// </summary>
public sealed class MonsterAnimatorController : MonoBehaviour
{
    private Animator _animator;
    private static readonly int HashState   = Animator.StringToHash("State");
    private static readonly int HashAttack  = Animator.StringToHash("Attack");
    private static readonly int HashDeath   = Animator.StringToHash("Death");
    private static readonly int HashHit     = Animator.StringToHash("Hit");

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    public void Initialize(RuntimeAnimatorController controller)
    {
        if (_animator != null && controller != null)
            _animator.runtimeAnimatorController = controller;
    }

    public void PlayIdle()   => _animator?.SetInteger(HashState, 0);
    public void PlayWalk()   => _animator?.SetInteger(HashState, 1);
    public void PlayAttack() => _animator?.SetTrigger(HashAttack);
    public void PlayDeath()  => _animator?.SetTrigger(HashDeath);
    public void PlayHit()    => _animator?.SetTrigger(HashHit);
}
```

---

## 5. 피격 경직 시스템 (HitStagger)

> **요구사항 4**: 에너미 피격시 0.1초간 움직임 멈춤

### 5.1 인터페이스

```csharp
/// <summary>피격 시 경직 가능한 객체의 인터페이스 (ISP)</summary>
public interface IStaggerable
{
    bool IsStaggered { get; }
    void ApplyStagger(float duration);
}
```

### 5.2 HitStaggerHandler

```csharp
/// <summary>
/// 피격 시 0.1초 경직을 전담하는 컴포넌트 (SRP).
/// HealthSystem.OnHit 이벤트를 구독하여 자동 발동.
/// 타이머 기반 → 코루틴 없이 GC 제로.
/// </summary>
public sealed class HitStaggerHandler : MonoBehaviour, IStaggerable
{
    [SerializeField] private float _staggerDuration = 0.1f;

    private MonsterRuntimeData _runtimeData;
    private HealthSystem       _healthSystem;
    private float              _staggerTimer;

    public bool IsStaggered => _runtimeData != null && _runtimeData.IsStaggered;

    private void Awake()
    {
        _runtimeData = GetComponent<MonsterRuntimeData>();
        _healthSystem = GetComponent<HealthSystem>();
    }

    private void OnEnable()
    {
        if (_healthSystem != null)
            _healthSystem.OnHit += OnHit;
    }

    private void OnDisable()
    {
        if (_healthSystem != null)
            _healthSystem.OnHit -= OnHit;
    }

    private void Update()
    {
        if (!_runtimeData.IsStaggered) return;

        _staggerTimer -= Time.deltaTime;
        if (_staggerTimer <= 0f)
        {
            _runtimeData.IsStaggered = false;
            _runtimeData.CurrentState = MonsterState.Idle; // BT가 다음 프레임에 재판단
        }
    }

    private void OnHit()
    {
        ApplyStagger(_staggerDuration);
    }

    public void ApplyStagger(float duration)
    {
        _runtimeData.IsStaggered = true;
        _runtimeData.CurrentState = MonsterState.Stagger;
        _staggerTimer = duration;

        // 즉시 이동 정지
        var rb = GetComponent<Rigidbody2D>();
        if (rb != null) rb.linearVelocity = Vector2.zero;

        // 피격 애니메이션
        GetComponent<MonsterAnimatorController>()?.PlayHit();
    }
}
```

---

## 6. 감지 시스템 (DetectionSystem)

> **요구사항 5**: 감지 범위 + 장애물 차단 (LoS 레이캐스트)

### 6.1 설계

```csharp
/// <summary>
/// 감지 범위를 기준으로 플레이어를 탐지.
/// 장애물(Obstacle/Wall 레이어)에 가려져 있으면 감지 불가 (Raycast2D).
/// 적대적 몹: 감지 범위 밖 또는 시야 차단 4초 이상 → 추격 해제.
/// 중립 몹: 감지 범위 × 1.5 벗어나면 도망 해제.
/// </summary>
public sealed class DetectionSystem : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private LayerMask _obstacleMask;  // "Wall" + "Obstacle" 레이어
    [SerializeField] private LayerMask _playerMask;    // "Player" 레이어
    [Tooltip("Raycast 체크 주기 (초). 매 프레임 대신 주기적 체크로 성능 최적화")]
    [SerializeField] private float _checkInterval = 0.15f;

    private MonsterRuntimeData _runtime;
    private float              _checkTimer;

    // ── LoS 차단 타이머 (적대적 몹용) ──
    [Tooltip("시야가 차단된 후 추격을 해제하기까지의 시간 (초)")]
    [SerializeField] private float _losBreakDuration = 4f;
    private float _losBlockedTimer;

    public bool      HasTarget      => _runtime.DetectedPlayer != null;
    public Transform DetectedTarget => _runtime.DetectedPlayer;

    private void Awake()
    {
        _runtime = GetComponent<MonsterRuntimeData>();
    }

    private void Update()
    {
        _checkTimer += Time.deltaTime;
        if (_checkTimer < _checkInterval) return;
        _checkTimer = 0f;

        PerformDetection();
    }

    private void PerformDetection()
    {
        float radius = _runtime.Data.DetectionRadius;

        // 1. 범위 내 플레이어 검출 (OverlapCircle, 가비지 없는 배열 오버로드)
        Collider2D hit = Physics2D.OverlapCircle(
            transform.position, radius, _playerMask);

        if (hit == null)
        {
            // 범위 밖 → 적대적 몹은 타이머 카운트
            HandleOutOfRange();
            return;
        }

        // 2. LoS (Line of Sight) 체크 — 장애물 레이캐스트
        Vector2 direction = (hit.transform.position - transform.position);
        float distance = direction.magnitude;

        RaycastHit2D losHit = Physics2D.Raycast(
            transform.position, direction.normalized, distance, _obstacleMask);

        if (losHit.collider != null)
        {
            // 장애물에 가려짐 → 적대적 몹은 타이머 카운트
            HandleLineOfSightBlocked();
            return;
        }

        // 3. 감지 성공
        _runtime.DetectedPlayer = hit.transform;
        _runtime.LoseAggroTimer = 0f;
        _losBlockedTimer = 0f;
    }

    private void HandleOutOfRange()
    {
        if (_runtime.DetectedPlayer == null) return;

        if (_runtime.Type == MonsterType.Hostile)
        {
            _runtime.LoseAggroTimer += _checkInterval;
            if (_runtime.LoseAggroTimer >= _losBreakDuration)
                ForceRelease();
        }
        else if (_runtime.Type == MonsterType.Neutral)
        {
            // 중립은 상위 BT에서 × 1.5 거리 체크로 해제
        }
        // 보스는 감지 해제하지 않음
    }

    private void HandleLineOfSightBlocked()
    {
        if (_runtime.DetectedPlayer == null) return;

        if (_runtime.Type == MonsterType.Hostile)
        {
            _losBlockedTimer += _checkInterval;
            if (_losBlockedTimer >= _losBreakDuration)
                ForceRelease();
        }
    }

    /// <summary>감지 강제 해제</summary>
    public void ForceRelease()
    {
        _runtime.DetectedPlayer = null;
        _runtime.LoseAggroTimer = 0f;
        _losBlockedTimer = 0f;
    }
}
```

### 6.2 성능 고려사항

| 기법 | 설명 |
|------|------|
| **주기적 체크** | `_checkInterval = 0.15초` → 매 프레임이 아닌 약 6.7fps로 체크 |
| **OverlapCircle 단일 호출** | `Collider2D` 단일 반환 오버로드 사용 (플레이어 1명이므로 충분) |
| **Raycast2D 단일 호출** | 장애물 확인용 최소 1회 |

---

## 7. 첫 처치 보너스 시스템 (FirstKillBonus)

> **요구사항 6**: 처음 잡는 몹 → 플레이어 스탯 영구 상승

### 7.1 FirstKillRegistry (기록 관리)

```csharp
/// <summary>
/// 플레이어가 처치한 몹의 ID를 기록하는 레지스트리 (SRP).
/// MobManager의 하위 컴포넌트로 운영.
/// </summary>
public sealed class FirstKillRegistry : MonoBehaviour
{
    private HashSet<int> _killedMonsterIds = new HashSet<int>();

    /// <summary>이 몹을 처음 잡는 것인지 확인</summary>
    public bool IsFirstKill(int monsterId) => !_killedMonsterIds.Contains(monsterId);

    /// <summary>처치 기록 등록. 첫 처치이면 true 반환.</summary>
    public bool RecordKill(int monsterId)
    {
        return _killedMonsterIds.Add(monsterId); // Add는 이미 있으면 false
    }

    /// <summary>전체 기록 초기화 (디버그용)</summary>
    public void ResetAll() => _killedMonsterIds.Clear();
}
```

### 7.2 FirstKillBonusApplier

```csharp
/// <summary>
/// 첫 처치 시 플레이어 스탯 영구 증가를 처리 (SRP).
/// MobManager.HandleMonsterDeath()에서 호출.
/// </summary>
public static class FirstKillBonusApplier
{
    // 일반 몹 첫 처치 보너스
    private static readonly float NormalHP  = 5f;
    private static readonly float NormalATK = 3f;
    private static readonly float NormalMAG = 3f;

    // 보스 몹 첫 처치 보너스
    private static readonly float BossHP  = 20f;
    private static readonly float BossATK = 10f;
    private static readonly float BossMAG = 10f;

    /// <summary>
    /// 첫 처치 보너스를 플레이어에게 적용.
    /// StatSystem의 기본 스탯을 영구적으로 증가시킵니다.
    /// </summary>
    public static void Apply(MonsterType type, PlayerEntity player)
    {
        StatSystem stats = player.Stats;
        bool isBoss = (type == MonsterType.Boss);

        float hpBonus  = isBoss ? BossHP  : NormalHP;
        float atkBonus = isBoss ? BossATK : NormalATK;
        float magBonus = isBoss ? BossMAG : NormalMAG;

        // StatSystem에 영구 보너스 메서드 추가 필요
        // (또는 IBonusProvider 기반 영구 버프 오브젝트 등록)
        stats.AddPermanentBonus(hpBonus, atkBonus, magBonus);
    }
}
```

### 7.3 StatSystem 확장 (영구 보너스)

기존 `StatSystem`에 영구 보너스 필드를 추가:

```csharp
// StatSystem.cs 에 추가할 메서드
private float _permMaxHP;
private float _permAtk;
private float _permMagicAtk;

/// <summary>첫 처치 보너스 등 영구 스탯 증가</summary>
public void AddPermanentBonus(float hp, float atk, float magicAtk)
{
    _permMaxHP    += hp;
    _permAtk      += atk;
    _permMagicAtk += magicAtk;
    OnStatsChanged?.Invoke();
}

// TotalAtk getter 수정: return _baseAtk + _permAtk + (providers 합산);
// TotalMaxHP getter 수정: return _baseMaxHP + _permMaxHP + (providers 합산);
// TotalMagicAtk getter 수정: return _baseMagicAtk + _permMagicAtk + (providers 합산);
```

---

## 8. 행동 및 네비게이션 시스템

> **요구사항 7**: 적대적=추격, 중립=도망 + 성능 최적화 네비게이션

### 8.1 MonsterNavigator (경량 네비게이션)

```csharp
/// <summary>
/// 경량 2D 네비게이션 시스템 (SRP).
/// NavMesh 대신 Rigidbody2D.MovePosition + 간단한 장애물 회피를 사용.
/// 성능 최적화: Raycast 기반 전방 장애물 회피 (A* 없이).
///
/// 알고리즘:
/// 1. 목표 방향으로 직선 이동 (Rigidbody2D.MovePosition)
/// 2. 전방 Raycast로 장애물 감지 시, 좌/우 방향으로 우회
/// 3. 우회 중에도 목표 방향을 점진적으로 복원 (Steering Behavior)
/// </summary>
public sealed class MonsterNavigator : MonoBehaviour
{
    [Header("Obstacle Avoidance")]
    [SerializeField] private LayerMask _obstacleMask;
    [SerializeField] private float     _avoidanceRayLength = 1.5f;
    [SerializeField] private float     _avoidanceAngle     = 45f;

    private Rigidbody2D        _rb;
    private MonsterRuntimeData _runtime;
    private Vector2            _currentDirection;

    private void Awake()
    {
        _rb      = GetComponent<Rigidbody2D>();
        _runtime = GetComponent<MonsterRuntimeData>();
    }

    /// <summary>목표 위치를 향해 이동 (적대적 추격용)</summary>
    public void MoveToward(Vector2 targetPosition)
    {
        Vector2 desiredDir = ((Vector2)targetPosition - (Vector2)transform.position).normalized;
        Vector2 finalDir = ApplyAvoidance(desiredDir);

        _rb.MovePosition(_rb.position + finalDir * _runtime.CurrentSpeed * Time.deltaTime);

        // 스프라이트 방향 전환
        FlipSprite(finalDir.x);
    }

    /// <summary>지정 방향으로 이동 (중립 도망용)</summary>
    public void MoveInDirection(Vector2 direction)
    {
        Vector2 finalDir = ApplyAvoidance(direction.normalized);
        _rb.MovePosition(_rb.position + finalDir * _runtime.CurrentSpeed * Time.deltaTime);
        FlipSprite(finalDir.x);
    }

    /// <summary>이동 즉시 정지</summary>
    public void Stop()
    {
        _rb.linearVelocity = Vector2.zero;
    }

    /// <summary>
    /// 전방 Raycast로 장애물 회피.
    /// 성능: Raycast 최대 3회 (정면 + 좌 + 우)
    /// </summary>
    private Vector2 ApplyAvoidance(Vector2 desiredDir)
    {
        // 정면 체크
        if (!Physics2D.Raycast(transform.position, desiredDir,
                               _avoidanceRayLength, _obstacleMask))
        {
            return desiredDir; // 장애물 없음 → 직진
        }

        // 좌측 우회
        Vector2 leftDir = Quaternion.Euler(0, 0, _avoidanceAngle) * desiredDir;
        if (!Physics2D.Raycast(transform.position, leftDir,
                               _avoidanceRayLength, _obstacleMask))
        {
            return leftDir;
        }

        // 우측 우회
        Vector2 rightDir = Quaternion.Euler(0, 0, -_avoidanceAngle) * desiredDir;
        if (!Physics2D.Raycast(transform.position, rightDir,
                               _avoidanceRayLength, _obstacleMask))
        {
            return rightDir;
        }

        // 모두 막힘 → 정지
        return Vector2.zero;
    }

    private void FlipSprite(float dirX)
    {
        if (Mathf.Abs(dirX) < 0.01f) return;
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.flipX = dirX < 0;
    }
}
```

### 8.2 네비게이션 성능 비교

| 방식 | 연산 비용 | 경로 품질 | 채택 |
|------|-----------|-----------|------|
| **A\*** | O(n log n), 매 프레임 불가 | 최적 | ❌ |
| **NavMesh2D** | 패키지 의존, 3D 변환 필요 | 양호 | ❌ |
| **Steering + Raycast** | O(1)~O(3) Raycast | 실용적 | ✅ |

> 선택 이유: 2D 탑다운 게임에서 몹 수가 많을 때 Raycast 3회가 A*보다 압도적으로 가벼움.

---

## 9. 비감지 배회 시스템 (WanderSystem)

> **요구사항 8**: 감지 전 자연스러운 맵 이동

```csharp
/// <summary>
/// 감지 전 몹의 자유 배회 전담 (SRP).
/// 일정 시간 간격으로 랜덤 방향을 선택하고 이동.
/// 타이머 기반 → 코루틴/GC 제로.
/// </summary>
public sealed class WanderSystem : MonoBehaviour
{
    [Header("Wander Settings")]
    [Tooltip("방향 전환 최소 간격 (초)")]
    [SerializeField] private float _minInterval = 2f;
    [Tooltip("방향 전환 최대 간격 (초)")]
    [SerializeField] private float _maxInterval = 5f;
    [Tooltip("한 번 이동 시 최대 거리 (Unity 단위)")]
    [SerializeField] private float _wanderRadius = 3f;
    [Tooltip("대기(Idle) 확률 (0~1). 0.3이면 30% 확률로 잠시 멈춤")]
    [Range(0f, 1f)]
    [SerializeField] private float _idleChance = 0.3f;

    private MonsterNavigator   _navigator;
    private MonsterRuntimeData _runtime;
    private MonsterAnimatorController _animCtrl;
    private float              _timer;
    private float              _currentInterval;
    private bool               _isIdling;

    private void Awake()
    {
        _navigator = GetComponent<MonsterNavigator>();
        _runtime   = GetComponent<MonsterRuntimeData>();
        _animCtrl  = GetComponent<MonsterAnimatorController>();
        ResetInterval();
    }

    /// <summary>BT에서 매 프레임 호출. 타이머 만료 시 새 방향 결정.</summary>
    public void Wander()
    {
        _timer += Time.deltaTime;

        if (_timer >= _currentInterval)
        {
            _timer = 0f;
            ResetInterval();
            ChooseNewBehavior();
        }

        if (!_isIdling)
        {
            // MonsterNavigator가 현재 방향으로 계속 이동 처리
        }
    }

    private void ChooseNewBehavior()
    {
        // 확률적으로 대기 or 이동
        if (Random.value < _idleChance)
        {
            _isIdling = true;
            _navigator.Stop();
            _animCtrl.PlayIdle();
        }
        else
        {
            _isIdling = false;
            Vector2 randomDir = Random.insideUnitCircle.normalized;
            _navigator.MoveInDirection(randomDir);
            _animCtrl.PlayWalk();
        }
    }

    private void ResetInterval()
    {
        _currentInterval = Random.Range(_minInterval, _maxInterval);
    }
}
```

---

## 10. 오브젝트 풀링 및 스폰 시스템

> **요구사항 9**: 기존 `SimpleObjectPool`을 활용한 몹 풀링 + 랜덤 생성

### 10.1 MobSpawner

```csharp
/// <summary>
/// 맵 청크 기반 몹 스폰 시스템 (SRP).
/// 기존 MapGenerator의 청크 시스템과 연동.
/// SimpleObjectPool을 사용하여 풀링 관리.
/// </summary>
public sealed class MobSpawner : MonoBehaviour
{
    [Header("Spawn Settings")]
    [Tooltip("스폰할 몹 데이터 목록 (바이옴별로 다르게 설정 가능)")]
    [SerializeField] private SpawnEntry[] _spawnTable;

    [Tooltip("청크당 최대 동시 활성 몹 수")]
    [SerializeField] private int _maxPerChunk = 5;

    [Tooltip("스폰 체크 간격 (초)")]
    [SerializeField] private float _spawnInterval = 5f;

    [Tooltip("플레이어로부터 스폰금지 최소 거리")]
    [SerializeField] private float _minDistFromPlayer = 5f;

    [Tooltip("플레이어로부터 스폰 최대 거리")]
    [SerializeField] private float _maxDistFromPlayer = 20f;

    private Transform _player;

    /// <summary>몹을 풀에서 꺼내 스폰</summary>
    public MonsterBase SpawnMonster(MonsterData data, Vector3 position)
    {
        if (data.MonsterPrefab == null) return null;

        GameObject obj = SimpleObjectPool.Instance.Get(
            data.MonsterPrefab, position, Quaternion.identity);

        MonsterBase monster = obj.GetComponent<MonsterBase>();
        if (monster != null)
        {
            monster.GetComponent<MonsterRuntimeData>().Initialize(data);
            monster.GetComponent<MonsterAnimatorController>()
                   .Initialize(data.AnimController);
            monster.Health.SetMaxHp(data.MaxHP, refill: true);
        }
        return monster;
    }

    /// <summary>몹을 풀에 반환 (사망 또는 청크 비활성화)</summary>
    public void DespawnMonster(MonsterBase monster)
    {
        SimpleObjectPool.Instance.Release(monster.gameObject);
    }
}

/// <summary>스폰 테이블 엔트리</summary>
[System.Serializable]
public struct SpawnEntry
{
    public MonsterData Data;
    [Range(0f, 1f)]
    public float SpawnWeight;  // 상대적 스폰 확률 가중치
}
```

### 10.2 스폰 위치 결정

```
스폰 위치 결정 알고리즘:

1. 플레이어 주변 _minDistFromPlayer ~ _maxDistFromPlayer 범위 내 랜덤 위치 선정
2. Physics2D.OverlapCircle로 장애물/벽 겹침 체크 (반경 0.5)
3. 겹치면 최대 5회 재시도
4. 성공 시 SpawnMonster() 호출
5. 실패 시 이번 주기 스킵
```

---

## 11. MobManager (전역 관리)

> **요구사항 10**: 필드의 모든 몹을 관리하는 매니저

```csharp
/// <summary>
/// 전역 몹 매니저 (싱글톤).
/// 모든 활성 몹을 추적하고, 일괄 조작 API를 제공.
/// </summary>
public sealed class MobManager : MonoBehaviour
{
    // ── 싱글톤 ────────────────────────────────────────────────
    private static MobManager _instance;
    public static MobManager Instance => _instance;

    // ── 하위 시스템 ──────────────────────────────────────────
    public MobSpawner       Spawner       { get; private set; }
    public FirstKillRegistry KillRegistry { get; private set; }

    // ── 몹 추적 ──────────────────────────────────────────────
    private readonly List<MonsterBase> _allMonsters = new List<MonsterBase>();
    private readonly Dictionary<int, List<MonsterBase>> _monstersByType
        = new Dictionary<int, List<MonsterBase>>();

    public IReadOnlyList<MonsterBase> AllMonsters => _allMonsters;

    private void Awake()
    {
        if (_instance != null && _instance != this) { Destroy(gameObject); return; }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        Spawner      = GetComponentInChildren<MobSpawner>();
        KillRegistry = GetComponentInChildren<FirstKillRegistry>();
    }

    // ── 등록/해제 ─────────────────────────────────────────────

    public void Register(MonsterBase monster)
    {
        if (!_allMonsters.Contains(monster))
        {
            _allMonsters.Add(monster);
            int id = monster.RuntimeData.MonsterId;
            if (!_monstersByType.ContainsKey(id))
                _monstersByType[id] = new List<MonsterBase>();
            _monstersByType[id].Add(monster);
        }
    }

    public void Unregister(MonsterBase monster)
    {
        _allMonsters.Remove(monster);
        int id = monster.RuntimeData.MonsterId;
        if (_monstersByType.ContainsKey(id))
            _monstersByType[id].Remove(monster);
    }

    // ── 사망 처리 ─────────────────────────────────────────────

    public void HandleMonsterDeath(MonsterBase monster)
    {
        int id = monster.RuntimeData.MonsterId;

        // 첫 처치 보너스 확인 및 적용
        if (KillRegistry.RecordKill(id))
        {
            PlayerEntity player = FindFirstObjectByType<PlayerEntity>();
            if (player != null)
                FirstKillBonusApplier.Apply(monster.RuntimeData.Type, player);
        }

        // 풀 반환 (약간의 딜레이로 사망 애니메이션 재생 시간 확보)
        StartCoroutine(DelayedDespawn(monster, 1.5f));
    }

    private System.Collections.IEnumerator DelayedDespawn(MonsterBase monster, float delay)
    {
        yield return new WaitForSeconds(delay);
        Spawner.DespawnMonster(monster);
    }

    // ── 일괄 조작 API ─────────────────────────────────────────

    /// <summary>모든 몹 일시 정지</summary>
    public void PauseAll()
    {
        for (int i = 0; i < _allMonsters.Count; i++)
            _allMonsters[i].enabled = false;
    }

    /// <summary>모든 몹 재개</summary>
    public void ResumeAll()
    {
        for (int i = 0; i < _allMonsters.Count; i++)
            _allMonsters[i].enabled = true;
    }

    /// <summary>특정 ID의 몹만 일시 정지</summary>
    public void PauseByType(int monsterId)
    {
        if (!_monstersByType.ContainsKey(monsterId)) return;
        var list = _monstersByType[monsterId];
        for (int i = 0; i < list.Count; i++)
            list[i].enabled = false;
    }

    /// <summary>특정 ID의 몹만 재개</summary>
    public void ResumeByType(int monsterId)
    {
        if (!_monstersByType.ContainsKey(monsterId)) return;
        var list = _monstersByType[monsterId];
        for (int i = 0; i < list.Count; i++)
            list[i].enabled = true;
    }

    /// <summary>모든 몹 즉시 제거 (풀 반환)</summary>
    public void DespawnAll()
    {
        for (int i = _allMonsters.Count - 1; i >= 0; i--)
            Spawner.DespawnMonster(_allMonsters[i]);
        _allMonsters.Clear();
        _monstersByType.Clear();
    }

    /// <summary>특정 ID의 몹 모두 제거</summary>
    public void DespawnByType(int monsterId)
    {
        if (!_monstersByType.ContainsKey(monsterId)) return;
        var list = _monstersByType[monsterId];
        for (int i = list.Count - 1; i >= 0; i--)
        {
            _allMonsters.Remove(list[i]);
            Spawner.DespawnMonster(list[i]);
        }
        list.Clear();
    }

    /// <summary>현재 활성 몹 수 반환</summary>
    public int GetActiveCount() => _allMonsters.Count;

    /// <summary>특정 ID의 활성 몹 수 반환</summary>
    public int GetActiveCountByType(int monsterId)
    {
        return _monstersByType.ContainsKey(monsterId)
            ? _monstersByType[monsterId].Count : 0;
    }
}
```

---

## 12. 아이템 드롭 및 획득 시스템

> **요구사항 11**: 기존 인벤토리 시스템과 연동

### 12.1 기존 인벤토리 시스템 분석

```
현재 구조:
  ItemData (SO) ← 정적 데이터
  Item          ← 인스턴스 (Data + Tier/Level/Count)
  InventorySlot ← UI 슬롯 (Drag&Drop)
  InventoryGenerator ← 슬롯 생성 + 초기 아이템 지급
  DataManager   ← 아이템/스킬 DB 캐싱 (싱글톤)

기존 드롭 시스템:
  ItemDropSpawner    → HealthSystem.OnHit 이벤트 → 프리팹 Instantiate
  FloatingMagneticItem → Dropping → Floating → Magnetic → CollectItem()
  ※ CollectItem()에 인벤토리 연결 로직이 TODO 상태
```

### 12.2 MonsterLootDropper (몹 사망 드롭)

```csharp
/// <summary>
/// 몹 사망 시 MonsterData.DropTable 기반 아이템 드롭 전담 (SRP).
/// 기존 ItemDropSpawner 패턴을 따르되, DropEntry의 확률/수량 시스템 적용.
/// </summary>
public sealed class MonsterLootDropper : MonoBehaviour
{
    [Header("Drop Physics")]
    [SerializeField] private float _minJumpForce = 3f;
    [SerializeField] private float _maxJumpForce = 5f;
    [SerializeField] private float _spreadAngle  = 45f;

    [Header("Drop Item Prefab")]
    [Tooltip("FloatingMagneticItem 컴포넌트가 부착된 드롭 아이템 프리팹")]
    [SerializeField] private GameObject _dropItemPrefab;

    private MonsterRuntimeData _runtime;

    private void Awake()
    {
        _runtime = GetComponent<MonsterRuntimeData>();
    }

    /// <summary>드롭 테이블에 따라 아이템 투하</summary>
    public void DropLoot()
    {
        if (_runtime.Data.DropTable == null) return;

        DropEntry[] table = _runtime.Data.DropTable;
        for (int i = 0; i < table.Length; i++)
        {
            ref DropEntry entry = ref table[i];
            if (entry.Item == null) continue;

            // 1. 드롭 여부 판정
            if (Random.value > entry.DropChance) continue;

            // 2. 드롭 수량 결정 (가중치 기반)
            int count = DetermineDropCount(entry);

            // 3. 아이템 스폰
            for (int j = 0; j < count; j++)
            {
                SpawnDropItem(entry.Item);
            }
        }
    }

    private int DetermineDropCount(DropEntry entry)
    {
        if (entry.CountWeights == null || entry.CountWeights.Length == 0)
            return 1;

        // 가중치 합산
        float totalWeight = 0f;
        int maxIndex = Mathf.Min(entry.CountWeights.Length, entry.MaxCount);
        for (int i = 0; i < maxIndex; i++)
            totalWeight += entry.CountWeights[i];

        // 룰렛 셀렉션
        float roll = Random.value * totalWeight;
        float cumulative = 0f;
        for (int i = 0; i < maxIndex; i++)
        {
            cumulative += entry.CountWeights[i];
            if (roll <= cumulative)
                return i + 1; // 인덱스 0 = 1개
        }
        return 1;
    }

    private void SpawnDropItem(ItemData itemData)
    {
        if (_dropItemPrefab == null) return;

        GameObject obj = SimpleObjectPool.Instance.Get(
            _dropItemPrefab, transform.position, Quaternion.identity);

        // FloatingMagneticItem에 초기 속도 주입
        FloatingMagneticItem drop = obj.GetComponent<FloatingMagneticItem>();
        if (drop != null)
        {
            float angle = Random.Range(-_spreadAngle, _spreadAngle);
            Vector2 dir = Quaternion.Euler(0, 0, angle) * Vector2.up;
            dir *= Random.Range(_minJumpForce, _maxJumpForce);
            drop.InitDrop(dir);
        }

        // 드롭 아이템에 ItemData 참조를 주입
        DroppedItemIdentity identity = obj.GetComponent<DroppedItemIdentity>();
        if (identity != null)
            identity.SetItem(itemData, 1);
    }
}
```

### 12.3 DroppedItemIdentity (드롭 아이템 식별)

```csharp
/// <summary>
/// 필드에 떨어진 아이템이 어떤 ItemData인지 기억하는 컴포넌트 (SRP).
/// FloatingMagneticItem 프리팹에 함께 부착.
/// </summary>
public sealed class DroppedItemIdentity : MonoBehaviour
{
    public ItemData ItemData  { get; private set; }
    public int      ItemCount { get; private set; }

    public void SetItem(ItemData data, int count)
    {
        ItemData  = data;
        ItemCount = count;

        // 스프라이트 업데이트
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null && data != null && data.Icon != null)
            sr.sprite = data.Icon;
    }
}
```

### 12.4 FloatingMagneticItem 확장 (인벤토리 연동)

기존 `FloatingMagneticItem.CollectItem()`의 TODO를 채워 인벤토리에 아이템 추가:

```csharp
// FloatingMagneticItem.cs 의 CollectItem() 수정
private void CollectItem()
{
    // DroppedItemIdentity에서 아이템 정보 획득
    DroppedItemIdentity identity = GetComponent<DroppedItemIdentity>();
    if (identity != null && identity.ItemData != null)
    {
        // 인벤토리에 아이템 추가
        InventoryManager.Instance.AddItem(identity.ItemData, identity.ItemCount);
    }

    // 풀 반환 (Destroy 대신)
    SimpleObjectPool.Instance.Release(gameObject);
}
```

### 12.5 InventoryManager (신규 — 인벤토리 중앙 관리)

기존 `InventoryGenerator`에 아이템 추가 API가 없으므로, 새로운 매니저를 추가:

```csharp
/// <summary>
/// 인벤토리 아이템 추가/제거를 전담하는 매니저 (SRP).
/// 기존 InventoryGenerator가 생성한 슬롯 리스트를 참조.
/// </summary>
public sealed class InventoryManager : MonoBehaviour
{
    public static InventoryManager Instance { get; private set; }

    [SerializeField] private InventoryGenerator _inventoryGenerator;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>비어있는 슬롯에 아이템 추가. 같은 아이템이 있으면 수량 증가.</summary>
    public bool AddItem(ItemData data, int count = 1)
    {
        // 1. 같은 아이템이 이미 있으면 수량 누적 (재료 아이템 등)
        // 2. 없으면 빈 슬롯에 배치
        // 3. 인벤토리가 꽉 찼으면 false 반환
        // (InventoryGenerator의 allSlots 리스트 접근 필요 → public 또는 메서드 제공)
        return true; // 구현 상세는 기존 슬롯 시스템에 맞춰 조정
    }
}
```

---

## 13. 공격 범위 시각화 시스템

> **요구사항 3 + Monster System.md 몹 공격 로직**: 공격 범위 표시 → 딜레이 → 공격 판정

### 13.1 MonsterAttackHandler

```csharp
/// <summary>
/// 몹의 공격 로직 전담 (SRP).
/// 1. 공격 범위 내 플레이어 확인 (IsPlayerInAttackRange)
/// 2. 범위 시각화 + AttackDelay 동안 차오르는 연출
/// 3. 딜레이 종료 → 범위 내 플레이어에 데미지
/// 4. AttackSpeed 쿨다운 적용
///
/// 공격 중 몹은 이동 불가 (BT에서 Attack 상태 = 이동 차단)
/// </summary>
public sealed class MonsterAttackHandler : MonoBehaviour
{
    private MonsterRuntimeData _runtime;
    private Rigidbody2D        _rb;

    // 공격 상태 머신
    private enum AttackPhase { Ready, Charging, Cooldown }
    private AttackPhase _phase = AttackPhase.Ready;
    private float       _phaseTimer;

    // 범위 시각화 오브젝트 (풀링 또는 자식 오브젝트)
    private AttackRangeVisualizer _visualizer;

    private void Awake()
    {
        _runtime    = GetComponent<MonsterRuntimeData>();
        _rb         = GetComponent<Rigidbody2D>();
        _visualizer = GetComponentInChildren<AttackRangeVisualizer>(true);
    }

    /// <summary>공격 범위 내에 플레이어가 있는지 체크</summary>
    public bool IsPlayerInAttackRange()
    {
        if (_runtime.DetectedPlayer == null) return false;
        return CheckShapeContainsPlayer(_runtime.DetectedPlayer.position);
    }

    /// <summary>BT에서 호출. 공격 상태 머신을 실행.</summary>
    public BTStatus ExecuteAttack()
    {
        _runtime.CurrentState = MonsterState.Attack;
        _rb.linearVelocity = Vector2.zero; // 공격 중 이동 불가

        switch (_phase)
        {
            case AttackPhase.Ready:
                StartCharging();
                return BTStatus.Running;

            case AttackPhase.Charging:
                _phaseTimer -= Time.deltaTime;
                float progress = 1f - (_phaseTimer / _runtime.AttackDelay);
                _visualizer?.UpdateFillProgress(progress);

                if (_phaseTimer <= 0f)
                {
                    PerformDamage();
                    _phase = AttackPhase.Cooldown;
                    _phaseTimer = 1f / _runtime.AttackSpeed; // 쿨다운
                    _visualizer?.Hide();
                }
                return BTStatus.Running;

            case AttackPhase.Cooldown:
                _phaseTimer -= Time.deltaTime;
                if (_phaseTimer <= 0f)
                {
                    _phase = AttackPhase.Ready;
                    return BTStatus.Success;
                }
                return BTStatus.Running;
        }

        return BTStatus.Failure;
    }

    private void StartCharging()
    {
        _phase = AttackPhase.Charging;
        _phaseTimer = _runtime.AttackDelay;
        _visualizer?.Show(_runtime.Data.AttackShape, transform);
        GetComponent<MonsterAnimatorController>()?.PlayAttack();
    }

    private void PerformDamage()
    {
        if (_runtime.DetectedPlayer == null) return;

        if (CheckShapeContainsPlayer(_runtime.DetectedPlayer.position))
        {
            IDamageable target = _runtime.DetectedPlayer.GetComponent<IDamageable>();
            if (target != null && target.IsAlive)
            {
                float damage = DamageCalculator.CalcOutgoingDamage(
                    _runtime.BaseATK, 0f, 1f);
                target.TakeDamage(damage, gameObject);
            }
        }
    }

    /// <summary>공격 범위 형태별 플레이어 포함 여부 체크</summary>
    private bool CheckShapeContainsPlayer(Vector2 playerPos)
    {
        AttackShapeData shape = _runtime.Data.AttackShape;
        Vector2 monsterPos = (Vector2)transform.position;
        Vector2 forward = GetFacingDirection();

        switch (shape.ShapeType)
        {
            case AttackShapeType.Rectangle:
                return CheckRectangle(monsterPos, forward, playerPos,
                                      shape.RectWidth, shape.RectHeight);
            case AttackShapeType.Fan:
                return CheckFan(monsterPos, forward, playerPos,
                                shape.FanRadius, shape.FanAngle);
            case AttackShapeType.Circle:
                return CheckCircle(monsterPos, playerPos, shape.CircleRadius);
            default:
                return false;
        }
    }

    private bool CheckRectangle(Vector2 origin, Vector2 forward,
                                 Vector2 target, float w, float h)
    {
        // 몹의 로컬 좌표계로 변환 후 AABB 체크
        Vector2 right = new Vector2(forward.y, -forward.x); // 90° 회전
        Vector2 localPos = target - origin;
        float localX = Vector2.Dot(localPos, right);
        float localY = Vector2.Dot(localPos, forward);
        return Mathf.Abs(localX) <= w * 0.5f && localY >= 0 && localY <= h;
    }

    private bool CheckFan(Vector2 origin, Vector2 forward,
                           Vector2 target, float radius, float angle)
    {
        Vector2 dir = target - origin;
        float dist = dir.magnitude;
        if (dist > radius) return false;

        float dot = Vector2.Dot(dir.normalized, forward);
        float halfAngle = angle * 0.5f * Mathf.Deg2Rad;
        return dot >= Mathf.Cos(halfAngle);
    }

    private bool CheckCircle(Vector2 origin, Vector2 target, float radius)
    {
        return Vector2.Distance(origin, target) <= radius;
    }

    private Vector2 GetFacingDirection()
    {
        SpriteRenderer sr = GetComponent<SpriteRenderer>();
        return sr != null && sr.flipX ? Vector2.left : Vector2.right;
    }
}
```

### 13.2 AttackRangeVisualizer (범위 시각화)

```csharp
/// <summary>
/// 공격 범위를 시각적으로 표시하는 컴포넌트 (SRP).
/// 몹의 자식 오브젝트로 배치. SpriteRenderer 또는 LineRenderer로 범위 표현.
/// AttackDelay에 비례하여 몬스터 쪽에서 서서히 밝아지며 차오르는 연출.
/// MaterialPropertyBlock으로 _FillProgress 프로퍼티 제어.
/// </summary>
public sealed class AttackRangeVisualizer : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _renderer;
    [SerializeField] private Material       _rectangleMaterial;
    [SerializeField] private Material       _fanMaterial;
    [SerializeField] private Material       _circleMaterial;

    private MaterialPropertyBlock _mpb;
    private static readonly int HashFillProgress = Shader.PropertyToID("_FillProgress");
    private static readonly int HashFillColor    = Shader.PropertyToID("_FillColor");

    private void Awake()
    {
        _mpb = new MaterialPropertyBlock();
        if (_renderer == null) _renderer = GetComponent<SpriteRenderer>();
        gameObject.SetActive(false);
    }

    public void Show(AttackShapeData shape, Transform monsterTransform)
    {
        gameObject.SetActive(true);
        ConfigureShape(shape);
        UpdateFillProgress(0f);
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    /// <summary>0 ~ 1 로 차오르는 연출 갱신</summary>
    public void UpdateFillProgress(float progress)
    {
        if (_renderer == null) return;
        _renderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(HashFillProgress, Mathf.Clamp01(progress));

        // 차오르면서 색상이 진해지는 효과
        Color fillColor = Color.Lerp(
            new Color(1f, 0f, 0f, 0.1f),  // 시작: 연한 빨강
            new Color(1f, 0f, 0f, 0.6f),  // 끝: 진한 빨강
            progress);
        _mpb.SetColor(HashFillColor, fillColor);
        _renderer.SetPropertyBlock(_mpb);
    }

    private void ConfigureShape(AttackShapeData shape)
    {
        switch (shape.ShapeType)
        {
            case AttackShapeType.Rectangle:
                transform.localScale = new Vector3(shape.RectWidth, shape.RectHeight, 1f);
                transform.localPosition = new Vector3(0, shape.RectHeight * 0.5f, 0);
                break;
            case AttackShapeType.Fan:
                float diameter = shape.FanRadius * 2f;
                transform.localScale = new Vector3(diameter, diameter, 1f);
                transform.localPosition = Vector3.zero;
                break;
            case AttackShapeType.Circle:
                float d = shape.CircleRadius * 2f;
                transform.localScale = new Vector3(d, d, 1f);
                transform.localPosition = Vector3.zero;
                break;
        }
    }
}
```

---

## 14. SOLID 원칙 적용 요약

### Single Responsibility (단일 책임)

| 클래스 | 책임 |
|--------|------|
| `MonsterData` | 정적 데이터 저장 |
| `MonsterRuntimeData` | 동적 상태 관리 |
| `MonsterBase` | BT 코디네이션 + 상속 구조 |
| `DetectionSystem` | 감지 + LoS 체크 |
| `HitStaggerHandler` | 피격 경직 처리 |
| `MonsterNavigator` | 이동 + 장애물 회피 |
| `WanderSystem` | 자유 배회 |
| `MonsterAttackHandler` | 공격 로직 + 범위 판정 |
| `MonsterAnimatorController` | 애니메이션 상태 전환 |
| `MonsterLootDropper` | 아이템 드롭 |
| `MobManager` | 전체 몹 추적 + 일괄 조작 |
| `MobSpawner` | 풀링 + 스폰 |
| `FirstKillRegistry` | 첫 처치 기록 |
| `AttackRangeVisualizer` | 공격 범위 시각화 |

### Open/Closed

- `MonsterData` (ScriptableObject) → 코드 수정 없이 Inspector에서 새 몹 추가
- `MonsterBase` → `HostileMonster`, `NeutralMonster`, `BossMonster` 확장
- `BTNode` → 새로운 행동 노드 추가 시 기존 코드 변경 불필요

### Liskov Substitution

- `MonsterBase` 파생 클래스는 모두 `MobManager`의 `List<MonsterBase>`에서 동일하게 관리
- `IDamageable` 인터페이스를 통해 플레이어 무기 시스템과 호환

### Interface Segregation

- `IDamageable` (피격), `IStaggerable` (경직), `ISkillUser` (스킬) 등 세분화
- 불필요한 인터페이스 강제 없음

### Dependency Inversion

- `MobManager` → `MonsterBase` (추상)에 의존, `HostileMonster` 등 구체에 의존 안 함
- 무기 시스템 → `IDamageable` 인터페이스에 의존, `Enemy`/`MonsterBase`에 직접 의존 안 함

---

## 15. 파일 구조

```
Assets/Scripts/
├── Enemy/                              (기존 → 유지 후 점진 교체)
│   └── Enemy.cs                        (기존, 레거시 호환)
│
├── Monster/                            (★ 신규 폴더)
│   ├── Data/
│   │   ├── MonsterData.cs              (ScriptableObject)
│   │   ├── MonsterRuntimeData.cs       (동적 상태 컴포넌트)
│   │   ├── AttackShapeData.cs          (공격 범위 구조체)
│   │   ├── DropEntry.cs                (드롭 테이블 구조체)
│   │   └── MonsterEnums.cs             (MonsterType, AttackShapeType, MonsterState)
│   │
│   ├── Base/
│   │   ├── MonsterBase.cs              (추상 기반 클래스)
│   │   ├── HostileMonster.cs           (적대적 몹)
│   │   ├── NeutralMonster.cs           (중립 몹)
│   │   └── BossMonster.cs             (보스 몹)
│   │
│   ├── BT/                            (Behavior Tree)
│   │   ├── BTNode.cs                   (추상 노드)
│   │   ├── BTSelector.cs               
│   │   ├── BTSequence.cs               
│   │   ├── BTCondition.cs              
│   │   └── BTAction.cs                
│   │
│   ├── Systems/
│   │   ├── DetectionSystem.cs          (감지 + LoS)
│   │   ├── WanderSystem.cs             (자유 배회)
│   │   ├── MonsterNavigator.cs         (이동 + 장애물 회피)
│   │   ├── HitStaggerHandler.cs        (피격 경직)
│   │   ├── MonsterAttackHandler.cs     (공격 로직)
│   │   ├── MonsterAnimatorController.cs (애니메이션)
│   │   ├── MonsterLootDropper.cs       (아이템 드롭)
│   │   └── AttackRangeVisualizer.cs    (공격 범위 시각화)
│   │
│   └── Manager/
│       ├── MobManager.cs               (전역 매니저)
│       ├── MobSpawner.cs               (풀링 + 스폰)
│       ├── FirstKillRegistry.cs        (첫 처치 기록)
│       └── FirstKillBonusApplier.cs    (첫 처치 보너스)
│
├── Object/
│   ├── FloatingMagneticItem.cs         (기존 수정 — CollectItem 인벤토리 연동)
│   ├── DroppedItemIdentity.cs          (★ 신규 — 드롭 아이템 식별)
│   └── ItemDropSpawner.cs              (기존)
│
├── Core/
│   ├── StatSystem.cs                   (기존 수정 — AddPermanentBonus 추가)
│   └── ... (기존 유지)
│
└── InventoryManager.cs                 (★ 신규 — 인벤토리 아이템 추가 API)
```

---

## 부록: 요구사항 → 설계 매핑

| # | 요구사항 | 섹션 | 핵심 클래스 |
|---|---------|------|------------|
| 1 | ScriptableObject 데이터 설계 | §2 | `MonsterData`, `AttackShapeData`, `DropEntry` |
| 2 | 동적 스탯 관리 | §3 | `MonsterRuntimeData` |
| 3 | 애니메이션/BT/공격 처리 | §4, §13 | `MonsterBase`, `BTNode`, `MonsterAttackHandler`, `MonsterAnimatorController` |
| 4 | 피격 경직 (0.1초) | §5 | `HitStaggerHandler`, `IStaggerable` |
| 5 | 감지 시스템 + 장애물 LoS | §6 | `DetectionSystem` |
| 6 | 첫 처치 스탯 보너스 | §7 | `FirstKillRegistry`, `FirstKillBonusApplier` |
| 7 | 행동별 네비게이션 | §8 | `MonsterNavigator`, BT 분기 |
| 8 | 비감지 자유 배회 | §9 | `WanderSystem` |
| 9 | 오브젝트 풀링 + 랜덤 스폰 | §10 | `MobSpawner`, `SimpleObjectPool` |
| 10 | MobManager 전역 관리 | §11 | `MobManager` |
| 11 | 아이템 획득 + 인벤토리 연동 | §12 | `MonsterLootDropper`, `DroppedItemIdentity`, `InventoryManager` |
