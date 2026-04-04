# 🧙‍♂️ 마법 무기 시스템 구현 가이드 (Wand & Magic System)

이 문서는 **UniTrio-Game-2026** 프로젝트의 마법(완드) 무기 시스템에 적용된 기술적 구현 사항과 최적화 내역을 정리한 리포트입니다.

---

## 1. 시스템 아키텍처 (System Architecture)

### 📂 핵심 파일 구성
*   **`WandBehaviour.cs`**: 무기의 메인 로직. 애니메이션 동기화, 투사체 발사, 글로우 빌드업을 담당합니다.
*   **`MagicProjectile.cs`**: 발사된 투사체의 물리 이동, 충돌 감지, 폭발 스폰 및 풀링 반환을 담당합니다.
*   **`ExplosionEffect.cs`**: 범위 데미지 판정(`Physics2D.OverlapCircleNonAlloc`) 및 폭발 시각 효과를 관리합니다.
*   **`SimpleObjectPool.cs`**: 투사체와 폭발 효과의 `Instantiate/Destroy` 부하를 없애주는 싱글톤 풀링 시스템입니다.
*   **`SpriteGlow.shader`**: URP 환경에서 하드웨어 가속(SRP Batcher)을 지원하는 고성능 발광 셰이더입니다.

---

## 2. 시각 효과 및 렌더링 (Visuals & Rendering)

### ✨ SpriteGlow 셰이더 기능
*   **Glow Intensity & Color**: HDR 컬러와 강도를 조절하여 Bloom 효과와 연동됩니다.
*   **Use Whole Sprite Glow (Toggle)**: 
    *   **ON**: 스프라이트 모양 그대로 전체가 빛납니다. (투사체, 폭발용)
    *   **OFF**: 지정된 `Emission Texture` 마스크 영역만 빛납니다. (완드 본체용)
*   **SRP Batcher 호환**: 상수 버퍼(`CBUFFER`)를 사용하여 드로우 콜(Draw Call)을 최소화합니다.

### 🎥 애니메이션 & 글로우 타이밍
*   완드 공격 시 `Mathf.SmoothStep` 곡선을 사용하여 **서서히 밝아졌다가 서서히 어두워지는** 부드러운 발광 효과를 냅니다.
*   **발생 조건**: 공격 개시(`AttackStart`) 시 글로우가 상승하고, 공격 종료(`OnDeactivated`) 시 즉시 초기화됩니다.

---

## 3. 최적화 내역 (Performance Optimizations)

| 항목 | 리스크 (전) | 해결책 (후) |
| :--- | :--- | :--- |
| **CPU/Memory** | 빈번한 `Instantiate`로 인한 프레임 드랍 | **오브젝트 풀링** (`SimpleObjectPool`) 적용 |
| **Physics** | `OverlapCircleAll`로 인한 매 프레임 메모리 할당 | **`OverlapCircleNonAlloc`** 및 정적 배열 재사용 |
| **GPU** | 드로우 콜 과다 및 불필요한 그림자 연산 | **SRP Batcher** 대응 및 **섀도우 강제 비활성화** |
| **Stuttering** | 가비지 컬렉션(GC) 스파이크 | **GC Free** 물리 쿼리 및 재사용 로직 구현 |

---

## 4. 인스펙터 설정 가이드 (Inspector Guide)

### 📌 완드 (Wand) 설정
1.  `Max Glow Intensity`: **35.0 이상** 추천 (화려한 연출 시)
2.  `WandGlow` 매터리얼: 
    *   `Use Whole Sprite Glow`: **Unchecked (OFF)**
    *   `Emission Texture`: 제작한 **White-on-Black** 마스크 이미지 할당.

### 📌 투사체 및 폭발 (Projectile & Explosion)
1.  매터리얼의 `Use Whole Sprite Glow`: **Checked (ON)**
2.  폭발 범위: `CircleCollider2D`의 `Radius` 값을 조절하여 설정.

---

## 5. 향후 확장 팁
*   **새로운 마법 무기**: `WandBehaviour`를 상속받거나 프리팹만 교체하여 쉽게 추가 가능합니다.
*   **성능 확장**: 더 많은 오브젝트가 필요할 경우 `SimpleObjectPool`의 초기 생성 개수를 조절하세요.
*   **시각 효과 확장**: `Bloom` 프로파일의 `Scatter` 값을 높여 더 넓은 빛 확산을 유도하세요.

---
*Created by Antigravity AI for UniTrio-Game-2026*
