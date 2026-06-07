/// <summary>
/// 대화 진행 로직의 추상화 인터페이스 (DIP/OCP 준수).
/// UI 바인더는 이 인터페이스에만 의존하며,
/// 향후 분기 대화, 선택지 등을 구현할 때 새 구현체를 추가하면 됩니다.
/// </summary>
public interface IDialoguePlayer
{
    /// <summary>
    /// 대화 시작 시 발생하는 이벤트.
    /// </summary>
    event System.Action OnDialogueStarted;

    /// <summary>
    /// 대화 종료 시 발생하는 이벤트.
    /// </summary>
    event System.Action OnDialogueEnded;

    /// <summary>
    /// 현재 줄이 변경될 때 발생합니다.
    /// 매개변수: (화자 CharacterSO, 전체 텍스트 string)
    /// </summary>
    event System.Action<CharacterSO, string> OnLineChanged;

    /// <summary>
    /// 타자기 효과로 현재까지 출력된 텍스트가 갱신될 때 발생합니다.
    /// 매개변수: (현재까지 출력된 부분 텍스트 string)
    /// </summary>
    event System.Action<string> OnTextUpdated;

    /// <summary>
    /// 현재 줄의 텍스트가 모두 표시 완료되었을 때 발생합니다.
    /// </summary>
    event System.Action OnLineCompleted;

    /// <summary>
    /// 대화가 현재 진행 중인지 여부.
    /// </summary>
    bool IsPlaying { get; }

    /// <summary>
    /// 현재 줄의 텍스트가 전부 출력되었는지 여부.
    /// </summary>
    bool IsLineComplete { get; }

    /// <summary>
    /// 대화를 시작합니다.
    /// </summary>
    /// <param name="dialogue">출력할 대화 데이터</param>
    void StartDialogue(DialogueSO dialogue);

    /// <summary>
    /// 클릭(진행) 처리.
    /// 타이핑 중이면 현재 문장을 즉시 완성하고,
    /// 텍스트가 이미 완료된 상태면 다음 문장으로 넘어갑니다.
    /// </summary>
    void Advance();
}
