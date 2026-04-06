# UniTrio-Game-2026 마스터 구현 계획서

> 최종 업데이트: 2026-04-06 (코드 리뷰 반영 + 대시 업그레이드 시스템 추가)

---

## 1. 프로젝트 개요

- **프로젝트명:** UniTrio 2D Top-Down Action
- **장르:** 2D 탑다운 액션 / 샌드박스
- **환경:** Unity 3D Core, URP, C#
- **특징:** 3D URP 환경에서 2D 탑다운 시점 구현. 빠른 판정과 안정적인 프레임 유지 필수.

---

## 2. 코딩 규칙 및 컨벤션

> 이 프로젝트의 모든 코드는 아래 규칙을 반드시 준수합니다.

### 2-1. 퍼포먼스 및 메모리 관리

- **GC 최소화:** `Update` / `FixedUpdate` / `LateUpdate` 내부에서 `new` 객체 생성 또는 문자열 `+` 연산 금지.
- **오브젝트 풀링 필수:** 총알, VFX, 몬스터, 데미지 텍스트 등 빈번히 생성·파괴되는 모든 객체는 Object Pool 패턴 사용. 게임플레이 중 `Instantiate` / `Destroy` 남발 금지.
- **LINQ 런타임 사용 금지:** `Where`, `ToList`, `Select` 등은 런타임 루프 안에서 사용 불가. 반드시 `for` / `foreach` 루프로 대체.
- **캐싱 필수:** `GetComponent<T>()`, `Camera.main`, `FindObjectOfType<T>()` 등은 반드시 `Awake()` 또는 `Start()`에서 변수에 캐싱 후 사용.
- **코루틴 최적화:** `yield return new WaitForSeconds()`는 매 호출마다 GC 발생. 타이머 변수를 활용한 `Update` 방식 또는 캐싱된 `WaitForSeconds` 재사용 권장.

### 2-2. 아키텍처 및 디자인 패턴

- **싱글톤 남용 금지:** `GameManager`, `AudioManager` 등 전역 접근이 꼭 필요한 매니저 클래스에만 한정. 컴포넌트 간 통신은 C# `event` / `Action` 기반 이벤트 아키텍처 우선.
- **데이터와 로직의 분리 (SRP):** 몬스터 스탯, 아이템 정보, 무기 데이터 등 기획 데이터는 `ScriptableObject`로 관리. 프리팹 내부 하드코딩 금지.
- **단일 책임 원칙 (SRP):** 클래스 하나는 하나의 책임만. 목표 150~200줄 이내로 모듈화.
- **상태 관리 (FSM):** 플레이어·보스의 행동 패턴은 `switch-case` 대신 FSM / State 패턴 적용. 현재 `PlayerStateMachine` 구조 참고.

### 2-3. URP 및 탑다운 특화 규칙

- **물리 일관성:** `Rigidbody2D` + `Collider2D` (2D 물리) 또는 `Rigidbody` + `Collider` (3D 물리) 중 프로젝트 통일 방식만 사용. 혼용 금지.
- **레이어 매트릭스 활용:** 아래 레이어를 분리하고 Layer Collision Matrix에서 불필요한 충돌 연산을 차단.

  | 레이어 | 역할 |
  |---|---|
  | `Player` | 플레이어 히트박스 |
  | `Enemy` | 몬스터 히트박스 |
  | `Projectile_P` | 플레이어 발사체 (Enemy / Obstacle / Wall 과만 충돌) |
  | `Projectile_E` | 몬스터 발사체 (Player / Obstacle / Wall 과만 충돌) |
  | `Obstacle` | 파괴 가능한 지형지물 (발사체 충돌 시 파괴) |
  | `Wall` | 고정 지형 (모든 물리 오브젝트와 충돌, 파괴 불가) |

  **Layer Collision Matrix 설계 기준:**
  ```
                Player  Enemy  Proj_P  Proj_E  Obstacle  Wall
  Player           -      O      -       O        O       O
  Enemy            O      -      O       -        O       O
  Proj_P           -      O      -       -        O       O
  Proj_E           O      -      -       -        O       O
  ```

