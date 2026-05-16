# Enemy System Analysis

> **프로젝트**: UniTrio-Game-2026-fork  
> **분석 일시**: 2026-04-10  
> **분석 범위**: `Assets/Scripts/Enemy/`, `Assets/Scripts/Core/`, `Assets/Scripts/Player/Weapon/`  

---

## 1. 클래스 상속 구조 (Class Hierarchy)

```
IDamageable (인터페이스)
│   ├── bool IsAlive { get; }
│   └── void TakeDamage(float damage, GameObject source = null)
│
└── LivingEntity (추상 클래스, MonoBehaviour)
    │   ├── [RequireComponent] HealthSystem
    │   ├── IsInvincible (무적 플래그)
    │   ├── TakeDamage() → CalculateIncomingDamage() → Health.ApplyDamage()
    │   └── abstract OnDeath()
    │
    ├── Enemy (현재 에너미 클래스) ← ★ 분석 대상
    │       ├── 접촉 데미지 (Contact Damage)
    │       └── OnDeath() → Destroy(gameObject)
    │
    └── CharacterBase (추상 클래스)
        │   ├── [RequireComponent] StatSystem
        │   ├── CalculateIncomingDamage() → DamageCalculator.CalcDamageTaken(raw, Def)
        │   └── Stats 프로퍼티 (TotalAtk, TotalDef, TotalMoveSpeed 등)
        │
        └── PlayerEntity
                ├── [RequireComponent] KarmaHandler
                ├── CalculateIncomingDamage() += 카르마 배율
                └── OnDeath() → 카르마 +1
```

> **핵심 포인트**: `Enemy`는 `LivingEntity`를 **직접** 상속하므로 `StatSystem`이 없다.  
> 즉, **방어력(Def)이 존재하지 않으며**, 받는 데미지 = raw 데미지 그대로 적용된다.  
> 주석에 "Milestone 4에서 `CharacterBase`로 변경 예정" 이라고 명시되어 있음.

---

## 2. Enemy 클래스 상세 분석

**파일 위치**: `Assets/Scripts/Enemy/Enemy.cs` (45줄)

### 2.1 Inspector 필드

| 필드 | 타입 | 기본값 | 설명 |
|------|------|--------|------|
| `_contactDamage` | float | 1.0 | 플레이어 접촉 시 주는 데미지 |
| `_contactDamageCooldown` | float | 0.5초 | 접촉 데미지 쿨다운 (연속 타격 방지) |

> **참고**: HP 관련 설정은 `HealthSystem` 컴포넌트의 `_maxHp` (기본값 10)에서 관리됨.

### 2.2 접촉 데미지 로직

```csharp
// Update(): 타이머 기반 쿨다운 (GC 없음)
if (_contactDamageTimer > 0f)
    _contactDamageTimer -= Time.deltaTime;

// OnCollisionStay2D(): Rigidbody2D 충돌 기반
if (!IsAlive || _contactDamageTimer > 0f) return;
var damageable = col.gameObject.GetComponent<IDamageable>();
if (damageable == null || !damageable.IsAlive) return;
damageable.TakeDamage(_contactDamage, gameObject);
_contactDamageTimer = _contactDamageCooldown;
```

- **충돌 유형**: `OnCollisionStay2D` → Rigidbody2D + **비-트리거** Collider2D 필요
- **대상 판별**: `IDamageable` 인터페이스로 접근 → 플레이어뿐 아니라 다른 `IDamageable` 오브젝트에도 데미지 가능
- **GC 최적화**: 코루틴 대신 타이머 변수 사용

### 2.3 사망 처리

```csharp
protected override void OnDeath()
{
    Debug.Log($"[{gameObject.name}] 처치됨");
    Destroy(gameObject);  // Milestone 4에서 오브젝트 풀링으로 교체 예정
}
```

---

## 3. 핵심 코어 시스템 (Core Systems)

### 3.1 HealthSystem (HP 관리)

**파일**: `Assets/Scripts/Core/HealthSystem.cs` (160줄)

```
[HealthSystem]
├── _maxHp (Inspector 설정, 기본 10)
├── _currentHealth (실시간 표시)
├── _isAlive
├── IsInvulnerable (무적 상태)
│
├── Events:
│   ├── OnHpChanged(currentHp, maxHp)  → UI 연동용
│   ├── OnDied                          → LivingEntity.HandleDeath() 호출
│   └── OnHit                          → 피격 이벤트
│
├── ApplyDamage(float) → HP 감소 + 플래시 이펙트 + 이벤트 발화
├── Heal(float)        → HP 회복
├── Resurrect(float)   → 부활
└── SetMaxHp(float, bool refill) → 최대 HP 변경 (비율 유지)
```

