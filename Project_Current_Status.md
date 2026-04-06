# UniTrio-Game-2026 구현 완료 현황

> 최종 업데이트: 2026-04-07

---

## ✅ Milestone 1 — 공통 기반 아키텍처 및 스탯 시스템 (테스트 완료)

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

---

## ✅ Milestone 2 — 플레이어 FSM 및 대시 (테스트 완료)

| 파일 | 내용 |
|---|---|
| `Player/PlayerMovement.cs` | WASD 이동, StatSystem.TotalMoveSpeed 연동, MoveInput/FacingDirection 공개 |
| `Player/FSM/PlayerStateMachine.cs` | FSM 코디네이터, HealthSystem 이벤트 구독, AfterimagePool 자동 추가 |
| `Player/FSM/States/IdleState.cs` | 대기 상태 |
| `Player/FSM/States/WalkState.cs` | 이동 상태 |
| `Player/FSM/States/HitState.cs` | 피격 스태거 0.2초 — **변경 예정** (아래 참고) |
| `Player/FSM/States/DashState.cs` | 업그레이드 분기, Animator 프리즈, 잔상 VFX 연동 |
| `Player/FSM/States/DeathState.cs` | 사망 후 입력 전체 잠금 |
| `Player/DashHandler.cs` | Space 대시, 쿨다운, 입력 예약 방지, `IsUpgraded` 분기 |
| `Player/DashAfterimagePool.cs` | 잔상 오브젝트 풀 (30개, 0.006s 간격) |
| `Player/AfterimageGhost.cs` | 잔상 1개, 0.3초 alpha 0.85→0 페이드 |

**FSM 상태 전이:**
```
Idle ↔ Walk → Dash (Space, 공격 중 불가)
Idle / Walk → Hit (피격, 대시 중 무시)
Any → Death (HP = 0)
```

**대시 업그레이드 분기 (`DashHandler.IsUpgraded`):**
```
false (기본): 빠른 이동 + 파란 틴트만. 무적·충돌무시·잔상 없음.
true  (업그레이드): 무적 + Enemy 레이어 통과 + 잔상 VFX (스킬트리 연동 예정)
```

---

## ✅ 무기 시스템 (구현 완료, 일부 변경 예정)

| 파일 | 내용 |
|---|---|
| `Player/Weapon/WeaponBehaviourBase.cs` | 모든 무기 추상 기반 |
| `Player/PlayerWeaponController.cs` | 커서 추적·피봇 회전·콤보 버퍼링·숫자키 무기 교체 — **변경 예정** |
| `Player/Weapon/SwordBehaviour.cs` | 2타 콤보, Y축 반전 |
| `Player/Weapon/SpearBehaviour.cs` | 직선 찌르기 |
| `Player/Weapon/BowBehaviour.cs` | 우클릭 차징 |
| `Player/Weapon/WandBehaviour.cs` | 마법탄 발사 — **버그: 마나 0에서도 발사됨 (M3에서 수정)** |
| `Player/Weapon/SwordHitbox.cs` | OnTriggerEnter2D 피격 판정, DamageCalculator 연동 |
| `Player/Weapon/ArrowProjectile.cs` | 화살 발사체 (사거리 제한 미구현 → M3) |
| `Player/Weapon/MagicProjectile.cs` | 마법탄 발사체 |
| `Player/Weapon/ExplosionEffect.cs` | 범위 폭발 (NonAlloc OverlapCircle) |
| `Player/WeaponSlotManager.cs` | E키 2슬롯 스왑 — **씬 UI 연결 작업 필요** |
| `Player/SkillSlotManager.cs` | Q키 스킬 시전 — **씬 컴포넌트 배치 필요** |

---

## ✅ ItemData 계층 (구현 완료, 에셋 생성 테스트 완료)

| 파일 | 내용 |
|---|---|
| `Data/ItemData.cs` | abstract SO. WeaponData / ConsumableData / IngredientData 포함 |
| `Data/ArmorData.cs` | IBonusProvider 구현 |
| `Data/AccessoryData.cs` | IBonusProvider 구현 |
| `Data/SkillData.cs` | ItemData 편입, ManaCost / Cooldown |
| `Data/BuildingData.cs` | 껍데기 (M7 예정) |
| `Data/Item.cs` | 인스턴스 래퍼, ItemType 열거형 |
| `Data/ItemDatabase.cs` | ID 기반 조회, 타입 필터링 |
| `DataManager.cs` | 딕셔너리 캐시 (GetItem / GetSkill) |

> **명명 규칙:** 모든 public 필드 PascalCase 적용 완료 (코드 리뷰 반영)

---

## ✅ MapGen 파트 (테스트 완료)

| 파일 | 내용 |
|---|---|
| `MapGenerator.cs` | 펄린 노이즈·시드 기반 맵 생성, 듀얼 그리드, 청크 풀링 |
| `FogSetup.cs` / `CreateVisionTexture.cs` | 전장의 안개, TrailRenderer 탐험 기록 |
| `MinimapFollow.cs` | 미니맵 갱신 |
| `PortalController.cs` | 파티클 연출 (진입 로직 미구현 → M6) |

---

## 🔧 변경 필요 사항 (테스트에서 발견)

### M2-06 HitState 개선
현재: 0.2초 이동·공격 불가 스태거만 존재  
**변경 내용:**
- Cinemachine 카메라 셰이크 추가
- 플레이어 스프라이트 **붉은 색 점멸** (기존 흰색 → 빨간색)
- 점멸 중 Animator.speed = 0 (대시와 동일한 프리즈)
- 점멸 중 공격도 불가능하게 변경

### WeaponSlotManager 씬 연결 필요
- 숫자키(1~9) 무기 교체 기능 제거 → E키 2슬롯만 허용
- 무기 최대 2개 제한 강제
- 씬에 UI 배치 완료 → **에디터로 연결 작업 필요**

### SkillSlotManager 씬 배치 필요
- 어떤 컴포넌트를 어디에 배치할지 가이드 작성 필요
- 또는 Editor 설정 스크립트로 자동화

### WandBehaviour 버그
- 마나가 0이어도 마법탄 발사됨
- M3 완드 마나 소모 구현 시 수정 예정

---

## 🔄 진행 중 / 예정

| 단계 | 내용 | 상태 |
|---|---|---|
| 선행 작업 | HitState 개선 (카메라 셰이크·붉은 점멸·프리즈) | 🔧 수정 필요 |
| 선행 작업 | WeaponSlotManager 씬 UI 에디터 연결 | 🔧 수정 필요 |
| 선행 작업 | SkillSlotManager 씬 배치 가이드 / 자동화 | 🔧 수정 필요 |
| M3 | ProjectileBase·사거리 제한·완드 마나 소모·HealingSystem | ⏳ 예정 |
| M4 | MonsterAI (Behaviour Tree) | ⏳ 예정 |
| M5 | 인벤토리 완성·필드 드롭 아이템 | ⏳ 예정 |
| M6 | 포탈 씬 전환·Sort Layer 정리 | ⏳ 예정 |
