# UniTrio 테스트 체크리스트

> **최종 업데이트:** 2026-04-06
> **테스트 씬:** `Assets/Scenes/AnimMakingScene2.unity` (Battle)

---

[T] = 확인 완료 & 문제 없음
[F] = 버그 발생
[ ] = 미테스트
[E] = 수정 사항 있음 

---

## 📦 사전 준비 — Inspector 기본값 확인

| 컴포넌트 | 항목 | 예상값 |
|---|---|---|
| `Health System` | `_maxHp` | 100 |
| `Stat System` | `_baseAtk` / `_baseDef` / `_baseMoveSpeed` | 5 / 0 / 5 |
| `Stat System` | `_baseMana` / `_manaRegen` | 100 / 1 |
| `Karma Handler` | `_maxKarma` | 10 |
| `Dash Handler` | `_dashForce` / `_dashDuration` / `_cooldown` | 14 / 0.18 / 1 |
| `Enemy` | `_contactDamage` / `_contactDamageCooldown` | 1 / 0.5 |

---

## 🎮 대시 시스템

### [DA-01] 애니메이션 프리즈 (공통)
**방법:** Walk 중 또는 Idle 상태에서 Space 입력

- [T] 대시 시작 순간 애니메이션 프리즈 (현재 프레임 고정)
- [T] 대시 종료 후 Walk / Idle 애니메이션 즉시 재개

---

### [DA-02] 기본 대시 (`Is Upgraded = false`)
**방법:** `DashHandler.Is Upgraded` 체크 해제 후 Space

- [T] 파란 색조 표시
- [T] 좀비 충돌 시 HP 감소 (충돌 무시 없음)
- [T] 좀비 접촉 피격 발생 (무적 없음)
- [T] 잔상 VFX 없음

---

### [DA-03] 업그레이드 대시 — 무적 및 충돌 무시 (`Is Upgraded = true`)
**방법:** `DashHandler.Is Upgraded` 체크 후 좀비를 향해 Space

- [T] 좀비 통과 가능 (충돌 무시)
- [T] 대시 중 좀비 접촉 HP 감소 없음 (무적)
- [T] 대시 종료 후 충돌·무적 복원

---

### [DA-04] 업그레이드 대시 — 잔상 VFX (`Is Upgraded = true`)
**방법:** `DashHandler.Is Upgraded` 체크 후 Space

- [T] 대시 경로에 잔상 생성 (최대 30개)
- [T] 잔상은 원본 스프라이트 색상, alpha 0.85에서 0.3초 페이드아웃
- [T] 대시 종료 후 신규 잔상 생성 중단, 기존 잔상 자연 소멸
- [T] 연속 대시 시 NullRef 없음 (풀 순환 재사용)

---

### [DA-05] 런타임 업그레이드 토글 (스킬트리 연동 대비)
**방법:** Play 중 Inspector에서 `Is Upgraded` 토글

- [T] false → true: 다음 대시부터 업그레이드 동작
- [T] true → false: 다음 대시부터 기본 동작

---

## ⚙️ Milestone 1 — 공통 아키텍처

### [M1-01] HealthSystem
- [T] Play 후 `_currentHealth` = `_maxHp` (100)
- [T] `_isAlive` = true (Inspector에서 확인)

### [M1-02] StatSystem 마나 회복
- [T] Play 후 1초마다 `_currentMana` 1씩 증가
- [T] `_baseMana`(100) 초과 없음

### [M1-03] 검 데미지 공식
- [T] `_baseAtk 5` + `SwordHitbox._baseDamage 5` → 좀비 피격 데미지 텍스트 **10**

### [M1-04] 피격 플래시
- [T] 검 타격 시 좀비 흰색 점멸 (~0.15초)
- [T] 연속 타격 시 점멸 리셋 (중첩 없음)

### [M1-05] 좀비 접촉 데미지
- [T] 좀비 접촉 시 플레이어 HP 감소 (0.5초 쿨다운)

---

## 🕹️ Milestone 2 — FSM 및 대시

### [M2-01] FSM 상태 표시
- [T] `Player State Machine._currentStateName` = "IdleState" (Play 직후)

### [M2-02] Idle ↔ Walk 전환
- [T] WASD 입력 시 "WalkState", 뗄 시 "IdleState"

### [M2-03] 대시 기본 동작
- [T] Space → 커서/이동 방향으로 빠른 이동 (~0.18초)
- [T] 대시 중 파란 색조, 종료 후 흰색 복귀

### [M2-04] 대시 입력 예약 방지
- [T] 쿨다운 중 Space 연타 → 쿨다운 종료 후 자동 발동 없음

### [M2-05] 공격 ↔ 대시 양방향 잠금
- [T] 공격 중 Space → 대시 발동 안 됨
- [T] 대시 중 좌클릭 → 공격 발동 안 됨

### [M2-06] HitState 스태거
- [E] 좀비 피격 순간 0.2초 이동·공격 불가 후 자동 복귀 -> 시네머신을 이용한 카메라 쉐이킹 약간 + 플레이어는 붉은 색 점멸, 공격도 잠시 불가능 하도록 바뀌기, 점멸중에는 대쉬와 똑같이 스프라이트 고정

---

## 🗂️ 선행 — ItemData / 슬롯 시스템 *(씬 셋업 후 테스트)*

### [S-01] ItemData ScriptableObject 생성
- [T] `Create → Data/Items/Weapon` → `Damage`, `AttackSpeed` 필드 표시
- [T] `Create → Data/Items/Armor` → `DefBonus`, `AtkBonus` 등 표시, `Type = Armor` 자동 설정
- [T] `Create → Data/Items/Skill` → `ManaCost`, `Cooldown` 표시, `Type = Skill` 자동 설정

### [S-02] WeaponSlotManager — E키 스왑 *(컴포넌트 씬 배치 필요)*
- [E] E키 입력 시 활성 무기 0 ↔ 1 전환 -> 더이상 숫자키로 무기 변경 불가능 / 무기 최대 갯수 2개 / 씬에 ui 배치 완료, 에디터로 연결해주세요.
- [E] 공격 중 E키 → 스왑 가능 여부 확인 -> 더이상 숫자키로 무기 변경 불가능 / 무기 최대 갯수 2개 / 씬에 ui 배치 완료, 에디터로 연결해주세요.

### [S-03] SkillSlotManager — Q키 시전 *(컴포넌트 씬 배치 필요)*
- [E] Q키 → Console `[SkillSlot] ... 시전!` 출력, 마나 감소 -> 컴포넌트 배치 가이드 필요, 어떤 컴포넌트를 어디에 어떻게 배치해야하는지 알아야함. 또는 MCP로 사용 가능하다면 ㄱㄱ
- [F] 마나 부족 시 시전 불가 -> 0이어도 지팡이 사용이됨, 스킬은 컴포넌트 배치 안해서 확인 불가능.
- [E] 쿨다운 중 Q키 재입력 무효 -> 컴포넌트 배치 가이드 필요, 어떤 컴포넌트를 어디에 어떻게 배치해야하는지 알아야함. 또는 MCP로 사용 가능하다면 ㄱㄱ

---

## 🐛 버그 리포트 양식

```
[버그 ID] 제목
- 발생 조건:
- 재현 순서:
- 예상 결과:
- 실제 결과:
- Console 로그:
```
