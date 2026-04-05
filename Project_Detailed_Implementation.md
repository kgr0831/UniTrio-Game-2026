# 🎮 UniTrio-Game-2026 초정밀 마스터 구현 계획서

본 문서는 `To-Do.md`에 명시된 68개 모든 항목을 분석하여 **논리적 선후 관계**, **최적화 전략**, **유지보수 가이드**를 포함한 최종 마스터 플랜입니다.

---

## 📅 1. 개발 로드맵 및 상세 순서 (Development Order)

개발은 **[기반 엔진 -> 플레이어 코어 -> 전투 시스템 -> 몬스터/월드]** 순으로 진행됩니다.

### Milestone 1: 코어 스택 및 데미지 엔진 (Foundation & Stats)
*모든 상호작용의 수치를 결정하는 두뇌를 먼저 구축합니다.*
1. **`PlayerStats.cs`**: `To-Do (4-13)`
    - 기본값: HP 100, Mana 50, ATK 30, DEF 0, AS 1.0, Speed 100, MagicATK 35.
    - **자연 회복**: `float ManaRegenStat` (기본 1.0)에 따라 초당 마나 회복 로직.
2. **`StatModifier.cs`**: `To-Do (14-18)`
    - 장신구, 물약 등으로 인해 실시간으로 변화하는 보정값 계산기.
3. **`DamageCalculator.cs`**: `To-Do (20-22)`
    - 정적 라이브러리화: `FinalDmg = (Atk + ItemAtk) * Multiplier` / `(Dmg - Def) / Red`.
4. **`KarmaHandler.cs`**: `To-Do (19)`
    - 사망 시(`To-Do 49`) 카르마 수치 증가 및 피격 데미지 증폭(1점당 10%) 로직.
5. **`HealthSystem.cs`**: 공통 체력 시스템 (Player/Monster 겸용).

### Milestone 2: 플레이어 상태 머신 및 조작 (FSM & Input)
*WASD 및 마우스 조작과 7가지 상태를 유기적으로 연결합니다.*
6. **`PlayerInputManager.cs`**: `To-Do (4, 6, 53, 61, 63)`
    - 키 매핑: `WASD`(이동), `Space`(대쉬), `E`(무기스왑), `LMB`(공격), `F`(힐물약).
7. **`PlayerStateMachine.cs`**: `To-Do (45-52)`
    - `Idle`, `Walk`, `Death`, `Cutscene`, `Dash`, `Hit` 상태 전이 관리.
8. **`DashHandler.cs`**: `To-Do (53-56)`
    - **판정**: 대쉬 중 무적 설정 및 VFX 생성.
    - **Lock**: 대쉬 중 공격 명령 차단 기능.
9. **`WeaponRotationSystem.cs`**: `To-Do (4)`
    - 무기 피봇이 커서 방향을 실시간 추적하고 회전하는 로직.

### Milestone 3: 전투 시스템 고도화 (Combat Mechanics)
*무기 스왑과 발사체, 자원 소모의 디테일을 구현합니다.*
10. **`WeaponSlotManager.cs`**: `To-Do (60-62)`
    - 2개의 슬롯 제한 및 `E` 키 토글 스위칭.
11. **`ProjectileHandler.cs`**: `To-Do (67-68)`
    - **10칸 거리 제한**: `Vector3.sqrMagnitude` 기반 자동 제거.
    - **마나 소모**: 지팡이 시전 시 `PlayerStats.CurrentMana -= 5`.
12. **`HealingSystem.cs`**: `To-Do (63-65)`
    - `F` 키 단축키, 물약 데이터 차감 및 VFX 재생 연동.

### Milestone 4: 몬스터 AI 및 시스템 (Monster AI)
*전투 상대를 지능화하고 보상 체계를 완성합니다.*
13. **`MonsterManager.cs`**: `To-Do (23-25, 28)`
    - 처치 카운팅 및 **최초 처치 시 영구 스탯 보너스** 부여 시스템.
14. **`MonsterAI (BT/Pathfinding)`**: `To-Do (30-41)`
    - 행동 트리: `Idle/Wander -> Detect -> Track -> Attack`.
    - 공격 텀(Cool) 및 애니메이션 딜레이 동기화.
15. **`MonsterStats`**: `To-Do (26-27)`
    - 일반몹(HP 5, ATK 3) / 보스몹(HP 20, ATK 10) 프리팹 구축.
16. **`MonsterDeathSystems`**: `To-Do (42-44, 59)`
    - 사망 시 아이템 드롭 및 필드 드롭 아이템(Physics) 관리.

---

## 🛠️ 2. 기술적 구현 및 성능 최적화 (Technical Guide)

### 2.1 레이어 관리 (Physics Layers Strategy)
현재 `Enemy` 레이어 외에 아래와 같은 레이어를 추가 배분하여 성능을 최적화합니다.
- `Player`: 플레이어 히트박스.
- `Projectile_P`: 플레이어 발사체 (Enemy와만 충돌).
- `Projectile_E`: 몬스터 발사체 (Player와만 충돌).
- `Obstacle`: 발사체를 파괴하는 지형지물.

### 2.2 코드 효율성 및 유지보수
- **Event-Driven Stats**: 스탯이 변할 때만 `Action` 이벤트를 호출하여 UI를 업데이트 (CPU 점율 효율화).
- **Object Pooling**: 화살과 이펙트는 가비지 컬렉션을 피하기 위해 사전에 풀링.
- **AI 토큰 최적화**: 클래스당 150줄 내외로 모듈화하여 AI(Antigravity)의 문맥 파악 및 수정 정확도를 높임.

---

**위의 68개 전 항목 계획이 모든 요구사항을 포함하고 있는지 확인 부탁드립니다.** 승인해 주시면 **Milestone 1**의 스탯 데이터 시스템부터 구현을 시작하겠습니다.