**피격 플래시 효과**:
- `SpriteGlow` 커스텀 셰이더의 `_FlashAmount` 프로퍼티를 `MaterialPropertyBlock`으로 제어
- 타이머 기반 (`_flashDuration` = 0.15초) → 코루틴 없이 GC 제로
- `MaterialPropertyBlock` 사용 → 머티리얼 인스턴스 복제 없이 배칭 유지

**StatSystem 연동**:
- `StatSystem`이 같은 오브젝트에 존재하면 `TotalMaxHP`를 자동 동기화
- `OnStatsChanged` 이벤트 구독 → 장비 변경 시 최대 HP 자동 갱신

### 3.2 DamageCalculator (데미지 공식)

**파일**: `Assets/Scripts/Core/DamageCalculator.cs` (34줄)

| 공식 | 수식 | 설명 |
|------|------|------|
| `CalcOutgoingDamage` | `max(0, (statAtk + itemAtk) * multiplier)` | 공격력 계산 |
| `CalcDamageTaken` | `max(1, rawDamage - defense) * karmaMultiplier` | 피격 데미지 계산 |
| `GetKarmaMultiplier` | `1 + karmaPoints * 0.1` | 카르마 → 배율 변환 |

> **Enemy의 경우**: `LivingEntity.CalculateIncomingDamage()`가 raw 데미지를 그대로 반환하므로,  
> `CalcDamageTaken()`이 호출되지 않고 **방어력 감산이 없음**.

### 3.3 HitStopManager (타격감)

**파일**: `Assets/Scripts/Core/HitStopManager.cs` (66줄)

- **싱글톤 패턴** (자동 생성 + DontDestroyOnLoad)
- `TriggerHitStop(float duration = 0.1)`: `Time.timeScale`을 0.01로 설정
- `WaitForSecondsRealtime`으로 비스케일 시간 기반 대기 후 복원
- 중첩 방지: `_isStopped` 플래그

### 3.4 IDamageable 인터페이스

```csharp
public interface IDamageable
{
    bool IsAlive { get; }
    void TakeDamage(float damage, GameObject source = null);
}
```

**구현체**: `LivingEntity` (→ Enemy, PlayerEntity), `TreeHit` (나무 오브젝트)

---

## 4. 데미지 파이프라인 (Damage Pipeline)

### 4.1 플레이어 → 에너미 방향

```
[플레이어 공격]
    │
    ├─ SwordHitbox (근접 무기)
    │   └─ OnTriggerEnter2D → tag "Enemy" 확인
    │       → IDamageable 취득 → CalcOutgoingDamage(TotalAtk, baseDmg) * bashMult
    │       → target.TakeDamage(damage, gameObject)
    │       → HitVFX + DamageText 스폰
    │       → [강타 시] 카메라 쉐이크(0.2s, 0.4) + HitStop(0.25s) + 3종 이펙트
    │
    ├─ ArrowProjectile (활 기본 공격)
    │   └─ OnTriggerEnter2D → tag "Enemy"
    │       → IDamageable.TakeDamage(_damage)
    │       → HitVFX + DamageText → 충돌 후 풀 반환
    │
    ├─ AimedShotArrow (조준 사격 스킬, 관통)
    │   └─ OnTriggerEnter2D → tag "Enemy"
    │       → IDamageable.TakeDamage(_damage)
    │       → HitVFX + DamageText → ★ 파괴되지 않음 (관통)
    │       ※ 30칸 비거리, 25칸부터 alpha 페이드
    │
    ├─ MagicProjectile (완드 투사체)
    │   └─ OnTriggerEnter2D → tag "Enemy"/"Wall"/"Obstacle"
    │       → Explode() → SpawnExplosion()
    │       → ExplosionEffect.SetupExplosion(damage)
    │
    ├─ ExplosionEffect (마법 폭발 범위 데미지)
    │   └─ OnEnable() → ApplyAreaDamage()
    │       → Physics2D.OverlapCircle (Enemy 레이어 마스크)
    │       → 범위 내 모든 IDamageable에 데미지
    │
    └─ ManaSpearProjectile (마나 창 스킬)
        └─ OnTriggerEnter2D → tag "Enemy"
            → IDamageable.TakeDamage(_damage) → TriggerHit()
```

### 4.2 에너미 → 플레이어 방향