- **깊이 정렬:** Y축(또는 Z축) 위치 기반으로 렌더링 순서 결정. 겹침 처리 로직 작성 시 항상 고려.
- **벡터 연산:** 거리 비교 시 `sqrMagnitude` 우선. `Distance` / `Magnitude`는 `Sqrt` 연산 포함으로 루프 안에서 비용이 큼.
- **MaterialPropertyBlock 사용:** 셰이더 프로퍼티(플래시, 색조 등) 변경 시 `material` 직접 접근 금지. `MaterialPropertyBlock`으로 머티리얼 인스턴스 복제 없이 적용하여 SRP Batching 유지. (`HealthSystem` 피격 플래시가 적용 예시)
- **NonAlloc Physics:** 범위 판정 시 `OverlapCircleNonAlloc`, `RaycastNonAlloc` 등 결과 배열을 사전 할당하는 NonAlloc API 사용. 매 프레임 배열 생성으로 인한 GC 방지. (`ExplosionEffect`가 적용 예시)
- **Update 루프 최소화:** 매 프레임 상태를 폴링하는 대신, 상태가 변하는 시점에 `event` / `Action`으로 알리고 구독자가 처리. `OnHpChanged`, `OnDied` 등 현재 구축된 이벤트 패턴을 동일하게 적용.

### 2-4. 코드 스타일

| 대상 | 규칙 | 예시 |
|---|---|---|
| 클래스 / 메서드 | PascalCase | `PlayerEntity`, `TakeDamage()` |
| 퍼블릭 필드 / 프로퍼티 | PascalCase | `MaxHp`, `IsAlive` |
| 프라이빗 / 프로텍티드 필드 | _camelCase | `_currentHealth`, `_dashTimer` |
| 로컬 변수 / 매개변수 | camelCase | `damage`, `targetPos` |

- **방어적 프로그래밍:** null 참조 방지를 위해 `?.`, `??`, `if (obj == null)` 적극 활용.
- **주석:** "왜 이렇게 했는지" 핵심 이유만 짧게. 장황한 설명 지양.
- **외부 라이브러리 지양:** 기본 Unity API로 해결 가능한 문제에 외부 플러그인 사용 금지.

### 2-5. 렌더링과 데이터의 분리

- **데이터 클래스는 순수 데이터만:** `ScriptableObject` 및 데이터 보관 클래스(`ItemData`, `MonsterData` 등)는 상태 값과 수치만 보유. `Use()`, `Render()`, `Show()` 등 행동·출력 로직을 데이터 클래스 안에 작성 금지.
- **렌더링 로직은 호출하는 쪽에서:** UI 갱신, 스프라이트 변경, 파티클 재생 등 시각적 출력은 `View` 또는 해당 컴포넌트(`MonoBehaviour`)에서 담당. 데이터가 변경되면 이벤트(`event`, `Action`)로 알리고, 렌더링 쪽이 구독하여 처리.
- **예시 구조:**
  ```
  ItemData (SO)          → 수치만 보유 (HealAmount, Damage 등)
  InventorySlot (View)   → ItemData를 받아 아이콘·수량 텍스트를 출력
  QuickSlotManager       → 입력을 받아 타입별 사용 로직 처리 (Use 행동 담당)
  ```
- **MonoBehaviour도 동일:** 게임 오브젝트 컴포넌트 역시 데이터 상태(`_currentHealth` 등)와 시각 효과(플래시, 색조 변경)를 같은 클래스 안에 섞지 말고, 가능하면 `HealthSystem`(데이터) / `SpriteRenderer 제어`(렌더링)처럼 책임을 분리.

### 2-6. 의존성 및 컴포넌트 참조

- **Find 계열 함수 절대 금지 (런타임 / Awake 모두):** `GameObject.Find()`, `FindObjectOfType()`, `FindGameObjectWithTag()` 사용 불가.
- **명시적 참조 우선:** 다른 컴포넌트 참조 시 반드시 `[SerializeField]`로 Inspector에서 직접 할당.
- **결합도 감소:** 오브젝트 간 통신은 직접 참조보다 C# `event` / `Action` 또는 `ScriptableObject` 채널 방식 우선.

