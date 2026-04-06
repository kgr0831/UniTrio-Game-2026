# 🧪 UniTrio 테스트 체크리스트

> **대상 씬:** `Assets/Scenes/AnimMakingScene2.unity`
> **현재 날짜:** 2026-04-06
> **테스트 상태:** 전체 미진행 (0/N)

---

## 📦 사전 준비 (테스트 전 필수 확인)

테스트를 시작하기 전, 아래 Inspector 설정이 정상인지 확인하세요.

### Player GameObject 컴포넌트 체크리스트

| 컴포넌트 | 확인 항목 | 예상값 |
|---|---|---|
| `Health System` | `_maxHp` | 100 |
| `Stat System` | `_baseAtk` / `_baseDef` / `_baseMoveSpeed` | 5 / 0 / 5 |
| `Stat System` | `_baseMana` / `_manaRegen` | 100 / 1 |
| `Karma Handler` | `_maxKarma` | 10 |
| `Dash Handler` | `_dashForce` / `_dashDuration` / `_cooldown` | 14 / 0.18 / 1 |
| `Sword Hitbox` (SwordHitBox 오브젝트) | `_playerEntity` | Player 오브젝트 연결됨 |
| `Bow Behaviour` (Bow 오브젝트) | `_playerEntity` | Player 오브젝트 연결됨 |
| `Wand Behaviour` (Wand 오브젝트) | `_playerEntity` | Player 오브젝트 연결됨 |

### Enemy(zombie) 컴포넌트 체크리스트

| 컴포넌트 | 확인 항목 | 예상값 |
|---|---|---|
| `Health System` | `_maxHp` | 5 |

---

## 🟢 Milestone 1 — 공통 기반 아키텍처 및 스탯 시스템

---

### [M1-01] HealthSystem — 플레이어 HP 초기화 확인

**목적:** `Health System`이 올바르게 초기화되는지 확인합니다.

**방법:**
1. Play 모드 진입
2. Hierarchy에서 `Player` 선택
3. Inspector → `Health System` 컴포넌트 확인
4. Inspector 우측 상단 `⋮` → **Debug** 모드 전환

**확인 항목:**
- [ ] `_maxHp` = 100
- [ ] `_currentHealth` (내부 필드) = 100
- [ ] `IsAlive` = true -> IsAlive가 인스펙터에서 안보임.

---

### [M1-02] HealthSystem — 적 HP 초기화 확인

**방법:**
1. Play 모드 진입
2. Hierarchy에서 zombie 중 하나 선택
3. Inspector → `Health System` 컴포넌트 → Debug 모드

**확인 항목:**
- [ ] `_maxHp` = 5
- [ ] 초기 CurrentHp = 5

---

### [M1-03] StatSystem — 마나 자연 회복 확인

**목적:** 초당 1마나씩 자연 회복되는지 확인합니다.

**방법:**
1. Play 모드 진입
2. `Player` 선택 → Inspector → `Stat System` → Debug 모드
3. `_currentMana` 필드를 실시간으로 관찰 -> _currentMana가 인스펙터에서 안보임
**확인 항목:**
3. `_currentMana` 필드를 실시간으로 관찰 -> _currentMana가 인스펙터에서 안보임
- [ ] 시작 시 `_currentMana` = 100 -> _currentMana가  안보임
- [ ] `_manaRegen` = 1이면 1초마다 숫자가 1씩 증가
- [ ] 최대(`_baseMana` = 100)를 초과하지 않음

> **팁:** `_currentMana`를 임의로 낮추려면 `Stat System._baseMana`를 50으로 줄이고 재진입하세요.

---

### [M1-04] 검 공격 — 데미지 공식 확인

**목적:** `(PlayerAtk + WeaponBaseDmg) * 1.0` 공식이 올바른지 확인합니다.

**기본 예상값:**  
`StatSystem._baseAtk = 5` + `SwordHitbox._baseDamage = 5` = **데미지 10**

**방법:**
1. Play 모드 진입
2. 숫자키 `1`로 검 장착 확인
3. 좀비 근처에서 **좌클릭**
4. 화면에 뜨는 데미지 텍스트 확인
5. 좀비의 `Health System` → `_currentHealth` 감소 확인

