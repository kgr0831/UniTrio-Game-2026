# 개발 세션 노트 — 2026-06-28

**브랜치**: `feature/Tutorial`  
**작업자**: 김가람  
**Unity**: 6.3, 3D URP  
**주요 작업**: 튜토리얼 시스템 버그 수정 (아웃라인, 대화 입력 잠금, 대화 UI)

---

## 전체 버그 목록

| # | 버그 | 심각도 | 상태 |
|---|------|--------|------|
| 1 | 아웃라인이 빨간색이 아닌 흰색으로 표시됨 | HIGH | ✅ 수정 완료 |
| 2 | 아웃라인 두께가 너무 두꺼움 (localScale 1.25 고정) | MEDIUM | ✅ 수정 완료 |
| 3 | 아웃라인이 나무 바람 셰이더 움직임을 따라가지 못함 | LOW | ⚠️ 허용 결정 |
| 4 | 대화 중 이동·공격·대시·인벤토리 열기 가능 | HIGH | ✅ 수정 완료 |
| 5 | "대화" UI가 완전히 이상한 위치에서 이상한 방향으로 움직임 | HIGH | 🔴 **미해결** |
| 6 | "대화" UI가 감지 범위 밖에서도 계속 표시됨 | MEDIUM | 🔴 미해결 (5번과 연관) |
| 7 | Edrin_NPC가 아예 감지되지 않아 "대화" UI 자체가 안 뜸 | HIGH | ✅ 수정 완료 (이전 세션) |
| 8 | NPC_장인 콜라이더가 과도하게 커서 엉뚱한 NPC가 감지됨 | HIGH | ✅ 수정 완료 (이전 세션) |

---

## 이전 세션 작업 이력

### [완료] TutorialNodeOutline 최초 구현

- **파일 신규 생성**: `Assets/Scripts/Tutorial/TutorialNodeOutline.cs`
- 기존 `JustDodgeOutline` (UV 텍셀 기반)을 대체하는 튜토리얼 전용 아웃라인
- 원본 SpriteRenderer 뒤에 확대 복사본을 붙여 테두리 링처럼 표시하는 방식
- `TutorialManager.OutlineSequenceRoutine()`에서 `JustDodgeOutline.Create()` → `TutorialNodeOutline.Create()`로 교체

### [완료] SpriteSilhouette 셰이더 신규 작성

- **파일 신규 생성**: `Assets/Resources/Shaders/SpriteSilhouette.shader`
- URP HLSL 셰이더: 텍스처 알파 채널만 사용 → 단색 실루엣 출력
- `Sprites-Lit-Default` 사용 시 텍스처 무늬 그대로 보이는 문제 해결용

### [완료] InteractionPromptUI 위치 계산 수정

- **파일 수정**: `Assets/Scripts/UI/InteractionPromptUI.cs`
- `BuildUI()`에서 `_container`를 루트 Canvas에 직접 붙이도록 변경
- `Show()` 호출 전 `SyncPosition()` 먼저 실행 → 활성화 직후 1프레임 (0,0) 점프 방지
- `GetComponentInParent<Canvas>()` 실패 시 `FindFirstObjectByType<Canvas>()` 폴백 추가
- `LateUpdate()`에서 `RectTransformUtility.ScreenPointToLocalPointInRectangle()` 으로 Canvas 스케일 보정

### [완료] Edrin_NPC 레이어·콜라이더 수정 (버그 7)

- **원인**: Edrin_NPC가 Layer 0 (Default)에 있었음 → `PlayerInteractionDetector._interactableMask = 1024` (Layer 10 전용)에 걸리지 않음 → 감지 자체 불가
- **수정**: MCP `execute_code`로 런타임에 `gameObject.layer = 10` (Interactable) 적용
- BoxCollider2D 크기: 로컬 (27×27) → **(9×9)** (~2.72 world units)

### [완료] NPC_장인·상인 콜라이더 수정 (버그 8)

- **원인**: NPC_장인의 BoxCollider2D가 (8×8) world units → 플레이어 시작 위치에서 3.73 units 거리의 NPC가 항상 감지 범위 안에 들어옴 → Edrin_NPC 대신 NPC_장인이 "대화" UI 타겟이 됨
- **수정**: MCP `execute_code`로 NPC_장인·NPC_상인 콜라이더를 각 **(2×2.5) world units**로 축소

---

## 이번 세션(2026-06-28) 수정 완료 상세

### 버그 1·2 — 아웃라인 흰색 + 두께 문제

**파일**: `Assets/Resources/Shaders/SpriteSilhouette.shader`, `Assets/Scripts/Tutorial/TutorialNodeOutline.cs`

