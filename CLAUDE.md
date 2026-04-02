# 프로젝트 개요 (Project Overview)
- **프로젝트 명:** Unitrio 2D Top-Down Action (Unity 3D URP 기반)
- **장르:** 2D 탑다운 액션 / 샌드박스
- **환경:** Unity 3D Core, Universal Render Pipeline (URP), C#
- **특징:** 3D URP 환경을 사용하지만 시각적/구조적으로는 2D 탑다운 시점을 구현함. 액션 게임이므로 빠르고 정확한 판정과 부드러운 프레임 유지가 필수.

# AI 코딩 가이드라인 및 규칙 (AI Coding Guidelines & Rules)
이 프로젝트의 코드를 작성하거나 수정할 때, 다음의 엄격한 규칙들을 반드시 준수하여 코드를 제안해야 합니다.

## 1. 퍼포먼스와 메모리 관리 (Performance & Memory Management)
- **가비지 컬렉션(GC) 최소화:** `Update()`, `FixedUpdate()`, `LateUpdate()` 등 매 프레임 호출되는 메서드 내에서 새로운 객체(`new`)를 생성하거나 문자열을 조합(`+`)하지 마세요.
- **오브젝트 풀링(Object Pooling) 필수:** 총알, 이펙트(VFX), 몬스터, 데미지 텍스트 등 빈번하게 생성/파괴되는 모든 객체는 반드시 Object Pool 패턴을 사용하여 구현하세요. `Instantiate`와 `Destroy`는 게임 플레이 도중에 절대로 남발해서는 안 됩니다.
- **LINQ 사용 금지:** 성능 저하와 불필요한 메모리 할당을 유발하는 LINQ(`Where`, `ToList`, `Select` 등)는 런타임 게임 루프 안에서 사용하지 마세요. 반드시 `for` 또는 `foreach` 루프를 사용하세요.
- **캐싱(Caching):** `GetComponent<T>()`, `Camera.main`, `FindObjectOfType<T>()`, `GameObject.Find()` 등은 무거운 연산입니다. 반드시 `Awake()`나 `Start()`에서 호출하여 변수에 캐싱한 뒤 사용하세요.
- **코루틴 최적화:** `yield return new WaitForSeconds()`는 매번 새로운 객체를 생성합니다. 캐싱해두고 재사용하거나, 타이머 변수를 활용한 `Update` 방식, 또는 UniTask(도입 시)를 권장합니다.

## 2. 아키텍처 및 디자인 패턴 (Architecture & Design Patterns)
- **싱글톤(Singleton) 남용 방지:** GameManager, AudioManager 등 전역 접근이 꼭 필요한 매니저 클래스에만 제한적으로 사용하세요. 컴포넌트 간의 결합도를 낮추기 위해 이벤트 기반 아키텍처(C# `event`, `Action` 또는 Observer 패턴)를 적극 활용하세요.
- **데이터와 로직의 분리:** 몬스터 스탯, 아이템 정보, 웨폰 데이터 등 변하지 않는 기획 데이터는 프리팹 내부에 하드코딩하지 말고 반드시 `ScriptableObject`를 사용하여 관리하세요.
- **상태 관리 (State Machine):** 플레이어와 보스 몬스터의 행동 패턴(Idle, Move, Attack, Dash 등)은 `switch-case`로 떡칠하지 말고, 유한 상태 기계(FSM, Finite State Machine) 패턴이나 State 패턴을 적용하여 확장 가능하게 작성하세요.

## 3. URP 및 3D 탑다운 액션 특화 규칙 (URP & Top-Down Action Rules)
- **물리 연산 (Physics):** 2D 탑다운이지만 3D 공간을 사용하므로, 충돌 처리 시 `Rigidbody`와 `Collider`(3D)를 쓸지, `Rigidbody2D`와 `Collider2D`를 쓸지 혼용하지 마세요. (프로젝트 설정에 따라 일관되게 작성할 것). 충돌 감지 로직은 최대한 가볍게 작성하고, 레이어 매트릭스(Layer Collision Matrix)를 활용할 수 있도록 레이어를 분리해서 제안하세요.
- **Z축/Y축 깊이 정렬 (Depth Sorting):** 3D 공간의 2D 탑다운 뷰에서는 캐릭터나 오브젝트가 겹칠 때 Y축(또는 Z축) 위치에 따라 카메라 앞에 렌더링되어야 합니다. 관련 로직 작성 시 이를 항상 고려하세요.
- **벡터 연산 주의:** 거리 계산이 필요할 때 `Vector3.Distance`나 `Vector3.Magnitude` 대신 연산이 가벼운 `Vector3.sqrMagnitude`를 우선적으로 고려하세요.

## 4. 코드 스타일 및 응답 형식 (Code Style & Output Format)
- **명명 규칙:** - 클래스 및 메서드: `PascalCase`
  - 퍼블릭 필드 및 프로퍼티: `PascalCase`
  - 프라이빗/프로텍티드 필드: `_camelCase`
  - 로컬 변수 및 매개변수: `camelCase`
- **방어적 프로그래밍:** 널(null) 참조 오류를 방지하기 위해 필요한 곳에 널 체크(`?.`, `??`, `if (obj == null)`)를 포함하세요.
- **설명과 주석:** 코드를 제공할 때 "왜 이렇게 작성하는 것이 성능에 좋은지" 핵심 이유를 짧게 주석이나 설명으로 덧붙여주세요. 쓸데없이 장황한 부연 설명은 생략하고, 코드 자체의 품질과 구조에 집중하세요.
- **이상한 컴포넌트/플러그인 지양:** 기본 Unity API만으로 해결할 수 있는 문제에 대해 무거운 외부 라이브러리나 복잡한 우회 코드를 제안하지 마세요. 직관적이고 표준화된 방식을 최우선으로 합니다.

## 5. 의존성 주입 및 컴포넌트 참조 (Dependency & Reference)
- **Find 계열 함수 절대 사용 금지:** `GameObject.Find()`, `FindObjectOfType()`, `FindGameObjectWithTag()` 등 씬 전체를 순회하며 오브젝트를 찾는 메서드는 런타임은 물론이고 `Awake()`나 `Start()`에서도 최대한 사용을 피하세요.
- **명시적 참조 (Explicit Reference) 우선:** 컴포넌트나 다른 클래스의 참조가 필요할 경우, 억지로 스크립트 안에서 찾으려 하지 말고 반드시 `[SerializeField]`를 사용하여 유니티 인스펙터(Inspector) 창에서 직접 할당(Drag & Drop)받도록 코드를 작성하세요.
- **결합도 낮추기:** 서로 다른 게임 오브젝트 간의 통신이 필요할 때는 직접 참조를 엮기보다는 C# `event`, `Action`을 이용한 이벤트 기반 통신이나 `ScriptableObject`를 채널로 활용하는 방식을 우선적으로 제안하세요.