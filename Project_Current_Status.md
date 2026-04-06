# 📊 UniTrio-Game-2026 전체 프로젝트 구조 및 코드 분석 보고서

본 문서는 현재 프로젝트의 모든 소스 코드와 리소스 구조를 정밀하게 분석한 최종 코드 리뷰 보고서입니다.

---

## 🏗️ 1. 프로젝트 폴더 구조 (Folder Structure)

| 폴더 | 주요 내용 |
| :--- | :--- |
| **Assets/Scripts/Data** | `ItemData (SO)`, `ItemDatabase`, `Item` 등 데이터와 도감 시스템. |
| **Assets/Scripts/Player** | `PlayerWeaponController`, `PlayerMovement` 등 캐릭터 핵심 조작. |
| **Assets/Scripts/Player/Weapon** | 무기별 고유 행동(`Bow`, `Sword`, `Spear`, `Wand`) 및 투사체 로직. |
| **Assets/Resources/Animations** | 플레이어의 8방향 이동 및 무기별 공격 애니메이션 세트. |
| **Assets/Resources/Items** | 실제 에디터에서 생성된 아이템 SO 에셋들. |

---

## ⚔️ 2. 핵심 시스템 상세 리뷰 (System Review)

### 2.1 전투 및 무기 시스템 (Combat & Weapon System)
- **추상화 (`WeaponBehaviourBase.cs`)**: 모든 무기는 이 추상 클래스를 상속받아 `IsAttacking` 상태와 피격 판정 폴링(`PollFinished`)을 직접 구현합니다.
- **제어기 (`PlayerWeaponController.cs`)**: 
    - **핵심 로직**: 입력 버퍼링(`_attackQueued`)을 통해 공격 프레임이 씹히지 않게 설계되었습니다. 
    - **커서 추적**: 마우스 위치에 따라 무기 피봇이 회전하며, 위/왼쪽 방향일 때 Z값을 조정해 플레이어 뒤로 렌더링(`UseGoBehind`)하는 디테일이 포함되어 있습니다.
    - **무기 교체**: 숫자키(1~9)를 통해 슬롯 기반으로 무기를 즉시 교체하며, 교체 시 이전 무기를 깔끔하게 정리(`OnDeactivated`)합니다.
- **무기 구현 상세**:
    - **검 (Sword)**: 2타 수동 콤보 시스템이 구현되어 있으며, Y-축 반전을 통한 역방향 휘두르기가 지원됩니다.
    - **활 (Bow)**: 우클릭 차징 기능이 있으며, 차징 비율에 따라 데미지와 화살 속도가 보간됩니다. (최근 이중 재생 버그 수정됨)

### 2.2 데이터 및 아이템 시스템 (Data System)
- **SO 중심 설계**: `ItemData`를 통해 무기, 소모품 등의 고유 스탯을 관리하며, 이를 참조하는 `Item` 클래스를 통해 메모리 중복 사용을 방지합니다.
- **ID 기반 조회**: `ItemDatabase`에 등록된 모든 아이템은 `GetItemById(int id)`를 통해 전역적으로 안전하게 가져올 수 있습니다.

### 2.3 애니메이션 및 시각 체계 (Animation & Visuals)
- **2D BlendTree**: 2D Cartesian 이동 방식을 사용하여 WASD 입력값(DirX, DirY)에 따라 8방향 시선을 자연스럽게 처리합니다.
- **VFX 연동**: 무기 타격 시 `SlashVFX`, `SpearVFX` 등 애니메이션 동기화된 이펙트가 함께 재생됩니다.

---

## 📈 3. 현재 코드 로직 상태 (Logical State)

- **[수정 완료] 자동 연속 공격 제거**: 마우스 버튼을 유지해도 공격이 반복되지 않도록 `GetMouseButtonDown`으로 교체하여 조작 신뢰성을 높였습니다.
- **[수정 완료] 활 차징 발사 보정**: 차징 후 발사 시 애니메이션이 끊기지 않고 자연스럽게 이어지도록 `SetTrigger` 중복 호출을 제거했습니다.
- **[상태 유지] 검의 2연격**: 검 무기의 타격감을 위해 2타 콤보 로직은 보존되어 있습니다.

---

## 🚀 4. 리팩토링 및 향후 과제 (Opportunities)

- **공통 본체 (Entity Core)**: 현재 플레이어와 향후 추가될 몬스터의 공통 분모(체력, 스탯 계산)를 묶어줄 `LivingEntity` / `CharacterBase` 도입이 예정되어 있습니다. (Milestone 1 계획서 기반)
- **스탯 및 데미지 엔진**: `To-Do.md`에 명시된 복잡한 스탯 가산/승산 로직과 카르마 패널티를 담은 `DamageCalculator` 엔진 구축이 필요합니다.

---

**전체적으로 프로젝트는 데이터의 '설계'와 시각적인 '뼈대'가 매우 견고하며, 이제 Milestone 1을 통해 "게임적인 규칙과 상호작용"의 살점을 붙여나가는 단계입니다.**
