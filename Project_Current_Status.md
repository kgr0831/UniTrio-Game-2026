# UniTrio-Game-2026 구현 완료 현황

> 최종 업데이트: 2026-04-06

---

## ✅ Milestone 1 — 공통 기반 아키텍처 및 스탯 시스템

| 파일 | 내용 |
|---|---|
| `Core/IDamageable.cs` | 피격 가능 인터페이스 (`IsAlive`, `TakeDamage`) |
| `Core/ISkill.cs` | ISkill + ISkillUser 인터페이스 |
| `Core/IBonusProvider.cs` | 아이템·버프 보너스 인터페이스 (Atk/Def/Speed/Mana) |
| `Core/HealthSystem.cs` | HP 관리, 피격 플래시 (MaterialPropertyBlock, 타이머 방식) |
| `Core/StatSystem.cs` | 스탯 합산, 마나 자연 회복, IBonusProvider 등록/해제 |
| `Core/DamageCalculator.cs` | 정적 데미지 공식 유틸 |
| `Core/LivingEntity.cs` | 모든 생명체 추상 기반, IsInvincible 무적 플래그 |
| `Core/CharacterBase.cs` | 스탯 추가 추상 기반 |
| `Player/PlayerEntity.cs` | CharacterBase 상속, KarmaHandler 연동, ISkillUser |
| `Player/KarmaHandler.cs` | 사망 시 카르마 +1, 피해 1점당 10% 증폭 |
| `Enemy/Enemy.cs` | LivingEntity 상속, 접촉 데미지 (0.5s 쿨다운 타이머) |

**데미지 공식:**
- 주는 데미지: `(PlayerAtk + WeaponBaseDmg) × 1.0`
- 받는 데미지: `(rawDamage - Def) ÷ 피해경감 × KarmaMultiplier`
- KarmaMultiplier: `1.0 + (KarmaPoints × 0.1)`

---

## ✅ Milestone 2 — 플레이어 FSM 및 대시

| 파일 | 내용 |
|---|---|
| `Player/PlayerMovement.cs` | WASD 이동, StatSystem.TotalMoveSpeed 연동, MoveInput/FacingDirection 공개 |
| `Player/FSM/PlayerState.cs` | FSM 추상 베이스 |
| `Player/FSM/PlayerStateMachine.cs` | FSM 코디네이터, HealthSystem 이벤트 구독 |
| `Player/FSM/States/IdleState.cs` | 대기 상태 |
| `Player/FSM/States/WalkState.cs` | 이동 상태 |
| `Player/FSM/States/HitState.cs` | 피격 스태거 0.2초 |
| `Player/FSM/States/DashState.cs` | 대시 중 무적·Enemy 레이어 통과·무기 잠금 |
| `Player/FSM/States/DeathState.cs` | 사망 후 입력 전체 잠금 |
| `Player/FSM/States/CutsceneState.cs` | 컷씬 전용 잠금 |
| `Player/DashHandler.cs` | Space 대시, 쿨다운, 입력 예약 방지, 공격 중 대시 불가 |

**FSM 상태 전이:**
```
Idle ↔ Walk → Dash (Space, 공격 중 불가)
Idle / Walk → Hit (피격, 대시 중 무시)
Any → Death (HP = 0)
```

---

## ✅ 무기 시스템

| 파일 | 내용 |
|---|---|
| `Player/Weapon/WeaponBehaviourBase.cs` | 모든 무기 추상 기반 (IsAttacking, ComboWindow 등) |
| `Player/PlayerWeaponController.cs` | 커서 추적·피봇 회전·콤보 버퍼링·무기 교체 (숫자키) |
| `Player/Weapon/SwordBehaviour.cs` | 2타 콤보, Y축 반전 |
| `Player/Weapon/SpearBehaviour.cs` | 직선 찌르기 |
| `Player/Weapon/BowBehaviour.cs` | 우클릭 차징, 차징 비율별 데미지·속도 보간 |
| `Player/Weapon/WandBehaviour.cs` | 마법탄 발사 (마나 소모 미구현 → M3) |
| `Player/Weapon/SwordHitbox.cs` | OnTriggerEnter2D 피격 판정, DamageCalculator 연동 |
| `Player/Weapon/ArrowProjectile.cs` | 화살 발사체 (사거리 제한 미구현 → M3) |
| `Player/Weapon/MagicProjectile.cs` | 마법탄 발사체 |
| `Player/Weapon/ExplosionEffect.cs` | 범위 폭발 (NonAlloc OverlapCircle) |