**확인 항목:**
- [ ] 데미지 텍스트가 **10** 표시
- [ ] zombie `_currentHealth` 5 → 0 이하로 1~2타 내 사망
- [ ] 사망 시 Console에 `[zombie] 처치됨` 출력

**데미지 공식 변동 테스트:**
- `Stat System._baseAtk`를 **0**으로 변경 → 데미지 텍스트가 **5** (무기 기본값만)
- `Stat System._baseAtk`를 **10**으로 변경 → 데미지 텍스트가 **15**

---

### [M1-05] 검 공격 — 피격 플래시 확인

**목적:** 좀비가 타격받을 때 하얗게 번쩍이는지 확인합니다.

**방법:**
1. Play 모드 진입
2. 검으로 좀비 공격

**확인 항목:**
- [ ] 타격 시 좀비 스프라이트가 흰색으로 **순간 번쩍임**
- [ ] 약 0.15초 후 원래 색으로 복귀
- [ ] 연속 타격 시 점멸이 중첩되지 않고 매 타격마다 리셋됨

---

### [M1-06] 활 공격 — 데미지 공식 확인

**목적:** 활 화살 데미지에 PlayerAtk 보너스가 합산되는지 확인합니다.

**기본 예상값 (좌클릭, 100% 위력):**  
`_maxArrowDamage = 30` + `StatSystem.TotalAtk = 5` = **데미지 35**

**방법:**
1. 숫자키 `2`로 활 장착
2. **좌클릭** (일반 발사) → 데미지 텍스트 확인
3. **우클릭** 0.3초 유지 후 발사 (50% 차징) → 데미지 텍스트 확인
4. **우클릭** 풀 차징(1초) 후 발사 → 데미지 텍스트 확인

**확인 항목:**
- [ ] 일반 발사(100%): 데미지 ≈ 35 (`30 + 5`)
- [ ] 50% 차징: 데미지 ≈ 22~23 (`(5+30) * 0.5 + 5`)
- [ ] `_baseAtk`를 10으로 올리면 모든 경우에서 데미지 +5 증가

---

### [M1-07] 완드 공격 — 마나 소모 없음 확인 (현재)

> **주의:** Milestone 3에서 완드 시전 시 마나 5 소모 예정. 현재는 소모 없음이 정상.

**방법:**
1. 숫자키 `4`로 완드 장착
2. **좌클릭**으로 마법탄 발사
3. `Stat System._currentMana` 확인

**확인 항목:**
- [ ] 발사 후 `_currentMana` 변화 없음 (소모 안 됨 = 정상)
- [ ] 마법탄이 좀비에게 닿으면 폭발 VFX + 데미지 텍스트 표시

---

### [M1-08] KarmaHandler — 사망 후 카르마 증가 확인

**방법:**
1. `Player → Health System._maxHp`를 **1**로 변경
2. Play 모드 진입
3. 좀비에게 접근해 맞기 (데미지 1 이상 받으면 즉사) -> 좀비에게 접근해도 아무런 일이 발생하지 않음.
4. Console 확인

**확인 항목:**
- [ ] Console에 `[Player] 사망 → 카르마 1pt` 출력
- [ ] `Player → Karma Handler` 컴포넌트의 `KarmaPoints` 필드가 **1**로 증가

---

### [M1-09] KarmaHandler — 카르마 피격 증폭 확인

**목적:** 카르마 1pt = 받는 데미지 10% 증가.

**방법:**
1. [M1-08] 상태 유지 (KarmaPoints = 1)
2. `Player → Health System._maxHp`를 **200**으로 변경 후 Play
3. 좀비에게 맞으며 HP 감소량 관찰

> 좀비 공격력은 현재 Enemy 코드에 명시되지 않아 직접 확인 어려울 수 있습니다.
> 대신 아래 **코드 디버그** 방법을 사용하세요.

**코드 디버그 방법:**  
`PlayerEntity.cs`의 `CalculateIncomingDamage`에 임시 로그 추가:
```csharp
protected override float CalculateIncomingDamage(float rawDamage)
{
    float karmaMulti = _karma.GetMultiplier();
    float result = DamageCalculator.CalcDamageTaken(rawDamage, Stats.TotalDef, karmaMulti);
    Debug.Log($"[PlayerEntity] rawDmg={rawDamage} def={Stats.TotalDef} karma={karmaMulti} → final={result}");
    return result;
}
```