**원인 분석**:
- `static Material _sharedMat` → Unity 6 "Enter Play Mode without Domain Reload" 에서 세션 간 static 필드 유지 → 오염된 머티리얼 재사용
- 셰이더가 버텍스 컬러(`IN.color.rgb`)를 사용 → SRP Batcher 환경에서 `SpriteRenderer.color` 버텍스 컬러 전달 신뢰성 부재 → 흰색 출력

**수정 1 — `SpriteSilhouette.shader`**:
```hlsl
// 기존: 버텍스 컬러 사용 → 흰색 버그
return half4(IN.color.rgb, alpha * IN.color.a);

// 수정: _Color 머티리얼 프로퍼티로 교체
Properties { _Color ("Color", Color) = (1, 0.08, 0.08, 1) }
CBUFFER_START(UnityPerMaterial)
    float4 _MainTex_ST;
    half4  _Color;
CBUFFER_END
// frag:
return half4(_Color.rgb, alpha * _Color.a);
```

**수정 2 — `TutorialNodeOutline.cs`**:
- `static Material _sharedMat` → **인스턴스별 `Material _matInstance`**
- `_matInstance.color = new Color(1f, 0.08f, 0.08f, 0f)` → `_Color` 프로퍼티 직접 제어
- `_bgSr.color = Color.white` (버텍스 컬러는 중립 유지)
- `OnDestroy()`에서 `Destroy(_matInstance)` → 메모리 누수 방지
- `SetAlpha(float alpha)`: `_matInstance.color.a` 조정

**수정 3 — 어댑티브 localScale**:
```csharp
float worldScale = Mathf.Abs(src.transform.lossyScale.x);
float border = Mathf.Clamp(worldScale * 0.03f, 0.1f, 0.3f);
float ls = (worldScale + 2f * border) / worldScale;
// 나무 (worldScale≈8):   ls≈1.06  (테두리 0.24 world units)
// 돌   (worldScale≈0.4): ls≈1.5   (테두리 0.10 world units)
```

---

### 버그 3 — 아웃라인이 바람 셰이더를 따라가지 못함 (허용)

- 나무(`TreeHits`)는 `TreeWindMat.mat` 버텍스 셰이더로 바람 흔들림 적용
- `TutorialNodeOutline`의 아웃라인은 `SpriteSilhouette` 셰이더 → 버텍스 변위 불일치
- **허용 이유**: 완전 해결 시 URP Renderer Feature(Stencil 아웃라인) 필요 → 공수 과다. 튜토리얼 하이라이트 목적으로는 시각적 허용 범위 내
- 스프라이트 프레임 교체(Animator)는 `LateUpdate` 동기화로 정상 처리됨

---

### 버그 4 — 대화 중 플레이어 이동·공격·대시·인벤토리 가능

**파일**: `Assets/Scripts/MapObject/NpcInteractable.cs`

**원인**:
- `NpcInteractable.Interact()` → `PlayDialogue()` 시 `PlayerStateMachine` 전환 없음
- `CutsceneState.Enter()`는 `Movement` + `WeaponCtrl`만 비활성화
- `DashHandler`와 `InventoryToggle`은 어디에서도 잠기지 않음

**수정 — `NpcInteractable.cs`**:
```csharp
private PlayerStateMachine _playerFsm; // 추가된 필드

// Interact()에서:
if (_playerFsm == null && player != null)
    _playerFsm = player.GetComponentInParent<PlayerStateMachine>();

// PlayDialogue()에서:
_dialoguePlayer.OnDialogueEnded -= OnDialogueEnded; // 중복 구독 방지
_dialoguePlayer.OnDialogueEnded += OnDialogueEnded;
_dialoguePlayer.StartDialogue(so);
LockPlayer();

// LockPlayer():
_playerFsm.TransitionTo(_playerFsm.Cutscene); // Movement + WeaponCtrl OFF
_playerFsm.Dash.enabled = false;               // Dash OFF
InventoryToggle.Instance.enabled = false;       // I키 인벤토리 OFF

// OnDialogueEnded → UnlockPlayer():
_playerFsm.Dash.enabled = true;
_playerFsm.TransitionTo(_playerFsm.Idle);      // Movement + WeaponCtrl ON
InventoryToggle.Instance.enabled = true;
```

---

## 🔴 미해결 — "대화 UI가 이상하게 움직임" (버그 5·6)

### 증상 (사용자 보고)
- "F / 대화" 프롬프트 UI가 NPC 위치와 무관한 이상한 위치에 표시됨
- 플레이어가 움직이면 UI가 완전히 비정상적인 방향으로 이동 ("완전 이상하게 움직임")
- 감지 범위 밖에서도 표시되고, 범위 안팎 관계없이 동일하게 이상 동작

