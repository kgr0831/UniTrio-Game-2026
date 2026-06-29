# 튜토리얼 개발 계획서

> 최종 업데이트: 2026-06-28  
> 브랜치: `feature/Tutorial`  
> 씬: `GameScene.unity`

---

## 목표 흐름

```
[게임 시작]
  → 마법사 NPC 인사 대화
  → Phase 1: 돌 / 나무 채집 안내    (채집 → 인벤토리 확인)
  → Phase 2: 크래프팅 안내           (I/K 패널 → 돌검 제작)
  → Phase 3: 핫바 장착 안내          (핫바 1번 슬롯 → 장착)
  → Phase 4: 곰 전투 안내            (왼쪽에서 곰 등장 → 차징스킬 격파)
  → 튜토리얼 완료
```

---

## Phase별 작업 현황

### ✅ Phase 0 — 공통 인프라 (완료)

| 파일 | 변경 내용 |
|------|-----------|
| `Assets/Scripts/Data/Dialogue/DialogueSO.cs` | `DialogueChoice` 클래스 + `Choices[]` 필드 추가 |
| `Assets/Scripts/Interface/IDialoguePlayer.cs` | `OnChoicesRequired`, `IsWaitingForChoice`, `CurrentDialogue`, `SelectChoice` 추가 |
| `Assets/Scripts/UI/Dialogue/DialoguePlayer.cs` | 선택지 분기, `SelectChoice(null/SO)` 지원 전면 재작성 |
| `Assets/Scripts/UI/Dialogue/DialogueUIBinder.cs` | F/Enter/Space 진행키, 입력차단, 초상화 디밍, 선택지 버튼 UI 전면 재작성 |
| `Assets/Scripts/Data/Dialogue/DialogueDatabase.cs` | `Register()` / `RegisterAll()` 런타임 등록 추가 |
| `Assets/Scripts/Data/Dialogue/DialogueCSVLoader.cs` | **신규** — CSV → DialogueSO 런타임 파서 |
| `Assets/Scripts/MapObject/NpcInteractable.cs` | `_revisitDialogueId`, `_hasGreeted` 재방문 대화 지원 |
| `Assets/Scripts/Object/GatherableNode.cs` | `OnAnyNodeDestroyed` 정적 이벤트 추가 |
| `Assets/Scripts/Tutorial/TutorialFocusUI.cs` | **신규** — 월드/슬롯 위 화살표 포커스 오버레이 |
| `Assets/Scripts/UI/InteractionPromptUI.cs` | DashUI 스타일 상호작용 프롬프트, Canvas 좌표 보정 |
| `GameScene.unity` (MCP) | InteractionUI 활성화, NPC 3개 Layer→10(Interactable) 변경, Test 오브젝트 삭제 |

---

### 🔲 Phase 1 — 채집 안내 (다음 작업)

**목표:** 플레이어가 화면에 표시된 채집물(돌/나무)을 공격해 아이템을 얻도록 유도

**세부 작업:**

1. `TutorialManager` 채집 단계에서 `TutorialFocusUI.Instance.ShowWorldTarget(nearestNode, "이걸 공격하세요!")` 호출  
2. `GatherableNode.OnAnyNodeDestroyed` 구독 → 목표 개수 달성 시 `TutorialFocusUI.Instance.Hide()`  
3. 채집 완료 후 인벤토리 열기 힌트 텍스트 표시 (I키 안내)  
4. **검증:** 채집 횟수 카운터가 올바르게 감소하는지, FocusUI 화살표가 채집물 위에 정확히 위치하는지 확인

**관련 파일:**
- `Assets/Scripts/Tutorial/TutorialManager.cs` — 채집 단계 로직 추가
- `Assets/Scripts/Tutorial/TutorialFocusUI.cs` — 이미 구현됨 (Phase 0)
- `Assets/Scripts/Object/GatherableNode.cs` — `OnAnyNodeDestroyed` 이미 구현됨

---

### 🔲 Phase 2 — 크래프팅 안내

**목표:** 크래프팅 패널(I/K)을 열고 돌검을 제작하도록 유도

**세부 작업:**

1. 채집 완료 후 `CraftingUIManager` 패널 열기 힌트  
2. 레시피 아이템별 순차 포커스 → `TutorialFocusUI.Instance.ShowSlotTarget(slotRect, "재료")`  
3. 제작 버튼 포커스 후 제작 완료 감지 (`CraftingUIManager.OnItemCrafted` 이벤트 구독)  
4. 중간 대화: "돌검이 완성됐어요!"

