# 💀 사망 및 부활 연출 테스트 가이드 (최종 통합본)

이 문서는 게임 내에서 플레이어가 사망하고 다시 부활하기까지의 모든 과정을 **어떻게 테스트하고 무엇을 확인해야 하는지** 자세하게 설명합니다. 

> **마지막 업데이트:** 2026-04-07  
> **담당 씬:** `Assets/Scenes/Unitro.unity`

### 📋 테스트 결과 표시법
테스트를 진행하시면서 아래 기호를 사용해 상태를 업데이트해 주세요!
- `[T]` : 문제 없음 (확인 완료)
- `[F]` : 이상함/버그 있음 (확인 필요)
- `[E]` : 수정이 필요함 (개선 요망)

---

## 1단계: 플레이어 사망 (The Moment of Death)
플레이어의 체력이 0이 되었을 때, '죽음'이 자연스럽고 임팩트 있게 느껴지는지 확인합니다.

- [T] **무기 숨김**: 죽는 순간 들고 있던 무기(`WeaponPivot`)가 즉시 사라지나요?
- [T] **슬로우 모션**: 죽자마자 아주 잠깐(0.2초 정도) 화면이 느려졌다가 완전히 멈추나요?
- [T] **금색 소멸 효과**: 캐릭터가 위에서부터 아래로 금색 빛을 내며 서서히 사라지나요? (이때 경계선이 반짝이는지 확인!)
- [T] **픽셀 파티클**: 캐릭터가 사라진 자리에서 금색 가루들이 하늘로 소용돌이치며 올라오나요?

---

## 2단계: 사망 화면 UI (Death Screen)
모든 연출이 멈추고 나서, 유저에게 "죽었다"는 사실을 알리고 부활 버튼을 보여주는 단계입니다.

- [T] **검은 화면 페이드**: 화면 전체가 부드러운 검은색 패널로 덮이나요? (0.8초 동안 서서히 어두워져야 합니다.)
- [T] **"Dead" 문구와 "부활" 버튼**: 검은 화면이 나온 뒤 잠시 후(0.5초), "Dead" 글자와 부활 버튼이 나타나나요?
- [T] **버튼 마우스 반응**: '부활' 버튼에 마우스를 올렸을 때 글자 색이 **흰색에서 붉은색**으로 변하나요? 마우스를 떼면 다시 돌아오나요?
- [T] **조작 차단**: 이 화면이 떠 있는 동안에는 캐릭터를 움직이거나 공격할 수 없어야 합니다.

---

## 3단계: 부활 시작 - 페이즈 1 (충격!) 💥
"부활" 버튼을 누르자마자 발생하는 강렬한 충격 효과를 확인합니다.

- [E] **화면 진동**: 버튼을 누르는 순간 카메라가 '쿵!' 하고 흔들리나요? (임펄스 소스 작동 확인) -> 흔들리기는 하는데, 부활 버튼을 누르고 부활 ui들이 사라지며 The referenced script (Unknown) on this Behaviour is missing!

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

성공
UnityEngine.Debug:Log (object)
BaseMapObject:SetIndicator (bool) (at Assets/Scripts/MapObject/BaseMapObject.cs:61)
BaseMapObject:OnEnable () (at Assets/Scripts/MapObject/BaseMapObject.cs:39)
UnityEngine.Object:Instantiate<UnityEngine.GameObject> (UnityEngine.GameObject,UnityEngine.Transform)
MapGenerator:TrySpawnObject (TerrainChunk,BiomeSetting) (at Assets/Scripts/MapGenerator.cs:187)
TerrainChunk:GenerateContent () (at Assets/Scripts/MapGenerator.cs:271)
TerrainChunk:.ctor (UnityEngine.Vector2Int,MapGenerator) (at Assets/Scripts/MapGenerator.cs:241)
MapGenerator:UpdateVisibleChunks () (at Assets/Scripts/MapGenerator.cs:85)
MapGenerator:Update () (at Assets/Scripts/MapGenerator.cs:56)