---

## ✅ MapGen 파트 (MergeBattle-MapGen 브랜치)

| 파일 | 내용 |
|---|---|
| `MapGenerator.cs` | 펄린 노이즈·시드 기반 맵 생성, 듀얼 그리드 타일맵, 청크 오브젝트 풀링 |
| `FogSetup.cs` / `CreateVisionTexture.cs` | Vision 레이어 기반 전장의 안개, TrailRenderer 탐험 기록 |
| `MinimapFollow.cs` | 플레이어 위치 기반 미니맵 갱신 |
| `PortalController.cs` | 포탈 파티클 연출·확장 애니메이션 (진입 로직 미구현 → M6) |

---

## ✅ 데이터 / 인벤토리 시스템 (MergeBattle-MapGen 브랜치)

| 파일 | 내용 |
|---|---|
| `Data/ItemData.cs` | abstract ScriptableObject. WeaponData / ConsumableData / IngredientData 포함 |
| `Data/Item.cs` | 아이템 인스턴스 래퍼 (동적 수량·티어), ItemType 열거형 |
| `Data/ItemDatabase.cs` | 전체 아이템 마스터 테이블, ID 기반 조회 |
| `DataManager.cs` | 딕셔너리 캐시 (GetItem / GetSkill 분리) |
| `InventoryGenerator.cs` | 행·열 동적 슬롯 생성 |
| `InventorySlot.cs` | 드래그 비주얼, 슬롯 갱신 (`_Icon` API 사용) |
| `DragManager.cs` | 마우스 추적 드래그 아이콘 |
| `QuickSlotManager.cs` | 1~8키, 타입별 사용 분기 (ConsumableData / WeaponData) |

---

## ✅ 머지 버그 수정 (2026-04-06)

| 항목 | 내용 |
|---|---|
| 중복 클래스 제거 | `Scripts/ItemData.cs`, `Interface/ItemDatabase.cs` 구버전 삭제 |
| API 불일치 수정 | `DataManager` (`item.id` → `item._Id`), `InventorySlot` (`currentData.Icon` → `_Icon`) |
| `QuickSlotManager` | `Use()` 호출 → 타입별 분기로 교체 (SRP 준수) |
| Inspector 가시성 | `_currentHealth`, `_isAlive`, `_currentMana`, `_currentStateName` SerializeField 노출 |
| 대시 입력 예약 버그 | 쿨다운/대시 중 Space 버퍼링 방지 |
| 공격·대시 양방향 잠금 | 공격 중 대시 불가 (`WeaponCtrl.IsAttacking` 체크) |
| 대시 Enemy 통과 | `Rigidbody2D.excludeLayers`로 Enemy(8번) 레이어 제외 |

---

## 🔄 진행 중 / 예정

| 단계 | 내용 | 상태 |
|---|---|---|
| 선행 0 | ItemData 계층 완성 (ArmorData, AccessoryData, SkillData 편입) | 🔄 진행 중 |
| 선행 1 | WeaponSlotManager (E키 스왑) | ⏳ 예정 |
| 선행 2 | SkillSlotManager (Q키 시전) | ⏳ 예정 |
| M3 | ProjectileBase·사거리 제한·마나 소모·HealingSystem | ⏳ 예정 |
| M4 | MonsterAI | ⏳ 예정 |
| M5 | 인벤토리 완성·필드 드롭 아이템 | ⏳ 예정 |
| M6 | 포탈 씬 전환·Sort Layer 정리 | ⏳ 예정 |