**관련 파일:**
- `Assets/Scripts/UI/CraftingUIManager.cs`
- `Assets/Scripts/Tutorial/TutorialManager.cs`

---

### 🔲 Phase 3 — 핫바 장착 안내

**목표:** 인벤토리에서 돌검을 드래그해 핫바 1번 슬롯에 장착하도록 유도

**세부 작업:**

1. 시작 무기를 목검에서 **빈 슬롯**으로 변경 (또는 목검 제거)  
2. 인벤토리 돌검 슬롯 포커스 → 핫바 1번 슬롯 포커스  
3. 장착 완료 감지 (`HotbarManager.OnWeaponEquipped` 이벤트 필요 — 없으면 추가)  
4. 힌트: "1번 키를 눌러 무기를 꺼내세요"

**관련 파일:**
- `Assets/Scripts/UI/HotbarManager.cs` (또는 인벤토리 관련 스크립트)
- `Assets/Scripts/Tutorial/TutorialManager.cs`

---

### 🔲 Phase 4 — 곰 전투 안내

**목표:** 튜토리얼 곰을 차징 스킬로 격파하도록 유도

**세부 작업:**

1. 곰을 플레이어 **왼쪽**에서 스폰 (TutorialManager 스포너 위치 조정)  
2. 곰 등장 시 카메라 팬 + 타임 스톱 연출 (1~2초)  
3. 차징 스킬 힌트 표시: "마우스 우클릭 홀드 → 차징, 해제 → 발사"  
4. **버그 수정:** `TutorialBearGuard.ShouldBlock()` — 차징 스킬 외 다른 공격도 통과하도록 조건 완화 또는 제거 검토  
5. 곰 사망 감지 → 튜토리얼 완료 대화

**관련 파일:**
- `Assets/Scripts/Tutorial/TutorialBearGuard.cs`
- `Assets/Scripts/Tutorial/TutorialManager.cs`
- `Assets/Scripts/Monster/Base/BearMonster.cs`

---

## CSV 대화 파일 구조

> `Assets/Resources/Dialogue/Tutorial.csv` (예정)

```
dialogue_id,speaker,text,choice1_text,choice1_next,choice2_text,choice2_next
TUT_001,마법사,"안녕하세요, 모험가님!",,,
TUT_002,마법사,"먼저 채집부터 배워봅시다.",,,
TUT_003,마법사,"돌이나 나무를 공격해 재료를 모으세요.",,,
TUT_CRAFT_001,마법사,"재료를 모았군요! I키를 눌러 제작 창을 열어보세요.",,,
TUT_CRAFT_002,마법사,"돌검을 제작하면 전투가 가능합니다.",,,
TUT_BEAR_001,마법사,"적이 나타났습니다! 마우스 우클릭을 홀드해 차징 스킬을 사용하세요.",,,
TUT_BEAR_002,마법사,"잘하셨습니다! 이제 본격적인 모험이 시작됩니다.",,,
```

---

## 씬 Inspector 세팅 (남은 작업)

`DialogueUIBinder` 컴포넌트에 아래 필드를 연결해야 합니다:

| 필드 | 값 |
|------|----|
| `_dialogueDb` | `DialogueDatabase` SO |
| `_interactionDetector` | Player의 `PlayerInteractionDetector` |
| `_portraitLeft` | (선택) 왼쪽 초상화 Image |
| `_portraitRight` | (선택) 오른쪽 초상화 Image |
| `_choiceContainer` | 선택지 버튼 부모 RectTransform |
| `_choiceButtonPrefab` | (선택) 커스텀 버튼 프리팹 |

`TutorialFocusUI` 컴포넌트:
- Canvas 하위 빈 GameObject에 붙이면 자동으로 루트 Canvas를 탐색해 UI를 생성합니다.

---

## 알려진 이슈 / 리스크

| 이슈 | 상태 |
|------|------|
| `TutorialBearGuard` 가 차징 스킬 외 공격 차단 | Phase 4에서 수정 예정 |
| 속성 게이지 적립 경로 미확인 | 조사 필요 |
| `FloatingMagneticItem.InitDrop` 시그니처 확인 필요 | GatherableNode 참조 중 |
| `SimpleObjectPool` null 체크 방어 코드 없음 | GatherableNode SpawnLoot 내부 |
| DialogueUIBinder 선택지 버튼 `_choiceButtonPrefab` 미지정 시 fallback 버튼 생성 | 폰트 불일치 가능 |