### 관련 코드 구조

**`PlayerInteractionDetector.cs`** (감지 → UI 호출):
```csharp
// Update() → DetectNearestInteractable()
int hitCount = Physics2D.OverlapCircleNonAlloc(
    transform.position, _detectionRadius=2f, _hitBuffer, _interactableMask=1024);

// 가장 가까운 IInteractable 선택
InteractionPromptUI.Instance.Show(nearestTransform, prompt);
// nearestTransform = col.transform (NPC의 Transform)
```

**`InteractionPromptUI.cs`** (UI 위치 계산):
```csharp
// Show()에서:
_targetTransform = target; // = NPC Transform
SyncPosition();            // 활성화 전 위치 선계산
_container.gameObject.SetActive(true);

// LateUpdate()에서:
Vector3 screenPos = _mainCamera.WorldToScreenPoint(
    _targetTransform.position + _worldOffset); // _worldOffset = (0, 2, 0)
RectTransformUtility.ScreenPointToLocalPointInRectangle(
    _rootCanvasRect, screenPos, uiCam, out Vector2 lp);
_container.localPosition = lp;
```

### 의심 원인 3가지

**[가장 유력] 원인 A — `_targetTransform`이 NPC가 아닌 다른 오브젝트**
- NPC_장인·NPC_상인 콜라이더를 MCP `execute_code`로 수정했으나 씬 저장이 정상 반영되지 않았을 가능성
- 또는 씬에 Layer 10(Interactable)이 붙어 있는 예상 외 오브젝트가 존재할 가능성
- 해당 오브젝트의 Transform이 이상한 위치(예: 플레이어 자신, 또는 (0,0,0) 고정)라면 UI가 완전히 틀린 곳에 표시됨

**원인 B — `_rootCanvasRect`가 잘못된 Canvas 참조**
- `InteractionPromptUI.Awake()`에서 `GetComponentInParent<Canvas>()` 실패 시 `FindFirstObjectByType<Canvas>()` 폴백 사용
- 씬에 Canvas가 여러 개 있을 경우 엉뚱한 Canvas의 RectTransform으로 좌표 변환 → UI 위치 완전히 틀어짐
- Screen Space Overlay Canvas(1920×1080)와 다른 Canvas가 혼재할 경우 발생

**원인 C — `_container`가 루트 Canvas 자식이 아닌 다른 곳에 붙음**
- `BuildUI()`에서 `var parent = _rootCanvas != null ? _rootCanvas.transform : transform`
- `_rootCanvas`가 null이면 `InteractionPromptUI` 자신의 transform에 `_container`를 붙임
- `_container.localPosition = lp` 에서 `lp`는 루트 Canvas 기준 좌표인데, 부모가 다른 오브젝트면 좌표계 불일치

### 다음 세션 조사 계획 (우선순위 순)

1. **런타임 Debug.Log로 실제 타겟 확인**
   ```csharp
   // InteractionPromptUI.Show()에 임시 추가
   Debug.Log($"[InteractUI] 타겟: {target.gameObject.name}, 위치: {target.position}");
   ```

2. **씬 내 Layer 10(Interactable) 오브젝트 전체 목록 출력**
   ```csharp
   // 임시 디버그 스크립트 또는 execute_code로:
   var objs = FindObjectsByType<Collider2D>(FindObjectsSortMode.None);
   foreach (var c in objs) if (c.gameObject.layer == 10) Debug.Log(c.gameObject.name);
   ```

3. **`InteractionPromptUI`의 `_rootCanvas`, `_rootCanvasRect` 참조 로그 확인**
   ```csharp
   Debug.Log($"rootCanvas: {_rootCanvas?.name}, renderMode: {_rootCanvas?.renderMode}");
   ```

4. 위에서 원인 확인 후 수정

---

## 씬 설정 현황 (GameScene.unity 기준)

| 오브젝트 | 설정값 |
|---|---|
| `Edrin_NPC` | Layer: 10 (Interactable) |
| `Edrin_NPC` | BoxCollider2D: 로컬 (9×9) ≈ 2.72 world units |
| `Edrin_NPC` | DialoguePlayer 연결, greetingDialogueId: "TUT_001" |
| `NPC_장인` | BoxCollider2D: (2×2.5) world |
| `NPC_상인` | BoxCollider2D: (2×2.5) world |
| `PlayerInteractionDetector` | detectionRadius: 2.0f, interactableMask: Layer 10 |
| Canvas (UI Root) | Screen Space Overlay, 1920×1080 |

