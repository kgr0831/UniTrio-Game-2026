# 🔮 떠다니는 마법 무기 시스템 (Floating Weapon System)

## 컨셉 요약
> 주인공이 무기를 직접 들지 않고, **마법의 힘(염력)으로 무기를 공중에 띄워 조종**하는 판타지 스타일.
> 손짓만 하면 무기가 둥둥 떠다니며 베고, 찌르고, 화살을 쏘고, 마법을 발사합니다.

---

## 현재 시스템 분석

### 기존 구조
```
PlayerWeaponController (피봇 회전, 공격 입력, 무기 교체)
  └─ WeaponPivot (Transform)
       └─ SwordWeapon / SpearWeapon / BowWeapon / WandWeapon (각 무기 오브젝트)
            └─ WeaponBehaviourBase 파생 클래스 (공격 로직)
            └─ SpriteRenderer (무기 스프라이트)
            └─ VFX Animator, Hitbox 등
```

### 핵심 관찰
- **이미 OrbitRadius 시스템 존재**: `WeaponBehaviourBase`에 `OrbitRadius` 프로퍼티가 있고, 창(0.5f)·활(0.5f)·지팡이(0.2f)는 이미 캐릭터 중심에서 떨어져 공전함
- **무기 피봇이 마우스 방향으로 회전**: `UpdateCursorDirection()`이 커서 방향으로 피봇을 회전 → 무기가 자연스럽게 따라감
- **VFX 기반 공격**: 공격 판정은 이미 VFX + 히트박스로 처리됨 (무기 스프라이트 자체가 때리는 것이 아님)

---

## 구현 계획

### Phase 1: `FloatingWeaponMotion` 컴포넌트 (핵심)

> [!IMPORTANT]
> 각 무기 오브젝트(SwordWeapon, SpearWeapon 등)에 붙이는 **독립적인 MonoBehaviour**.
> 기존 `WeaponBehaviourBase`나 `PlayerWeaponController`를 수정하지 않고도 떠다니는 연출을 추가합니다.

#### 역할
| 기능 | 설명 |
|------|------|
| **Idle 부유** | 비공격 상태에서 상하로 살짝 둥실둥실 (sin 파동) |
| **회전 흔들림** | 미세한 Z축 회전 요동 (살아있는 느낌) |
| **마법 파티클** | 무기 주변에 은은한 마법 입자 이펙트 |
| **공격 반응** | 공격 시작 시 앞으로 튀어나가는 모션 / 공격 종료 시 원위치 복귀 |
| **장착/해제 연출** | 활성화 시 위에서 떨어지며 등장, 비활성화 시 사라지는 효과 |

#### 주요 파라미터 (ScriptableObject or SerializeField)
```csharp
[Header("Idle Floating")]
float _bobAmplitude = 0.08f;     // 상하 부유 폭
float _bobFrequency = 2.0f;      // 상하 부유 속도
float _tiltAmplitude = 3f;       // Z축 회전 요동 (도)
float _tiltFrequency = 1.5f;     // 회전 요동 속도

[Header("Magic Aura")]
Color _auraColor;                 // 무기별 마법 오라 색상
float _auraIntensity = 2f;       // 글로우 강도

[Header("Attack Response")]
float _attackLungeDistance = 0.3f; // 공격 시 전방 돌출 거리
float _lungeSpeed = 15f;          // 돌출 속도
float _returnSpeed = 8f;          // 복귀 속도
```

#### 핵심 로직 (의사코드)
```csharp
void LateUpdate()
{
    if (weapon.IsAttacking)
    {
        // 공격 모션 중: 전방으로 살짝 돌출 → 이미 애니메이터가 처리
        // 추가적으로 마법 파티클 강도 증가
        UpdateAttackAura();
    }
    else
    {
        // Idle: 둥실둥실 부유 + 미세 회전
        float bobY = sin(Time * _bobFrequency) * _bobAmplitude;
        float tilt = sin(Time * _tiltFrequency) * _tiltAmplitude;
        
        // localPosition에 bobY 오프셋 추가
        // localEulerAngles에 tilt 추가 (피봇 회전과 별개로)
    }
}
```

### Phase 2: 무기별 마법 오라 이펙트

각 무기 타입마다 고유한 마법 오라를 추가합니다.