---

## 3. 핵심 아키텍처

```
LivingEntity (abstract)          ← IDamageable 구현
  └── CharacterBase (abstract)   ← StatSystem RequireComponent
        └── PlayerEntity         ← KarmaHandler, ISkillUser 구현
        └── MonsterBase (예정)   ← AI 컴포넌트 연동

HealthSystem    ← HP 관리, 피격 플래시 (SRP 분리)
StatSystem      ← 스탯 합산, 마나 자연 회복 (SRP 분리)
DamageCalculator← 정적 데미지 공식 유틸

PlayerStateMachine ← Idle / Walk / Hit / Dash / Death / Cutscene
WeaponBehaviourBase← 모든 무기의 추상 기반

ItemData (abstract, SO) ← WeaponData / ConsumableData / IngredientData
ItemDatabase (SO)       ← 전체 아이템 마스터 테이블
Item                    ← 인스턴스 (동적 수량·티어 관리)
```

### 데미지 공식

```
주는 데미지 = (PlayerAtk + WeaponBaseDmg) × 배율
받는 데미지 = (rawDamage - Def) ÷ 피해경감 × KarmaMultiplier
KarmaMultiplier = 1.0 + (KarmaPoints × 0.1)
```

---

## 4. 구현 완료 현황

### Battle 파트 ✅
| 시스템 | 비고 |
|---|---|
| LivingEntity / CharacterBase | 공통 생명체 기반 |
| HealthSystem | 피격 플래시, GC 없는 타이머 방식 |
| StatSystem | 마나 자연 회복, IBonusProvider 목록 |
| DamageCalculator | 정적 공식 유틸 |
| KarmaHandler | 사망 시 카르마 +1, 피해 10%씩 증폭 |
| PlayerEntity | CharacterBase 상속, ISkillUser |
| PlayerStateMachine | Idle / Walk / Hit / Dash / Death / Cutscene |
| DashHandler | 무적, 쿨다운, 공격 양방향 잠금, Enemy 레이어 통과 |
| PlayerMovement | StatSystem.TotalMoveSpeed 연동 |
| PlayerWeaponController | 콤보 버퍼링, 커서 추적, 무기 교체 (숫자키) |
| 검 / 창 / 활 / 완드 | WeaponBehaviourBase 상속 구현 완료 |
| Enemy | LivingEntity 상속, 접촉 데미지 (타이머 방식) |

### MapGen 파트 ✅
| 시스템 | 비고 |
|---|---|
| MapGenerator | 청크 풀링, 듀얼 그리드, 노이즈 바이옴 |
| 전장의 안개 / 미니맵 | Vision 레이어, TrailRenderer, RenderTexture |
| PortalController | 파티클 연출, 확장 애니메이션 (진입 로직 미구현) |
| ItemData 계층 | abstract SO → WeaponData / ConsumableData / IngredientData |
| ItemDatabase | ID 기반 조회, 타입 필터링 |
| InventoryGenerator | 행·열 동적 생성 |
| InventorySlot | 드래그 비주얼 (데이터 이동 미구현) |
| QuickSlotManager | 1~8키, 타입별 사용 분기 |
| DataManager | 딕셔너리 캐시 |

---

## 4-2. 핫바 / 슬롯 구조

```
┌─────────────────────────────────────────────┐
│  [WeaponSlot A] [WeaponSlot B]  ← E키 스왑   │
│  [SkillSlot]                    ← Q키 사용   │
│  [1] [2] [3] [4] [5] [6] [7] [8] [9]        │
│   ↑                                          │
│   1번 = 현재 활성 무기 (읽기전용 표시)          │
│   2~9 = 건축/소모품 등 자유 배치              │
└─────────────────────────────────────────────┘
```

- **WeaponSlot A/B:** 무기 2개를 보관, E키로 활성 슬롯 1↔2 토글. 현재 활성 슬롯이 핫바 1번에 표시.
- **SkillSlot:** 인벤토리에서 `SkillData` 드래그 앤 드롭으로 장착. Q키로 시전.
- **핫바 1번:** 현재 활성 무기 읽기전용 표시. 직접 클릭/배치 불가.
- **핫바 2~9:** `BuildingData`, `ConsumableData` 등 자유 배치. 해당 키 입력 시 사용.