---

## 코드 아키텍처 현황

### TutorialManager 흐름 (현재 구현 상태)

```
게임 시작
└─ TutorialManager.Start()
   └─ IntroRoutine() [코루틴]
      ├─ 화면 블러 + 플레이어 Cutscene 상태 진입 (입력 잠금)
      ├─ TUT_001_Intro 대화 재생 (DialoguePlayer)
      ├─ 대화 종료 후 OutlineSequenceRoutine() [나무→돌 순서]
      │   ├─ 카메라 TreeHits로 팬 → 빨간 아웃라인 페이드인 → 유지 → 페이드아웃
      │   └─ 카메라 RockHits로 팬 → 빨간 아웃라인 페이드인 → 유지 → 페이드아웃
      └─ 플레이어 입력 잠금 해제, 자유 이동 시작

플레이어가 Edrin_NPC(2.72m) 범위 진입
└─ PlayerInteractionDetector → "F / 대화" UI 표시 [⚠️ 현재 비정상]
   └─ F키 입력 → NpcInteractable.Interact()
      └─ PlayDialogue("TUT_001") → LockPlayer() → 대화 진행
         └─ 대화 종료 → OnDialogueEnded → UnlockPlayer()

Phase D (곰 전투 유도) → 미구현
```

### 핵심 파일 목록

| 파일 | 역할 |
|---|---|
| `Assets/Scripts/Tutorial/TutorialManager.cs` | 튜토리얼 전체 흐름 제어 상태머신 |
| `Assets/Scripts/Tutorial/TutorialNodeOutline.cs` | 나무·돌 빨간 실루엣 아웃라인 |
| `Assets/Resources/Shaders/SpriteSilhouette.shader` | 단색 실루엣 URP HLSL 셰이더 |
| `Assets/Scripts/MapObject/NpcInteractable.cs` | NPC F키 상호작용 + 대화 중 플레이어 잠금 |
| `Assets/Scripts/UI/InteractionPromptUI.cs` | "F / 대화" 월드 추적 UI |
| `Assets/Scripts/Player/PlayerInteractionDetector.cs` | OverlapCircle 감지 + InteractionPromptUI 제어 |
| `Assets/Scripts/UI/Dialogue/DialoguePlayer.cs` | 대화 진행 (타자기·선택지·이벤트) |

---

## 남은 작업 목록

### 즉시 처리 필요

- [ ] **[BUG HIGH] "대화 UI 이상하게 움직임" 원인 파악 및 수정** (버그 5·6)
  - Debug.Log로 `_targetTransform` 실제 오브젝트 확인
  - Layer 10 오브젝트 전체 목록 확인
  - `_rootCanvasRect` 참조 정확성 확인

### 플레이 검증 필요

- [ ] **아웃라인 빨간색 및 두께** 육안 확인 (나무 ≈1.06×, 돌 ≈1.5×)
- [ ] **대화 중 이동·공격·대시·인벤토리 차단** 확인
- [ ] **대화 종료 후** 이동·공격·대시·인벤토리 정상 복원 확인
- [ ] **MCP로 수정한 NPC 콜라이더** 씬에 실제 반영 여부 에디터에서 확인
  - NPC_장인 BoxCollider2D → (2×2.5) 맞는지
  - NPC_상인 BoxCollider2D → (2×2.5) 맞는지

### 튜토리얼 Phase 미구현

- [ ] **Phase D — 곰 전투 유도**
  - TutorialManager에 BearMonster 등장 Phase 구현
  - 곰 사냥 완료 조건 감지 및 다음 Phase 전환
  - 완료 후 돌검 제작 안내(인벤토리·조합 UI 하이라이트) 연결

### 인벤토리·조합 튜토리얼 (Phase 후속)

- [ ] I키 인벤토리 열기 하이라이트 연출
- [ ] 핫바 배치 하이라이트 연출
- [ ] 숫자키 장비 하이라이트 연출

---

## 참고: 주요 설정값 스냅샷

```
TutorialManager:
  - IntroDialogueId: "TUT_001_Intro"
  - OutlineSequence: [나무(TreeHits SR), 돌(RockHits SR)]
  - OutlineFadeDuration: (TutorialManager 인스펙터 참조)

PlayerInteractionDetector:
  - _detectionRadius: 2.0f
  - _interactableMask: 1024 (Layer 10, "Interactable")
  - _interactKey: KeyCode.F

InteractionPromptUI:
  - _worldOffset: (0, 2, 0)  ← NPC 기준 머리 위 오프셋
  - _container.pivot: (0.5, 0)  ← 하단 중앙
```