**확인 항목:**
- [ ] `KarmaPoints = 0` → Console: `karma=1.0`
- [ ] `KarmaPoints = 1` → Console: `karma=1.1`
- [ ] `KarmaPoints = 5` → Console: `karma=1.5`

---

## 🔵 Milestone 2 — 플레이어 상태 머신 및 대시

---

### [M2-01] FSM — 상태 이름 실시간 확인 설정

**모든 M2 테스트 전 필수 설정입니다.**

**방법:**
1. Play 모드 진입
2. `Player` 선택 → Inspector → `Player State Machine` → Debug 모드
3. `_currentStateName` 필드가 보이는지 확인

**확인 항목:**
- [ ] Play 시작 직후 `_currentStateName` = **"IdleState"**

---

### [M2-02] FSM — Idle ↔ Walk 전환

**방법:**
1. Play 모드 진입, `_currentStateName` 필드 관찰 -> _currentStateName가 인스펙터에서 보이지 않음.
2. WASD 이동 입력

**확인 항목:**
- [ ] 이동 키 누르는 순간 `_currentStateName` = **"WalkState"** -> _currentStateName가 인스펙터에서 보이지 않음.
- [ ] 이동 키 떼는 순간 `_currentStateName` = **"IdleState"** 복귀 -> _currentStateName가 인스펙터에서 보이지 않음.

---

### [M2-03] DashHandler — 기본 대시 동작

**방법:**
1. Play 모드 진입
2. **Space** 키 입력

**확인 항목:**
- [ ] 커서 방향으로 빠르게 이동 (약 0.18초)
- [ ] 대시 중 스프라이트가 **반투명 파란 색조**로 변경
- [ ] 대시 후 스프라이트 **흰색**으로 복귀
- [ ] `_currentStateName` 순서: Idle → **"DashState"** → Idle -> _currentStateName가 인스펙터에서 보이지 않음.

---

### [M2-04] DashHandler — 이동 중 대시 방향 확인

**방법:**
1. `D` 키(오른쪽)를 누르며 이동
2. 이동 중 **Space** 키

**확인 항목:**
- [ ] 이동 방향(오른쪽)으로 대시가 발생
- [ ] 커서가 왼쪽에 있어도 이동 방향으로 대시됨
- [ ] `_currentStateName`: WalkState → **"DashState"** → WalkState (이동 키 유지 시)

---

### [M2-05] DashHandler — 쿨다운 확인

**방법:**
1. Play 모드 진입
2. **Space** 키 연타 (빠르게 2번 이상 입력)

**확인 항목:**
- [ ] 첫 번째 대시 후 **약 1초** 동안 대시 불가 -> 빠르게 2연타 하면 1번 대시 하고, 1초뒤에 입력이 없음에도 2번째 대시가 나감, 이거 고쳐야함. 입력을 예약하지 않음.
- [ ] 1초 이내 Space 재입력 시 대시가 실행되지 않음
- [ ] 1초 후 Space 입력 시 정상 대시 실행

---

### [M2-06] DashHandler — 무적 판정 확인

**목적:** 대시 중에는 적의 공격을 받지 않는지 확인합니다.

**방법:**
1. `Player → Health System._maxHp` = **1** 설정
2. Play 모드 진입
3. 좀비 위를 **Space 대시**로 통과 -> 좀비를 뚫지 못함 또한 대시중 공격은 안돼지만 공격중에 대시하는건 됨. 서로 안되야 정상임.

**확인 항목:**
- [ ] 대시 중 좀비와 겹쳐도 HP가 깎이지 않음
- [ ] 대시 종료 후 좀비와 겹치면 HP가 정상 감소
- [ ] Console에 `[Player] 사망` 메시지가 대시 중에는 출력되지 않음

---

### [M2-07] HitState — 피격 스태거 확인

**목적:** 피격 시 0.2초 동안 이동·공격이 잠기는지 확인합니다.

**방법:**
1. `Player → Health System._maxHp` = **200** (충분히 높게)
2. Play 모드 진입
3. 좀비에게 의도적으로 맞기 -> 좀비에게 부딛혀도 HP가 그대로임. 아무일도 발생안함.
4. 피격 즉시 WASD + 좌클릭 시도

