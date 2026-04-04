# ⚔️ UniTrio 종합 전투 시스템 매뉴얼 (Combat System Manual)

이 문서는 **UniTrio-Game-2026** 프로젝트의 전투 시스템 전반에 대한 설계 구조, 작동 원리 및 최적화 기법을 정리한 공식 가이드입니다.

---

## 1. 전투 흐름 (Combat Flow Overview)

전투는 다음과 같은 4단계 시퀀스로 진행됩니다:
1.  **입력 및 제어**: `PlayerWeaponController`가 입력을 받고 무기 피봇을 조절합니다.
2.  **공격 실행**: 장착된 무기(`WeaponBehaviour`)가 애니메이션을 재생하고 발사체나 히트박스를 생성합니다.
3.  **타격 및 판정**: `OnTriggerEnter2D` 또는 `OverlapCircleNonAlloc`을 통해 적(`Enemy`)의 `TakeDamage`를 호출합니다.
4.  **피드백 출력**: 적의 화이트 플래시, 데미지 텍스트(`DamageText`), 폭발 VFX 등이 출력됩니다.

---

## 2. 플레이어 무기 시스템 (Player Weaponry)

### 🕹️ 무기 컨트롤러 (`PlayerWeaponController.cs`)
*   **무기 스왑**: 숫자키 1~9번을 통해 실시간으로 무기 오브젝트와 로직을 교체합니다.
*   **방향 제어**: 마우스 커서 방향으로 무기 피봇을 회전시키며, 공격 시 캐릭터의 스프라이트 방향(Flip)을 제어합니다.
*   **입력 버퍼링**: 공격 애니메이션 중에도 다음 입력을 0.3초간 기억하는 큐(Queue) 시스템이 적용되어 조작감이 부드럽습니다.
*   **콤보 시스템**: 연속 공격 시 공격 단계(`_comboStep`)를 관리하여 차등 애니메이션과 강화된 공격을 지원합니다.

### ⚔️ 무기 종류별 특징
*   **검 (Sword)**: 충돌 지점에 무작위 각도의 타격 VFX를 생성하여 고전적인 타격감을 제공합니다.
*   **창 (Spear)**: 직선형 찌르기 공격 위주로 설계되었습니다.
*   **완드 (Wand)**: 원거리 발사체를 소환하며, 발사 시 무기에 글로우 효과가 점진적으로 쌓입니다.
*   **활 (Bow)**: 차징 및 투사체 발사 메커니즘을 가집니다.

---

## 3. 적 및 피격 시스템 (Enemy & Hit Logic)

### 👾 에너미 코어 (`Enemy.cs`)
*   **체력 관리**: 최대/현재 체력을 관리하며, `TakeDamage`를 통해 외부의 모든 공격을 수용합니다.
*   **피격 효과 (Flash)**: `MaterialPropertyBlock`을 사용하여 셰이더의 `_FlashAmount`를 조절, 매터리얼 복제 없이 효율적으로 화이트 점멸 효과를 냅니다.

### 🎯 타격 판정 매커니즘
*   **근접 타격 (`SwordHitbox.cs`)**: 트리거 충돌 시 적의 가장 가까운 표면(`ClosestPoint`)을 계산하여 정확한 위치에 이펙트를 소환합니다.
*   **원거리/범위 타격 (`ExplosionEffect.cs`)**: 물리 연산 시 가비지가 발생하지 않는 `NonAlloc` 방식을 사용하여 대규모 폭발 시에도 성능을 유지합니다.

---

## 4. 시각 효과 및 피드백 (VFX & Feedback)

### 🔢 데미지 팝업 (`DamageText.cs`)
*   TextMeshPro를 사용하여 고화질 렌더링을 지원합니다.
*   생성 시 위로 떠오르며 알파 값이 서서히 빠지는(Fade-out) 연출이 적용되어 있습니다.

### 🌟 글로우 시스템 (`SpriteGlow.shader`)
*   HDR 블룸(Bloom) 효과와 연동되어 마법 무기와 폭발에 강력한 광원 효과를 부여합니다.
*   키워드(`_USE_MAIN_ALPHA_AS_GLOW`)를 통해 마스크 텍스처 없이도 전체 발광이 가능하도록 설계되었습니다.

---

## 5. 최적화 및 안정성 (Technical Optimization)

전투 중 발생하는 수많은 오브젝트를 효율적으로 처리하기 위해 다음 기술이 적용되었습니다:

| 기술 | 적용 대상 | 기대 효과 |
| :--- | :--- | :--- |
| **Object Pooling** | 투사체, 폭발 VFX, 마법 효과 | `Instantiate/Destroy` 오버헤드 제거 |
| **NonAlloc Physics** | 폭발 범위 데미지 판정 | 매 프레임 발생하는 GC Alloc 제거 |
| **SRP Batching** | 모든 전투용 스프라이트 | 드로우 콜(Draw Call) 획기적 감소 |
| **Shadow Optimization** | 발사체 및 VFX 렌더러 | 불필요한 그림자 연산 제거로 GPU 부하 감소 |

---
*Last Updated: 2026-04-04 | Logic by Antigravity*
