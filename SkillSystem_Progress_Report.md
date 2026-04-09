# 스킬 시스템 구현 진행 보고서 (Skill System Progress Report)

**날짜:** 2026-04-09
**상태:** 구현 완료 (Implementation Completed)

## 1. 구현 목표
특정 무기 착용 시에만 사용 가능한 스킬 시스템을 설계하고 구현하였습니다. 요구 사항에 따른 3가지 스킬과 무기 불일치 시의 시각적 피드백 시스템이 포함되었습니다.

## 2. 주요 변경 사항

### 2.1 데이터 구조 및 기초 시스템
- **WeaponType Enum 도입**: 검, 창, 활, 지팡이를 구분하기 위한 열거형 추가 (`Item.cs`)
- **ScriptableObject 확장**:
    - `WeaponData`: 무기 타입 전용 필드 추가
    - `SkillData`: 시전 로직(`Execute`) 및 요구 무기(`RequiredWeapon`) 필드 추가
- **StatSystem 확장**: 마법 공격력(`TotalMagicAtk`) 스탯 및 강타 버프(`BashCount`) 관리 기능 추가

### 2.2 UI 피드백 시스템
- **NotificationUI 구현**: 무기가 맞지 않을 때 "XX를 착용해야 합니다!"라는 메시지를 상단에서 노출하며 아래로 내려가며 사라지게 구현 (`NotificationUI.cs`)

### 2.3 스킬 세부 구현
- **강타 (Bash)**: 검 장착 시 작동. `StatSystem`의 배율을 조정하여 다음 3회 기본 공격 데미지를 2배로 증가시킴.
- **조준 사격 (Aimed Shot)**: 활 장착 시 작동. 무기 공격 속도에 비례한 차징(`3 / 공속`) 후 2.5배 계수의 투사체를 발사.
- **마나 창 (Mana Spear)**: 지팡이 적합. `마나 * 0.5 + 마공 * 0.5` 공식의 데미지를 가진 투사체를 마우스 방향으로 즉시 발사.

## 3. 관련 파일 목록

| 기능 | 파일 경로 |
| :--- | :--- |
| **코어 시스템** | `Assets/Scripts/Data/Item.cs`, `StatSystem.cs`, `SkillData.cs` |
| **무기 행동** | `SwordBehaviour.cs`, `SpearBehaviour.cs`, `BowBehaviour.cs`, `WandBehaviour.cs` |
| **스킬 데이터** | `BashSkillData.cs`, `AimedShotSkillData.cs`, `ManaSpearSkillData.cs` |
| **UI** | `NotificationUI.cs` |
| **전투 로직** | `SwordHitbox.cs`, `SkillSlotManager.cs` |

## 4. 향후 작업 권장 사항 (Next Steps)
- 스킬 사용 시의 추가적인 시각 효과(VFX) 및 사운드 효과(SFX) 보강
- 스킬 데이터 에셋(ScriptableObject) 실제 생성 및 인스펙터 설정
- 각 스킬에 대한 밸런스 테스트 및 계수 조정

---
*본 문서는 Antigravity AI 어시스턴트에 의해 생성되었습니다.*