**확인 항목:**
- [ ] `_currentStateName` = **"HitState"** 순간 표시
- [ ] HitState 진입 약 0.2초 동안 이동 불가
- [ ] 0.2초 후 자동 복귀 (이동 키 누르면 WalkState, 없으면 IdleState)
- [ ] 대시 중(DashState) 좀비에게 맞아도 HitState 전환 없음 (무적 확인)

---

### [M2-08] DeathState — 사망 후 입력 잠금 확인

**방법:**
1. `Player → Health System._maxHp` = **1**
2. Play 모드 진입
3. 좀비에게 맞아 사망 -> 좀비에게 부딛혀도 HP가 그대로임. 아무일도 발생안함.

**확인 항목:**
- [ ] `_currentStateName` = **"DeathState"** 전환
- [ ] 사망 후 WASD 이동 완전 불가
- [ ] 사망 후 좌클릭 공격 완전 불가
- [ ] Console: `[Player] 사망 → 카르마 1pt` 출력
- [ ] Console: `[Player] Death 상태 진입` 출력

---

### [M2-09] PlayerMovement — StatSystem 이동속도 연동 확인

**목적:** `StatSystem.TotalMoveSpeed`가 실제 이동 속도에 반영되는지 확인합니다.

**방법:**
1. Play 모드에서 이동 속도 기본값 체감 확인
2. **Play 모드 종료**
3. `Player → Stat System._baseMoveSpeed` = **2** (느리게)
4. Play 모드 재진입 → 이동 속도 체감 비교

**확인 항목:**
- [ ] `_baseMoveSpeed = 2`일 때 체감상 훨씬 느리게 이동
- [ ] `_baseMoveSpeed = 10`일 때 빠르게 이동
- [ ] 기존 `PlayerMovement.moveSpeed` 필드와 무관하게 동작

---

### [M2-10] FSM + Dash — 대시 중 무기 잠금 확인

**방법:**
1. Play 모드 진입
2. 대시와 동시에 **좌클릭** 연타 -> 시중 공격은 안돼지만 공격중에 대시하는건 됨. 서로 안되야 정상임.

**확인 항목:**
- [ ] 대시 중 공격 애니메이션이 재생되지 않음
- [ ] 대시 종료 후 클릭 입력이 정상 처리됨 (공격 발동)

---

## 🔴 통합 테스트 — 전투 흐름 전체

---

### [INT-01] 완전한 전투 사이클

**방법:**
1. `_maxHp` = 50으로 설정
2. 좀비 3마리와 전투 진행
3. 검/활/완드 번갈아 사용

**확인 항목:**
- [ ] 모든 무기의 데미지 텍스트가 정상 표시
- [ ] 피격 시 HitState → 자동 복귀
- [ ] 대시로 적 공격 회피 가능
- [ ] 적 사망 후 오브젝트 제거됨
- [ ] 플레이어 사망 시 DeathState로 전환 (이동 불가)

---

### [INT-02] 무기 교체 중 FSM 상태 유지

**방법:**
1. Play 모드 진입
2. 대시 직후 즉시 숫자키로 무기 교체

**확인 항목:**
- [ ] 무기 교체 후 FSM 상태 정상 유지 (DashState → Idle/Walk)
- [ ] 새 무기로 교체 후 공격 정상 동작

---

## 📝 알려진 제한 사항 (현재 미구현)

| 항목 | 구현 예정 |
|---|---|
| 완드 시전 시 마나 5 소모 | Milestone 3 |
| `F` 키 물약 사용 | Milestone 3 |
| `E` 키 무기 슬롯 토글 | Milestone 3 |
| 발사체 10칸 사거리 제한 | Milestone 3 |
| 사망 애니메이션 (`Death` 파라미터 미등록) | Milestone 3+ |
| 사망 후 부활/게임오버 흐름 | Milestone 3+ |
| 몬스터 AI (추적, 공격) | Milestone 4 |
| 몬스터 오브젝트 풀링 | Milestone 4 |

---

## 🐛 버그 리포트 양식

테스트 중 발견된 버그는 아래 형식으로 기록해 주세요:

```
[버그 ID] 제목
- 발생 조건: 
- 재현 순서: 
- 예상 결과: 
- 실제 결과: 
- 스크린샷/Console 로그: 
```