```
[에너미 접촉 데미지]
    Enemy.OnCollisionStay2D(Collision2D)
        │
        ├─ 생존 확인 (IsAlive) + 쿨다운 확인 (_contactDamageTimer)
        ├─ IDamageable 취득 (플레이어의 LivingEntity)
        └─ damageable.TakeDamage(_contactDamage, gameObject)
            │
            └─ PlayerEntity.TakeDamage()
                ├─ IsInvincible 확인 (대시 중이면 true)
                ├─ CalculateIncomingDamage(rawDamage)
                │   └─ CalcDamageTaken(raw, Def, karmaMulti)
                │       = max(1, raw - Def) * (1 + karma * 0.1)
                └─ Health.ApplyDamage(final)
                    ├─ HP 감소 + 플래시 이펙트
                    ├─ OnHit 이벤트
                    └─ HP ≤ 0 → OnDied → PlayerEntity.OnDeath()
```

### 4.3 대시 중 에너미 면역

```
DashState.Enter() (업그레이드 후):
    ├─ Entity.IsInvincible = true       → TakeDamage 무시
    ├─ Rigidbody2D.excludeLayers |= Enemy 레이어  → 물리 충돌 제외
    └─ AfterimagePool.StartSpawning()   → 잔상 VFX

DashState.Exit():
    ├─ Entity.IsInvincible = false
    ├─ Rigidbody2D.excludeLayers &= ~Enemy 레이어  → 충돌 복원
    └─ AfterimagePool.StopSpawning()
```

---

## 5. 에너미 오브젝트 필수 컴포넌트 구성

Enemy 프리팹/오브젝트에 필요한 최소 구성:

| 컴포넌트 | 필수 | 설명 |
|----------|------|------|
| `Enemy` (MonoBehaviour) | ✅ | 접촉 데미지 + 사망 처리 |
| `HealthSystem` | ✅ | `[RequireComponent]`로 자동 부착. HP 관리 |
| `SpriteRenderer` | ⚠️ | 피격 플래시 효과에 필요 (없어도 동작은 함) |
| `Rigidbody2D` | ✅ | `OnCollisionStay2D` 발동에 필요 |
| `Collider2D` (비-트리거) | ✅ | 접촉 충돌 감지용 |
| Tag: `"Enemy"` | ✅ | 플레이어 무기의 태그 기반 충돌 판정 |
| Layer: `"Enemy"` | ✅ | 대시 레이어 제외, 폭발 범위 검출 |

---

## 6. 성능 최적화 패턴

| 패턴 | 적용 위치 | 설명 |
|------|-----------|------|
| **타이머 기반 쿨다운** | Enemy, HealthSystem | 코루틴 0개 → GC 제로 |
| **MaterialPropertyBlock** | HealthSystem | 머티리얼 인스턴스 복제 방지 → 배칭 유지 |
| **Shader.PropertyToID 캐싱** | HealthSystem | 매 프레임 문자열 해싱 방지 |
| **SimpleObjectPool** | 모든 무기·VFX | Instantiate/Destroy 대신 풀링 |
| **static Collider2D[] 배열** | ExplosionEffect | OverlapCircle 가비지 제거 |
| **IDamageable 인터페이스** | 전체 | Enemy 타입에 직접 의존하지 않음 → 결합도 ↓ |

---

## 7. 현재 미구현 사항 (TODO / Milestone)

코드 주석에 명시된 향후 계획:

| 항목 | 현재 상태 | 계획 (Milestone) |
|------|-----------|-------------------|
| **MonsterBase 클래스** | Enemy → LivingEntity 직접 상속 | M4: CharacterBase 상속으로 변경 (방어력/스탯 추가) |
| **오브젝트 풀링** | `Destroy(gameObject)` 사용 | M4: SimpleObjectPool 기반 반환으로 교체 |
| **AI / 이동 로직** | 없음 (정적 배치) | 미정 *(추론: FSM 또는 Behavior Tree 방식의 AI 추가 필요)* |
| **드롭 시스템** | 없음 | 미정 *(OnDeath에서 아이템 스폰 로직 추가 필요)* |
| **어그로 시스템** | `TakeDamage`에 `source` 파라미터는 있으나 미사용 | 미정 |
| **넉백 / 히트 리액션** | 없음 | 미정 |
| **상태 이상 (디버프)** | 없음 | 미정 |
| **몬스터 개별 스탯** | 없음 (HP만 존재) | M4: CharacterBase 전환 시 StatSystem 부착 |
| **공격 패턴** | 접촉 데미지만 존재 | 미정 *(원거리 공격, 스킬 등 추가 필요)* |

---

## 8. 아키텍처 다이어그램

