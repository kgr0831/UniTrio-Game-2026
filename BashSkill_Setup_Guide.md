[T] -> 테스트 성공
[F] -> 테스트 실패 (오류 또는 버그 발생)
[E] -> 수정 사항 있음


# 강타(Bash) 스킬 — 구현 현황 및 테스트 가이드

---

## 현재 구현 및 에디터 설정 완료 목록

| 항목 | 파일 / 위치 | 상태 |
|---|---|---|
| 스킬 데이터 클래스 | `Scripts/Data/BashSkillData.cs` | 완료 |
| 붉은 블룸 + VFX 교체 로직 | `Scripts/Player/Weapon/SwordBehaviour.cs` | 완료 |
| 강타 스택 / 데미지 배율 | `Scripts/Core/StatSystem.cs` | 완료 |
| 히트 시 배율 소비 | `Scripts/Player/Weapon/SwordHitbox.cs` | 완료 |
| 기본 스킬 자동 장착 필드 | `Scripts/Player/SkillSlotManager.cs` | 완료 |
| `BashSkill.asset` 생성 | `Resources/Data/Skills/BashSkill.asset` | **에디터 생성 완료** |
| 스킬 아이콘 연결 | `Resources/SkillIcon/강타.png` → `Icon` | **에디터 연결 완료** |
| `SkillSlotManager` 프리팹 추가 | `Player.prefab` → Player 루트 | **에디터 추가 완료** |
| 기본 스킬 자동 장착 | `_defaultSkill` → `BashSkill.asset` | **에디터 연결 완료** |
| `SwordBehaviour` 렌더러 연결 | `_swordRenderer` / `_vfxRenderer` | **에디터 연결 완료** |
| 강타 VFX 스프라이트 | `_bashVfxSprite` | 비어있음 (선택 사항) |

> **플레이 버튼을 누르면 강타 스킬이 즉시 장착된 상태로 시작됩니다.**

---

## 스킬 수치 요약

| 항목 | 값 |
|---|---|
| 단축키 | **Q** |
| 마나 소모 | 20 |
| 쿨다운 | 10초 |
| 효과 지속 | 기본 공격 **3회** 적중까지 |
| 데미지 배율 | 공격력 **2.0배** |
| 사용 가능 무기 | **검(Sword) 전용** |

---

## 실행 흐름

```
[Q 키]
  │
  ▼
SkillSlotManager.TryUseSkill()
  ├─ 쿨다운 남음?        → 차단 (콘솔 로그)
  ├─ 검 미착용?          → NotificationUI "검을 착용해야 합니다!" 표시 후 차단
  ├─ 마나 < 20?          → 차단 (콘솔 로그)
  └─ 통과
       │
       ▼
  마나 -20 소모
  쿨다운 타이머 10초 시작
  StatSystem.BashCount = 3
  SwordBehaviour.SetBashEffectActive(true)
       │
       ▼
  [검 스프라이트 → HDR 붉은 블룸 색조 적용]
  [VFX SpriteRenderer → _bashVfxSprite로 교체] (연결 시)
       │
       ▼
  기본 공격 클릭 → SwordHitbox.OnTriggerEnter2D()
       ├─ StatSystem.UseBashStack() 호출
       ├─ BashCount 3 → 2 → 1 → 0
       └─ 데미지 × 2.0 적용
            │
            ▼ (BashCount = 0)
       SwordBehaviour.SyncBashEffect() 자동 감지
       → 붉은 효과 해제, VFX 스프라이트 원복
```

---

## 컴포넌트 구조

```
Player (루트)
├─ StatSystem          ← BashCount, UseBashStack()
├─ SkillSlotManager    ← Q키 입력, 쿨다운, _defaultSkill = BashSkill.asset
└─ WeaponPivot
   └─ SwordWeapon      ← SwordBehaviour
      ├─ Sword         ← _swordRenderer (블룸 대상)
      └─ VFX           ← _vfxRenderer (스프라이트 교체 대상)
         └─ SwordHitBox  ← SwordHitbox (데미지 판정 + UseBashStack 호출)
```

---

## 테스트 방법

### 사전 확인

1. Unity 에디터에서 `Unitro2` 씬이 열려 있는지 확인
2. **Hierarchy → Player → SkillSlotManager** 컴포넌트 Inspector 확인
   - `Default Skill` 슬롯에 `BashSkill` 에셋이 연결되어 있어야 함
3. **Hierarchy → Player → WeaponPivot → SwordWeapon → SwordBehaviour** Inspector 확인
   - `Sword Renderer` : `Sword` 오브젝트의 SpriteRenderer 연결됨
   - `Vfx Renderer` : `VFX` 오브젝트의 SpriteRenderer 연결됨
   - `Bash Sword Color` : HDR 빨강 (R≈3) 값 확인

---

### 테스트 1 — 기본 발동 및 시각 효과

**목적** : Q키 입력 시 스킬이 발동되고 검이 붉게 빛나는지 확인