[Player] 사망 → 카르마 1pt
UnityEngine.Debug:Log (object)
PlayerEntity:OnDeath () (at Assets/Scripts/Player/PlayerEntity.cs:47)
LivingEntity:HandleDeath () (at Assets/Scripts/Core/LivingEntity.cs:42)
HealthSystem:ApplyDamage (single) (at Assets/Scripts/Core/HealthSystem.cs:75)
LivingEntity:TakeDamage (single,UnityEngine.GameObject) (at Assets/Scripts/Core/LivingEntity.cs:33)
Enemy:OnCollisionStay2D (UnityEngine.Collision2D) (at Assets/Scripts/Enemy/Enemy.cs:34)

[Player] Death 상태 진입
UnityEngine.Debug:Log (object)
DeathState:Enter () (at Assets/Scripts/Player/FSM/States/DeathState.cs:24)
PlayerStateMachine:TransitionTo (PlayerState) (at Assets/Scripts/Player/FSM/PlayerStateMachine.cs:83)
PlayerStateMachine:HandleDied () (at Assets/Scripts/Player/FSM/PlayerStateMachine.cs:96)
HealthSystem:ApplyDamage (single) (at Assets/Scripts/Core/HealthSystem.cs:75)
LivingEntity:TakeDamage (single,UnityEngine.GameObject) (at Assets/Scripts/Core/LivingEntity.cs:33)
Enemy:OnCollisionStay2D (UnityEngine.Collision2D) (at Assets/Scripts/Enemy/Enemy.cs:34)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.Rendering.RenderPipelineManager:DoRenderLoop_Internal (UnityEngine.Rendering.RenderPipelineAsset,intptr,UnityEngine.Object,Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.Rendering.RenderPipelineManager:DoRenderLoop_Internal (UnityEngine.Rendering.RenderPipelineAsset,intptr,UnityEngine.Object,Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.Rendering.RenderPipelineManager:DoRenderLoop_Internal (UnityEngine.Rendering.RenderPipelineAsset,intptr,UnityEngine.Object,Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.Rendering.RenderPipelineManager:DoRenderLoop_Internal (UnityEngine.Rendering.RenderPipelineAsset,intptr,UnityEngine.Object,Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.Rendering.RenderPipelineManager:DoRenderLoop_Internal (UnityEngine.Rendering.RenderPipelineAsset,intptr,UnityEngine.Object,Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.Rendering.RenderPipelineManager:DoRenderLoop_Internal (UnityEngine.Rendering.RenderPipelineAsset,intptr,UnityEngine.Object,Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.Rendering.RenderPipelineManager:DoRenderLoop_Internal (UnityEngine.Rendering.RenderPipelineAsset,intptr,UnityEngine.Object,Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.Rendering.RenderPipelineManager:DoRenderLoop_Internal (UnityEngine.Rendering.RenderPipelineAsset,intptr,UnityEngine.Object,Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.Rendering.RenderPipelineManager:DoRenderLoop_Internal (UnityEngine.Rendering.RenderPipelineAsset,intptr,UnityEngine.Object,Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.Rendering.RenderPipelineManager:DoRenderLoop_Internal (UnityEngine.Rendering.RenderPipelineAsset,intptr,UnityEngine.Object,Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.Rendering.RenderPipelineManager:DoRenderLoop_Internal (UnityEngine.Rendering.RenderPipelineAsset,intptr,UnityEngine.Object,Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.Rendering.RenderPipelineManager:DoRenderLoop_Internal (UnityEngine.Rendering.RenderPipelineAsset,intptr,UnityEngine.Object,Unity.Collections.LowLevel.Unsafe.AtomicSafetyHandle)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&)

The render pass ColorInvertRendererFeature+ColorInvertPass does not have an implementation of the RecordRenderGraph method. Please implement this method, or consider turning on Compatibility Mode (RenderGraph disabled) in the menu Edit > Project Settings > Graphics > URP. Otherwise the render pass will have no effect. For more information, refer to https://docs.unity3d.com/Packages/com.unity.render-pipelines.universal@latest/index.html?subfolder=/manual/customizing-urp.html.
UnityEngine.GUIUtility:ProcessEvent (int,intptr,bool&)

가 뜨고 한 2~3초 뒤에 효과가 나타납니다.
또한 플레이어는 0,0지점에서 나타나야하는데 죽은지점에서 부활하는 버그가 있습니다.

- [F] **색상 반전**: 화면 색깔이 순간적으로 반전(반전 영화처럼)되었다가 다시 돌아오나요? -> 아니요

- [F] **빛 번짐 (색수차)**: 화면 가장자리가 무지개처럼 약간 번졌다가 사라지나요? -> 아니요

---

## 4단계: 부활 진행 - 페이즈 2 (다시 살아나기) 👹
캐릭터의 형체가 아래에서부터 다시 차오르는 연출입니다.

- [F] **역방향 소멸 효과**: 사라질 때와 반대로, 발끝에서부터 머리끝까지 캐릭터가 다시 나타나나요? -> 아니요 
- [E] **붉은색 테두리**: 캐릭터가 나타나는 경계선에 붉은색 빛이 감도나요?
-> 네, 근데 몇초뒤에 플레이어 스프라이트 자체가 안보이게 바뀌어요.

- [F] **붉은 비네트**: 화면 가장자리에 어두운 붉은색 그림자가 생겼다가 캐릭터가 다 나타나면 사라지나요? -> 아니요 

---

## 5단계: 부활 마무리 - 페이즈 3 (회복과 조작) ✨
완전히 살아난 직후 월드로 복귀하는 과정입니다.

- [F] **채도와 노이즈**: 화면이 잠시 흑백처럼 변했다가 다시 원래 색깔로 돌아오나요? (동시에 지직거리는 노이즈도 사라져야 합니다.)
-> 아예 그런 효과 자체가 없습니다.
- [E] **시간 흐름 복구**: 멈춰있던 적들과 환경이 다시 움직이기 시작하나요? (TimeScale=1) -> 네, 근데 타이밍이 너무 빠릅니다. 모든 연출이 끝나고 진행되어야 합니다.(이건 그냥 지금 연출 일부가 버그로 안나타나서 그렇게 느끼는걸수도 있습니다.)
- [T] **붉은 잔상 (Trail)**: 플레이어가 움직일 때 뒤에 붉은색 실루엣(잔상)이 약 2초 동안 멋지게 따라붙나요? -> 네 근데 해당 잔상이 생긴지 2초 이후 플레이어가 안보입니다.
- [F] **카르마 알림**: 화면 상단에 "카르마가 쌓인것 같다"는 메시지가 떴다가 사라지나요? -> 아예 안보입니다.

---


## 🛠️ 관리자용 설정 도움말 (Setup Guide)

연출이 제대로 작동하지 않는 것 같을 때 확인해 보세요.

### 1. 참조 연결 자동화 메뉴
씬을 새로 만들거나 오브젝트가 바뀌었을 때 실행하세요:
- `Tools > Setup DeathScreenUI References` : 모든 UI와 컨트롤러 연결
- `Tools > Setup Phase1 Scene Objects` : 카메라 진동 및 특수 효과 설정
- `Tools > Add ColorInvert Renderer Feature` : 색상 반전 기능 등록 (PC 전용)

### 2. 세밀한 느낌 조정 (Tuning)
`Player` 오브젝트의 `DeathCutsceneController` 컴포넌트에서 값을 직접 바꿀 수 있습니다:
- **진동이 너무 약해요**: `_impulseForce` 값을 높이세요. (추천: 1.8 ~ 3.0)
- **무지개 번짐이 너무 심해요**: `_maxChromaticAberration` 값을 낮추세요.
- **화면이 너무 붉어요**: `_maxVignetteIntensity` 값을 낮추세요.

---

## 📁 관련 파일 리스트 (참고용)

| 기능명 | 파일 경로 |
| :--- | :--- |
| 전체 흐름 제어 | `DeathCutsceneController.cs` |
| UI 페이드 및 버튼 | `DeathScreenUI.cs` |
| 캐릭터 재등장 셰이더 | `PlayerResurrectDissolve.shader` |
| 붉은 잔상 효과 | `ResurrectionTrailPool.cs` |
| 색상 반전 모듈 | `ColorInvertRendererFeature.cs` |

---

## 남은 작업 리스트

- [ ] 전체 연출 느낌 확인 및 값 조정 (인스펙터 튜닝)
- [ ] 모바일 환경에서 색상 반전이 나오는지 테스트 (`Mobile_Renderer.asset` 확인 필요)
- [ ] 카르마가 실제 데이터(`KarmaHandler`)에 1씩 잘 쌓이는지 확인