| 무기 | 오라 색상 | 이펙트 스타일 |
|------|-----------|---------------|
| 🗡️ 검 | 붉은 HDR (`#FF3030`) | 칼날을 따라 흐르는 불꽃 입자 |
| 🔱 창 | 푸른 HDR (`#3080FF`) | 창끝에서 번개 입자 산란 |
| 🏹 활 | 보라 HDR (`#8030FF`) | 시위에서 에너지 맥동 |
| 🪄 지팡이 | 하늘 HDR (`#30C0FF`) | 보석 주변 반짝이는 별 입자 |

> [!TIP]
> 기존 `Custom/SpriteGlow` 셰이더 + `MaterialPropertyBlock`을 그대로 활용하면 SRP Batching도 유지됩니다.

### Phase 3: PlayerWeaponController 보정

기존 컨트롤러에 최소한의 수정만 가합니다:

1. **손 위치 보정 제거 옵션**: 떠다니는 무기에는 `_handYOffset`이 불필요할 수 있음
2. **GoBehind 비활성화**: 떠다니는 무기는 항상 앞에 보이는 것이 자연스러움
3. **공격 시 캐릭터 애니메이션**: 현재 캐릭터가 "무기를 들고 때리는" 애니메이션 → "손짓/시전" 애니메이션으로 교체 필요 (애니메이션 에셋 의존)

### Phase 4: 무기 교체 연출

무기 교체 시 기존의 단순 SetActive 대신:

```
[해제] 현재 무기가 반투명해지며 위로 살짝 올라가다 사라짐 (0.2초)
[장착] 새 무기가 위에서 떨어지며 파티클 폭발과 함께 등장 (0.3초)
```

---

## 작업 순서 (우선순위)

### Step 1 — `FloatingWeaponMotion.cs` 작성
- [x] 분석 완료
- [ ] 부유(bob) + 회전(tilt) Idle 모션
- [ ] 공격 반응(lunge/return) 모션
- [ ] 마법 오라 파티클 시스템

### Step 2 — 각 무기에 컴포넌트 부착
- [ ] SwordBehaviour → `_bobAmplitude`, `_auraColor` 설정
- [ ] SpearBehaviour → 설정
- [ ] BowBehaviour → 설정
- [ ] WandBehaviour → 설정

### Step 3 — PlayerWeaponController 미세 조정
- [ ] 떠다니는 모드에서 `_handYOffset` 비활성화
- [ ] UseGoBehind 기본값 false로 전환 검토

### Step 4 — 무기 교체 전환 연출
- [ ] Fade In/Out 코루틴
- [ ] 등장 파티클 이펙트

### Step 5 — (선택) 캐릭터 시전 모션
- [ ] 공격 시 캐릭터의 "손짓" 애니메이션 (아트 에셋 필요)

---

## 질문 & 확인 사항

> [!WARNING]
> 아래 사항을 먼저 확인해주세요!

1. **캐릭터 애니메이션**: 현재 캐릭터가 공격 시 특별한 모션을 재생하나요? (무기를 휘두르는 동작이 있다면 "손짓" 모션으로 교체 필요)
2. **무기 스프라이트 크기**: 떠다니는 무기는 캐릭터와 비슷한 크기? 약간 작게? (현재 기준 유지?)
3. **오라 색상 취향**: 위 표의 색상 제안이 괜찮은지, 다른 색상 테마가 있는지?
4. **우선순위**: Phase 1(부유 모션)부터 바로 구현할까요, 아니면 전체 계획을 더 논의할까요?
5. **참고 영상**: 처음 메시지에 "아래 영상과 같이"라고 하셨는데, 혹시 참고 영상/이미지를 공유해주실 수 있나요?

---

## 기술적 제약 사항

> [!NOTE]
> 프로젝트 규칙 준수 확인

- ✅ **GC 최소화**: `FloatingWeaponMotion`의 `LateUpdate()`에서 `new` 사용 없음
- ✅ **캐싱**: 파티클 시스템, 렌더러 참조 모두 `Awake()`에서 캐싱
- ✅ **MaterialPropertyBlock**: 셰이더 프로퍼티 변경 시 MPB 사용
- ✅ **SerializeField**: 모든 참조는 인스펙터 할당 방식
- ✅ **데이터/렌더링 분리**: 모션 로직은 View 컴포넌트로 분리