1. 플레이 버튼 실행
2. **숫자키 1**을 눌러 검 장착 확인
3. **Q키** 입력
4. **기대 결과**
   - 검 스프라이트 전체가 붉은 색조로 변함
   - Console에 `[BashSkill] 강타 발동! 다음 3회 공격에 2배 데미지 적용` 출력
   - 마나가 20 감소함
5. 적에게 기본 공격(마우스 좌클릭) 3회 적중
6. **기대 결과**
   - 3번째 적중 후 붉은 효과가 사라지고 검 색상이 원래대로 복원

> **블룸이 안 보일 때** : Scene의 Global Volume → Bloom 컴포넌트 → `Threshold` 값이 1.0 이하인지 확인.
> 값이 크면 `SwordBehaviour`의 `Bash Sword Color` R 값을 더 높게 조정.

---

### 테스트 2 — 데미지 2배 확인

**목적** : 강타 적용 공격이 정확히 2배 데미지를 주는지 수치로 검증

1. 먼저 강타 **비활성** 상태에서 적 1회 공격 → DamageText 수치 기록
2. Q키로 강타 발동 후 동일 적 1회 공격 → DamageText 수치 기록
3. **기대 결과** : 강타 적용 수치 ≈ 일반 수치 × 2.0

> 계산식 : `(StatSystem.TotalAtk + SwordHitbox._baseDamage) × 1.0 × 2.0`

---

### 테스트 3 — 무기 조건 차단

**목적** : 검 이외 무기 착용 시 스킬이 막히는지 확인

1. 플레이 후 **숫자키 2**를 눌러 창(Spear)으로 교체
2. **Q키** 입력
3. **기대 결과**
   - 스킬 발동 안 됨
   - 화면에 `"검을 착용해야 합니다!"` NotificationUI 메시지 표시

---

### 테스트 4 — 쿨다운

**목적** : 사용 후 10초 동안 재사용이 막히는지 확인

1. 검 장착 후 Q키로 강타 발동
2. 즉시 Q키 재입력
3. **기대 결과** : Console에 `쿨다운 중 (N.Ns 남음)` 출력, 스킬 발동 안 됨
4. 10초 대기 후 Q키 입력
5. **기대 결과** : 스킬 재발동 성공

---

### 테스트 5 — 마나 부족 차단

**목적** : 마나가 20 미만일 때 스킬이 막히는지 확인

> 테스트 방법: `StatSystem`의 Inspector에서 `_baseMana`를 10으로 임시 변경하거나,
> 마나를 빠르게 소모하는 다른 스킬을 반복 사용 후 시도.

1. 현재 마나가 20 미만인 상태에서 Q키 입력
2. **기대 결과** : Console에 `마나 부족 (필요: 20, 현재: N)` 출력, 스킬 발동 안 됨

---

### 테스트 6 — 무기 교체 시 효과 해제

**목적** : 강타 활성 중 무기를 바꾸면 붉은 효과가 즉시 사라지는지 확인

1. Q키로 강타 발동 (검 붉어짐)
2. 공격을 3회 완료하기 전에 **숫자키 2**로 창으로 교체
3. **기대 결과** : 검 오브젝트의 붉은 색조가 즉시 원복됨 (`OnDeactivated` 호출)
4. 다시 **숫자키 1**로 검 재장착 시 붉은 효과 없음 (BashCount 잔여량 유지)

> `BashCount`는 StatSystem에 남아 있지만, 검을 다시 들어도 SwordBehaviour가
> `LateUpdate`에서 `SyncBashEffect()`로 즉시 재적용합니다.

---

### 테스트 7 — (선택) VFX 스프라이트 교체 확인

`_bashVfxSprite` 슬롯에 스프라이트를 연결한 경우에만 해당.

1. Hierarchy → SwordWeapon → SwordBehaviour Inspector에서 `Bash Vfx Sprite` 슬롯에 스프라이트 연결
2. 강타 발동
3. **기대 결과** : VFX 오브젝트의 스프라이트가 지정한 이미지로 교체됨
4. 3회 공격 완료 후 원래 VFX 스프라이트로 복원

---

## 자주 발생하는 문제

| 증상 | 원인 | 해결 |
|---|---|---|
| Q키를 눌러도 아무 반응 없음 | SkillSlotManager 없음 또는 Default Skill 미연결 | Player Inspector에서 SkillSlotManager 확인 |
| 붉은 색이 안 보임 | `_swordRenderer` 미연결 또는 Bloom Threshold 높음 | SwordBehaviour Inspector / Global Volume Bloom 설정 확인 |
| 2배 데미지가 안 됨 | SwordHitbox에서 Stats 미연결 | SwordHitbox Inspector의 `Player Entity` 연결 확인 |
| 스킬 발동 후 효과가 안 꺼짐 | StatSystem 참조 누락 | SwordBehaviour Awake의 `GetComponentInParent<StatSystem>()` 경로 확인 |
| 검 미착용 메시지 안 뜸 | NotificationUI.Instance 없음 | 씬에 NotificationUI 오브젝트 배치 여부 확인 |