---

## 4-3. ItemData 계층 전체 구조

```
ItemData (abstract, ScriptableObject)
├── WeaponData          ← 무기. Damage, AttackSpeed
├── ConsumableData      ← 소모품/음식. HealAmount
├── IngredientData      ← 재료. 스탯 없음
├── SkillData           ← 스킬. ManaCost, Cooldown (ItemData 편입)
├── ArmorData           ← 방어구. IBonusProvider 구현 (DefBonus 등)
├── AccessoryData       ← 장신구. IBonusProvider 구현 (복합 스탯 보너스)
└── BuildingData        ← 건축 아이템. 타일 배치용 (우선순위 후순위)
```

**장착형 아이템 (ArmorData, AccessoryData):**
- `IBonusProvider` 구현 → 장착 시 `StatSystem.RegisterBonus(this)`, 해제 시 `UnregisterBonus(this)` 호출
- 매 프레임 스탯 재계산 없음. 장착/해제 트리거 시점에만 반영 (이벤트 기반)

---

## 5. 구현 로드맵

---

### 선행 작업 — 아이템 계층 및 핫바 시스템 ← 현재 목표

#### 선행 0. ItemData 계층 완성 ✅
- `SkillData` → `IUseable` 독립에서 `ItemData` 상속으로 전환. `ItemType.Skill` 추가
- `ArmorData` 생성: `ItemData` 상속 + `IBonusProvider` 구현
- `AccessoryData` 생성: `ItemData` 상속 + `IBonusProvider` 구현
- `DataManager` 스킬 캐시 API 업데이트 (`skill.id` → `skill.Id`)
- `BuildingData` 생성: 껍데기만 (구현은 Milestone 7에서)
- **코드 리뷰 반영:** 모든 public 필드 `_camelCase` → `PascalCase` 수정 완료

#### 선행 0.5. 대시 업그레이드 시스템

> **PlayerAnimController 파라미터:** `DirX`(Float), `DirY`(Float), `IsMoving`(Bool), `Attack`(Trigger)
> `IsDashing` 파라미터 없음 → `Animator.speed`로 프레임 고정 처리

##### 공통 — 애니메이션 프리즈 (업그레이드 전/후 모두)

- `DashState.Enter()`: `Machine.Animator.speed = 0f` → 대시 시작 시점의 프레임 고정
- `DashState.Exit()`: `Machine.Animator.speed = 1f` → 대시 종료 후 재생 재개
- `PlayerMovement`의 `IsMoving`, `DirX/Y` 파라미터는 대시 중에도 갱신되므로 `Machine.Movement.enabled = false`(기존 DashState 처리)가 이를 자동 차단

##### 업그레이드 전 대시 (Base Dash)

- 파란 색조 틴트(`DashTint`) 유지
- **무적 없음:** `Entity.IsInvincible` 미설정
- **충돌 무시 없음:** `excludeLayers` 미적용
- 이동 거리·속도는 기존 `DashHandler` 그대로

##### 업그레이드 후 대시 (Upgraded Dash)

- 기존 무적(`IsInvincible = true`) + Enemy 레이어 충돌 무시(`excludeLayers`) 유지
- **잔상 VFX 추가** (아래 참조)

##### 잔상 VFX 설계 (`DashAfterimagePool`)

```
DashAfterimagePool (MonoBehaviour)
├── 풀 크기: 10개 (씬 시작 시 미리 생성, Object Pool 패턴)
├── Spawn(sprite, position, scale, baseColor):
│   └── 비활성 AfterimageGhost를 꺼내 위치·스프라이트·색상 세팅 후 활성화
└── DashState.Enter() → StartSpawning() / DashState.Exit() → StopSpawning()

AfterimageGhost (MonoBehaviour)
├── SpriteRenderer 하나만 보유
├── 초기 alpha: 0.7  /  페이드아웃 시간: 0.3초
├── Update(): _timer += deltaTime; alpha = Lerp(0.7→0, _timer/0.3)
│            0.3초 경과 시 비활성화(풀 반환)
└── GC 없는 Update 방식 (WaitForSeconds 코루틴 금지)
```