```
┌──────────────────────────────────────────────────────┐
│                    Scene Layer                        │
│                                                      │
│  ┌──────────┐    OnCollisionStay2D    ┌────────────┐ │
│  │  Enemy   │ ───────────────────────→│ PlayerEntity│ │
│  │(LivingE) │        _contactDmg      │(CharacterB) │ │
│  │          │                         │             │ │
│  │ Health ──┤←── ApplyDamage ◄────────│── Weapons   │ │
│  │ System   │    (from weapons)       │ (Sword/Bow/ │ │
│  │          │                         │  Wand/Skill)│ │
│  └──────────┘                         └────────────┘ │
│       ↑                                    ↑         │
│       │ IDamageable                        │         │
│       │                              StatSystem      │
│       │                              HealthSystem     │
│       │                              KarmaHandler     │
└───────┼──────────────────────────────────────────────┘
        │
┌───────┼──────────────────────────────────────────────┐
│       │              Core Layer                       │
│       │                                              │
│  IDamageable ─── LivingEntity ─── CharacterBase      │
│  IBonusProvider     │                  │              │
│  ISkillUser         │                  │              │
│                HealthSystem       StatSystem          │
│                DamageCalculator   HitStopManager      │
└──────────────────────────────────────────────────────┘
```

---

## 9. 확장 시 권장 사항

### 9.1 MonsterBase 클래스 도입 (Milestone 4)

```csharp
// 제안 구조
public abstract class MonsterBase : CharacterBase
{
    // StatSystem 자동 부착 (CharacterBase의 RequireComponent)
    // → 방어력, 이동속도 등 스탯 활용 가능
    
    protected override float CalculateIncomingDamage(float rawDamage)
    {
        // 방어력 감산 적용
        return DamageCalculator.CalcDamageTaken(rawDamage, Stats.TotalDef);
    }
    
    protected override void OnDeath()
    {
        DropLoot();         // 드롭 아이템 처리
        PoolReturn();       // 오브젝트 풀 반환
    }
}
```

### 9.2 AI 시스템 방향

현재 플레이어의 FSM 구조(`PlayerStateMachine` + `PlayerState`)를 참고하여  
에너미용 FSM을 별도로 구성하는 것을 권장:

```
EnemyStateMachine
├── IdleState (대기 / 순찰)
├── ChaseState (추적)
├── AttackState (공격 패턴 실행)
├── HitState (피격 경직)
└── DeathState (사망 연출 → 풀 반환)
```

### 9.3 어그로 시스템

`TakeDamage(float damage, GameObject source)`의 `source` 파라미터가  
이미 인터페이스에 정의되어 있으므로, 이를 활용한 어그로 관리가 자연스럽게 가능:

```csharp
public override void TakeDamage(float damage, GameObject source = null)
{
    base.TakeDamage(damage, source);
    if (source != null)
        AggroSystem.AddThreat(source, damage);
}
```

---

## 10. 관련 파일 목록

| 경로 | 역할 | 줄 수 |
|------|------|-------|
| `Assets/Scripts/Enemy/Enemy.cs` | 적 엔티티 본체 | 45 |
| `Assets/Scripts/Core/LivingEntity.cs` | 생명체 추상 기반 | 56 |
| `Assets/Scripts/Core/CharacterBase.cs` | 스탯 보유 캐릭터 기반 | 29 |
| `Assets/Scripts/Core/HealthSystem.cs` | HP 관리 + 피격 플래시 | 160 |
| `Assets/Scripts/Core/StatSystem.cs` | 스탯 계산 + 마나 | 176 |
| `Assets/Scripts/Core/DamageCalculator.cs` | 데미지 공식 유틸리티 | 34 |
| `Assets/Scripts/Core/IDamageable.cs` | 피격 가능 인터페이스 | 15 |
| `Assets/Scripts/Core/IBonusProvider.cs` | 스탯 보너스 인터페이스 | 16 |
| `Assets/Scripts/Core/HitStopManager.cs` | 타격 정지 연출 | 66 |
| `Assets/Scripts/Player/PlayerEntity.cs` | 플레이어 엔티티 | 55 |
| `Assets/Scripts/Player/KarmaHandler.cs` | 카르마 시스템 | 36 |
| `Assets/Scripts/Player/Weapon/SwordHitbox.cs` | 근접 무기 히트박스 | 202 |
| `Assets/Scripts/Player/Weapon/ArrowProjectile.cs` | 화살 투사체 | 116 |
| `Assets/Scripts/Player/Weapon/AimedShotArrow.cs` | 조준 사격 (관통) | 207 |
| `Assets/Scripts/Player/Weapon/MagicProjectile.cs` | 마법 투사체 | 197 |
| `Assets/Scripts/Player/Weapon/ManaSpearProjectile.cs` | 마나 창 스킬 | 220 |
| `Assets/Scripts/Player/Weapon/ExplosionEffect.cs` | 폭발 범위 데미지 | 172 |
| `Assets/Scripts/Player/FSM/States/DashState.cs` | 대시 (적 면역) | 89 |
