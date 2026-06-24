# 안개(VisionObject) 셰이더 누락 — "잠복 폭탄" 사후분석

작성: 2026-06-21 / 관련 브랜치: `feature/lighting-2d`

## 한 줄 요약
`TrailEraser.shader`(안개 머티리얼이 참조하는 셰이더)가 **오래전 커밋에서 삭제됐지만 머티리얼은 남아 있어**, 디스크상으로는 계속 "셰이더 없는 깨진 머티리얼" 상태였다. Unity의 셰이더 캐시가 이를 가려 화면은 멀쩡해 보였고, 캐시가 무효화되는 순간(브랜치 전환·강제 재임포트·Reimport All·재클론) **마젠타(`Hidden/InternalErrorShader`)로 폴백**한다.

## 증상
- 안개/시야가 적용되는 영역(또는 화면 전체)이 **마젠타**로 표시됨.
- 콘솔에 컴파일 에러는 없음(셰이더 "에러"가 아니라 셰이더 "없음"이라서).

## 근본 원인
- 깨진 머티리얼: `Assets/Resources/Materials/Custom_NewUnlitUniversalRenderPipelineShader.mat`
  - 셰이더 이름: `Custom/NewUnlitUniversalRenderPipelineShader`
  - 참조 셰이더 GUID: `2fece3bb378b9ca4a95db209dff1bdae`
- 그 GUID의 실제 파일 = `Assets/TrailEraser.shader`
- 이 셰이더는 커밋 **`4f03313`(미니맵/전장의 안개 구현)** 에서 추가됐다가, 커밋 **`f39a00a`(인벤토리/슬롯/장비 변경)** 에서 **삭제**됨.
  - 그런데 이를 참조하는 머티리얼과 씬의 `VisionObject`(레이어 `Vision`, `VisionCamera`→`FogMap_RT`로 렌더)는 함께 지워지지 않아 **고아 참조**가 남음.
- `f39a00a`는 이후 거의 모든 커밋(`a488973`, `48ad694`, `5d12dbe` 등)의 **조상**이라, 그 커밋들 어디로 되돌려도 안개는 깨진 상태다.

## 왜 평소엔 멀쩡해 보였나
Unity는 한 번 컴파일한 셰이더를 `Library` 캐시에 GUID로 보관한다. 소스(.shader)가 없어도 캐시본이 있으면 머티리얼이 그대로 렌더된다. 따라서 삭제 이후로도 안개가 정상으로 보였다 — **고쳐져서가 아니라 캐시가 받쳐줬을 뿐**.

## 무엇이 폭탄을 터뜨리나(캐시 무효화 트리거)
- Unity를 켜둔 채 **git 브랜치 전환** (디스크 파일이 통째로 바뀜)
- `AssetDatabase.ImportAsset(..., ForceUpdate)` 등 **강제 재임포트**
- **Reimport All**, `Library/` 삭제, **새로 clone**
→ 위 중 하나라도 발생하면 머티리얼의 셰이더를 다시 찾으려다 실패 → `Hidden/InternalErrorShader`(마젠타).

## 영구 해결책 (코딩 아님, git 복원 한 줄)
```
git checkout f39a00a~1 -- Assets/TrailEraser.shader Assets/TrailEraser.shader.meta
```
- `f39a00a~1`은 삭제 직전 커밋이라 동일 GUID(`2fece3bb…`)의 셰이더가 그대로 복원된다.
- 이 셰이더가 살아있는 다른 브랜치(예: `feature/beta`, `feature/BattleSystemBE` 등)에서 가져와도 무방(같은 GUID).
- 복원 후 검증: 머티리얼 셰이더가 `Custom/NewUnlitUniversalRenderPipelineShader`로 바인딩되고 `isSupported = true`, 씬 깨진 머티리얼 0개, `FogMap_RT` 중앙 픽셀이 회색(≈0.49)으로 정상.

## 적용 기록
- 2026-06-21: 위 `git checkout`으로 `TrailEraser.shader`(+`.meta`) 복원 완료. 머티리얼 재바인딩·RT 정상 확인.

## 검증 시 주의 (스크린샷 함정)
이 프로젝트는 **멀티 카메라(메인 + `VisionCamera`→`FogMap_RT` 안개 합성) + 포스트프로세싱(Bloom 등)** 구조다.
- 에디터 스크립트에서 `Camera.main.Render()`를 임시 RenderTexture로 단발 렌더하거나 `ScreenCapture`로 찍으면, 이 전체 프레임 파이프라인이 재현되지 않아 **실제와 다른 결과(마젠타/흰 화면)** 가 나올 수 있다.
- 따라서 안개/포스트프로세싱 관련 검증은 **실제 Game 뷰**로 확인할 것. 머티리얼 상태(`shader.isSupported`, `FogMap_RT` 픽셀색)는 프로그램적으로 확인하는 편이 신뢰도가 높다.

## 재발 방지 메모
- 셰이더/스크립트를 삭제할 때는 **그것을 참조하는 머티리얼/프리팹/씬 오브젝트**도 함께 정리하거나 참조를 교체할 것(고아 참조 금지).
- 비슷한 고아 참조 후보: `MapGenerator`가 런타임 전 null 머티리얼 상태(생성기라 정상일 수 있으나 확인 권장).