**스폰 간격 계산:**
- 대시 지속시간 0.18초, 잔상 최대 10개 → 0.018초(≈1프레임)마다 1개 스폰
- `DashAfterimagePool`은 `FixedUpdate` 또는 타이머로 스폰 간격 제어

##### 업그레이드 여부 제어

- `DashHandler`에 `[SerializeField] private bool _isUpgraded = false` 추가
- `public bool IsUpgraded => _isUpgraded` 프로퍼티 노출
- `DashState`에서 `Machine.Dash.IsUpgraded` 체크 → 무적·충돌무시·잔상 분기
- 추후 **스킬트리** 구현 시 `DashHandler.IsUpgraded = true` 호출로 연동 (외부 진입점 단일화)

##### 수정 파일 목록

| 파일 | 변경 내용 |
|---|---|
| `DashHandler.cs` | `_isUpgraded` 필드 + `IsUpgraded` 프로퍼티 추가 |
| `DashState.cs` | Animator.speed 0/1 토글, IsUpgraded 분기 (무적·충돌·잔상) |
| `DashAfterimagePool.cs` (신규) | 잔상 오브젝트 풀 + 스폰 타이머 |
| `AfterimageGhost.cs` (신규) | 단일 잔상 페이드아웃 로직 |

---

#### 선행 1. WeaponSlotManager — E키 2슬롯 스왑 🔧
코드 구현 완료. 씬 연결 작업 필요.
- ~~숫자키(1~9) 무기 교체~~ → **E키 2슬롯 전용으로 변경** (`PlayerWeaponController.HandleWeaponSwitch()` 제거)
- 무기 최대 2개 강제 (슬롯 A / B)
- 씬 UI 배치 완료 → Editor 스크립트로 `WeaponSlotManager` 컴포넌트·Inspector 연결 필요
- `OnSlotChanged` 이벤트로 핫바 1번 UI 갱신

#### 선행 2. SkillSlotManager — Q키 스킬 시전 🔧
코드 구현 완료. 씬 배치 필요.
- `SkillSlotManager` 컴포넌트를 Player에 부착, `StatSystem` 자동 참조
- 인벤토리 `SkillData` 드래그 앤 드롭 → `EquipSkill()` 호출
- Q키 → `HasEnoughMana` 체크 → `ConsumeMana` → 쿨다운 타이머
- Editor 설정 스크립트 또는 배치 가이드 제공 예정

#### 선행 3. HitState 개선 🔧
현재 0.2초 스태거만 있음. 아래 항목 추가 필요:
- **Cinemachine 카메라 셰이크** (피격 시 짧은 임펄스)
- **플레이어 붉은 점멸** (기존 흰색 플래시 → 빨간색으로 변경)
- **Animator.speed = 0** 프리즈 (대시와 동일, 점멸 동안 유지)
- **점멸 중 공격 불가** (`WeaponCtrl.enabled = false`)

---

### Milestone 3 — 전투 보조 시스템

#### M3-1. ProjectileBase + 사거리 제한
- `ProjectileBase` 추상 클래스로 `ArrowProjectile` / `MagicProjectile` 공통 로직 통합
- 발사 좌표 저장 → `sqrMagnitude` 기반 10칸(worldUnit) 초과 시 풀 반환
- `Instantiate` / `Destroy` 금지 → Object Pool 연동

#### M3-2. 완드 마나 소모 🐛 (버그 확인됨)
- **현재 버그:** 마나 0에서도 마법탄 발사 가능
- `WandBehaviour.BeginAttack()` 에서 `StatSystem.HasEnoughMana(5)` 체크 추가
- 통과 시 `StatSystem.ConsumeMana(5)` 호출 후 발사
- 마나 부족 시 발사 불가

#### M3-3. WeaponSlotManager — E키 무기 교체
- `E` 키로 슬롯 1↔2 즉시 토글
- 최대 2슬롯 제한 강제 (현재 숫자키 1~9 무제한 → 정리)
- `PlayerWeaponController`와 분리하여 SRP 유지

