/// <summary>
/// 대화 진행 로직의 추상화 인터페이스 (DIP/OCP 준수).
/// </summary>
public interface IDialoguePlayer
{
    event System.Action OnDialogueStarted;
    event System.Action OnDialogueEnded;
    event System.Action<CharacterSO, string> OnLineChanged;
    event System.Action<string> OnTextUpdated;
    event System.Action OnLineCompleted;

    /// <summary>선택지가 필요할 때 발생. UIBinder가 버튼을 표시하고 SelectChoice를 호출해야 함.</summary>
    event System.Action<DialogueChoice[]> OnChoicesRequired;

    bool IsPlaying { get; }
    bool IsLineComplete { get; }

    /// <summary>선택지 입력 대기 중이면 true. Advance()를 무시해야 함.</summary>
    bool IsWaitingForChoice { get; }

    /// <summary>현재 재생 중인 줄 인덱스 (0-based). 연출 바인딩 비교용.</summary>
    int CurrentLineIndex { get; }

    /// <summary>true이면 Advance()가 차단됨. 아웃라인 연출 중 등 외부에서 잠글 때 사용.</summary>
    bool IsAdvanceLocked { get; }

    /// <summary>현재 재생 중인 DialogueSO (초상화 배치용). 없으면 null.</summary>
    DialogueSO CurrentDialogue { get; }

    void StartDialogue(DialogueSO dialogue);
    void Advance();
    void LockAdvance();
    void UnlockAdvance();

    /// <summary>플레이어가 선택지를 고른 뒤 UIBinder가 호출. nextDialogue=null이면 대화 종료.</summary>
    void SelectChoice(DialogueSO nextDialogue);
}
