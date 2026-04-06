# 🎮 UniTrio-Game-2026 초정밀 마스터 구현 계획서 (Base & Interface 강화판)

본 문서는 `To-Do.md`의 모든 항목과 **공통 부모 클래스 아키텍처**, **미래 확장(스킬 등)을 위한 인터페이스**, **유니티 최적화 가이드**를 결합한 최종 프로젝트 명세서입니다.

---

## 🏛️ 1. 핵심 아키텍처 설계 (Common Base & Interface)

중복 코드를 줄이고 다형성을 활용하기 위해 **상속(Inheritance)**과 **인터페이스(Interface)**를 프로젝트의 근간으로 설계합니다.

### 1.1 공통 부모 클래스 (Base Classes)
- **`LivingEntity` (Abstract)**: 체력(HP), 피격(TakeDamage), 사망(Death), 히트 이펙트 등 모든 생명체의 기본 속성을 관리합니다. (Player, Monster 공통)
- **`CharacterBase` (Abstract) inherits `LivingEntity`**: `LivingEntity`를 확장하며, 공격력/방어력/이동속도 등 기초 스탯과 이동 제어 로직의 기본 틀을 제공합니다.
- **`ProjectileBase`**: 모든 원거리 발사체(화살, 마법탄)의 공통 로직(사거리 제한, 관통 여부, 충돌 파괴)을 관리합니다.

### 1.2 핵심 인터페이스 (Core Interfaces)
- **`IDamageable`**: 피해를 입을 수 있는 모든 객체(적, 보스, 상자 등)에 적용됩니다.
- **`ISkillUser`**: 스킬을 장착하고 사용할 수 있는 주체(플레이어, 보스 등)를 정의합니다.
- **`ISkill`**: 모든 스킬의 인터페이스입니다. (시전 비용, 쿨타임, 효과 정의 등)
- **`IBonusProvider`**: 장착 아이템이나 버프가 캐릭터 스탯에 실시간 보너스를 제공하는 표준 방식입니다.

---

## 📅 2. 상세 개발 로드맵 (Development Order)

### Milestone 1: 공통 기반 아키텍처 및 스탯 시스템 (Foundation)
1. **`Core Interfaces` 구축**: `IDamageable`, `ISkill`, `IBonusProvider`.
2. **`LivingEntity` & `CharacterBase` 구현**:
    - **SRP**: 체력 로직(`HealthSystem`)과 스탯 로직(`StatSystem`)을 분리하여 부착형 컴포넌트로 설계.
3. **`PlayerStats` 및 데미지 공식**: `To-Do (4-13)`
    - **공식**: `(Atk + ItemAtk) * Multiplier` / `(Dmg - Def) / Red`.
    - **자연 회복**: `float ManaRegenStat` 기반 마나 회복 시스템 (초당 1회복 가정).
4. **`KarmaHandler`**: `To-Do (19)`
    - 사망 시 카르마 증가 및 피격 데미지 증폭(1점당 10%) 로직.

### Milestone 2: 플레이어 상태 머신 및 조작 (Player FSM)
5. **`PlayerStateMachine`**: `CharacterBase`를 상속받아 구현. `To-Do (45-52)`
    - `Idle`, `Walk`, `Death`, `Cutscene`, `Dash`, `Hit` 상태 전이.
6. **`DashHandler`**: `To-Do (53-56)`
    - 무적 판정, VFX 연동, **공격 잠금(Lock) 시퀀스**.
7. **`WeaponRotation`**: 무기 피봇이 커서 방향을 실시간 추적하고 회전하는 시스템.

### Milestone 3: 전투 및 보조 시스템 (Combat)
8. **`WeaponSlotManager`**: `To-Do (60-62)`
    - 2개의 슬롯 제한 및 `E` 키 토글 스위칭.
9. **`ProjectileSystem`**: `ProjectileBase`를 상속받아 화살/지팡이 구현. `To-Do (67-68)`
    - **10칸 사거리 제한**: `sqrMagnitude` 기반 자동 제거.
    - **마나 소모**: 지팡이 시전 시 `Mana -= 5`.
10. **`HealingSystem`**: `To-Do (63-65)`
    - `F` 키 단축키 및 물약 사용 연출.

### Milestone 4: 몬스터 AI 및 월드 (Monster AI)
11. **`MonsterBase`**: `CharacterBase`를 상속받아 구현. `To-Do (26-27)`
    - 일반몹(HP 5, ATK 3) / 보스몹(HP 20, ATK 10) 사양 적용.
12. **`MonsterAI (BT)`**: `To-Do (30-41)`
    - 행동 트리: 배회 -> 감지 -> 추적 -> 공격 -> 쿨다운 관리.
13. **`MonsterManager`**: `To-Do (23-25, 28)`
    - 최초 처치 보상 수집 및 몬스터 전역 사망 관리 매니저.

---

## 🛠️ 3. 기술적 구현 및 최적화 전략 (Technical Spec)

### 3.1 성능 및 최적화 (Unity Best Practices)
- **오브젝트 풀링 (Object Pooling)**: 발사체와 이펙트는 가상 풀러를 통해 재사용 (GC 최소화).
- **이벤트 중심 UI (Event-Driven)**: `LivingEntity`의 HP가 변할 때만 이벤트를 발생시켜 UI 갱신.
- **레이어 매트릭스 (Layer Matrix)**: `Player`, `Enemy`, `Projectile_P/E`, `Obstacle` 레이어를 통한 물리 연산 최적화.

### 3.2 단일 책임 원칙 (SRP) 기반 설계
- 모든 클래스는 150~200줄 내외로 모듈화하여, AI(Antigravity, Claude Code) 및 코딩 참여자가 기능을 즉시 파악할 수 있도록 분리합니다.

---

**위의 공통 아키텍처 설계가 사용자의 프로젝트 비전과 일치합니까?** 승인해 주시면 **Milestone 1**의 추상 클래스와 인터페이스부터 순차적으로 구현에 착수하겠습니다.