#### M3-4. HealingSystem — F키 물약
- `F` 키 입력 → 인벤토리에서 `ConsumableData` 슬롯 탐색
- `HealthSystem.Heal(_HealAmount)` 호출
- 수량(`Item._CurrentCount`) 차감
- 회복 VFX (간단한 파티클 또는 플래시)

---

### Milestone 4 — 몬스터 AI 및 월드

#### M4-1. MonsterBase
- `CharacterBase` 상속 → `HealthSystem` / `StatSystem` 자동 포함
- `ScriptableObject` 기반 몬스터 스탯 데이터 분리

| 구분 | HP | ATK | 마법ATK |
|---|---|---|---|
| 일반몹 | 5 | 3 | ATK 동일 |
| 보스몹 | 20 | 10 | ATK 동일 |

#### M4-2. Monster AI (Behaviour Tree)
```
Root
├── 사망 → DeathState
├── 플레이어 감지 범위 내?
│   ├── Yes → 추적 (벡터 방향 이동)
│   │   └── 공격 범위 내? → 공격 (애니메이션 딜레이 + 쿨다운 분리)
│   └── No  → 배회 (Idle, 랜덤 방향 이동)
```
- 거리 계산: `sqrMagnitude` 사용 (Magnitude 금지)
- `Find` 계열 금지 → 플레이어 참조는 `[SerializeField]` 또는 이벤트로 주입

#### M4-3. MonsterManager
- 첫 처치 몹 기록 → 능력치 보너스 지급 이벤트 발행
- 몬스터 생성 / 파괴: Object Pool 필수
- 사망 시 `ItemData` 기반 필드 드롭 아이템 생성

---

### Milestone 5 — 인벤토리 / 아이템 시스템 완성

#### M5-1. 인벤토리 슬롯 간 데이터 이동
- `OnDrop` 에서 두 슬롯의 `Item` 데이터 교환
- 같은 타입 아이템 겹치기: 수량(`_CurrentCount`) 합산

#### M5-2. 필드 드롭 아이템
- 몬스터 사망 시 필드에 픽업 오브젝트 생성 (Object Pool)
- 플레이어 접근 자동 픽업 또는 `F`키 수동 줍기

#### M5-3. WeaponSlotManager ↔ 인벤토리 연동
- `WeaponData`를 무기 슬롯에 드롭 시 자동 장착
- `M3-3` 완료 이후 진행

---

### Milestone 6 — 월드 연결 및 씬 정리

#### M6-1. 포탈 씬 전환
- `PortalController`에 `OnTriggerEnter2D` 추가
- 플레이어 접촉 시 다음 구역으로 씬 / 레벨 전환
- 맵 생성 단계와 포탈 배치 위치 연동

#### M6-2. Sort Layer 정리
현재 커밋 메모: "임의로 숫자로 맞춰놓음" → 공식 체계 수립 필요

| Sort Layer | 대상 |
|---|---|
| Background | 타일맵 바닥 |
| Obstacle | 나무, 돌 등 오브젝트 |
| Enemy | 몬스터 |
| Player | 플레이어 |
| Projectile | 화살, 마법탄 |
| VFX | 이펙트, 폭발 |
| UI | HUD, 인벤토리 |

---

## 6. 우선순위 요약

| 순서 | 작업 | 이유 |
|---|---|---|
| 1 | M3-1 사거리 제한 + ProjectileBase | 전투 밸런스 직결, 코드 구조 정리 |
| 2 | M3-2 완드 마나 소모 | StatSystem API 이미 준비됨, 변경 범위 최소 |
| 3 | M3-3 E키 무기 교체 | To-Do 명시 항목, UX 완성도 |
| 4 | M3-4 F키 물약 | 전투 사이클 완성 |
| 5 | M4 몬스터 AI | 가장 큰 작업 단위, M3 완료 후 착수 |
| 6 | M5 인벤토리 완성 | M4 드롭 아이템과 연동 필요 |
| 7 | M6 포탈·씬·Sort Layer | 월드 흐름 마무리 |
