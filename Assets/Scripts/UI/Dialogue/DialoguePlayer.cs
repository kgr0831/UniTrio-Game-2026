using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 대화 진행 핵심 로직 (SRP: 대화 상태 관리 및 타자기 효과만 담당).
/// IDialoguePlayer 인터페이스를 구현하여 UI 바인더와 분리됩니다.
/// 코루틴 기반 타자기 효과, 클릭 처리, 문장 전환을 수행합니다.
/// </summary>
public class DialoguePlayer : MonoBehaviour, IDialoguePlayer
{
    [Header("Typing Settings")]
    [Tooltip("한 글자당 출력 간격 (초). 값이 작을수록 빠르게 출력됩니다.")]
    [SerializeField] private float _typingSpeed = 0.05f;

    // ── IDialoguePlayer 이벤트 ──
    public event Action OnDialogueStarted;
    public event Action OnDialogueEnded;
    public event Action<CharacterSO, string> OnLineChanged;
    public event Action<string> OnTextUpdated;
    public event Action OnLineCompleted;

    // ── IDialoguePlayer 프로퍼티 ──
    public bool IsPlaying { get; private set; }
    public bool IsLineComplete { get; private set; }

    // ── 내부 상태 ──
    private DialogueSO _currentDialogue;
    private int _currentLineIndex;
    private string _fullText;
    private Coroutine _typingCoroutine;

    /// <summary>
    /// 대화를 시작합니다. 이미 진행 중인 대화가 있으면 강제 종료 후 새 대화를 시작합니다.
    /// </summary>
    /// <param name="dialogue">출력할 대화 데이터</param>
    public void StartDialogue(DialogueSO dialogue)
    {
        if (dialogue == null || dialogue.LineCount == 0)
        {
            Debug.LogWarning("[DialoguePlayer] 대화 데이터가 비어있거나 null입니다.");
            return;
        }

        // 이미 진행 중인 대화가 있으면 정리
        if (IsPlaying)
        {
            StopDialogueImmediate();
        }

        _currentDialogue = dialogue;
        _currentLineIndex = 0;
        IsPlaying = true;

        // 콜백 발생: 코드 기반 Action + SO의 UnityEvent
        OnDialogueStarted?.Invoke();
        _currentDialogue.OnDialogueStart?.Invoke();

        DisplayCurrentLine();
    }

    /// <summary>
    /// 클릭(진행) 처리.
    /// 타이핑 중이면 현재 문장을 즉시 완성하고,
    /// 텍스트가 이미 완료된 상태면 다음 문장으로 넘어갑니다.
    /// </summary>
    public void Advance()
    {
        if (!IsPlaying) return;

        // J-8 대화 진행(넘김/완성)음
        if (AudioManager.Instance != null) AudioManager.Instance.PlayDialogueAdvance();

        if (!IsLineComplete)
        {
            // 타이핑 중 → 즉시 완성
            CompleteLine();
        }
        else
        {
            // 텍스트 완료 → 다음 문장으로
            _currentLineIndex++;

            if (_currentLineIndex < _currentDialogue.LineCount)
            {
                DisplayCurrentLine();
            }
            else
            {
                EndDialogue();
            }
        }
    }

    /// <summary>
    /// 현재 인덱스의 대화 줄을 표시합니다 (타자기 효과 시작).
    /// </summary>
    private void DisplayCurrentLine()
    {
        DialogueLine line = _currentDialogue.Lines[_currentLineIndex];
        _fullText = line.Text;
        IsLineComplete = false;

        // 줄 변경 이벤트 발생 (UI 바인더가 화자 이름/초상화 갱신)
        OnLineChanged?.Invoke(line.Speaker, _fullText);

        // 이전 타이핑 코루틴 중지 후 새로 시작
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
        }
        _typingCoroutine = StartCoroutine(TypeTextCoroutine());
    }

    /// <summary>
    /// 타자기 효과 코루틴. 한 글자씩 텍스트를 출력합니다.
    /// </summary>
    private IEnumerator TypeTextCoroutine()
    {
        int charIndex = 0;
        int totalLength = _fullText.Length;

        while (charIndex < totalLength)
        {
            charIndex++;
            string partialText = _fullText.Substring(0, charIndex);
            OnTextUpdated?.Invoke(partialText);
            yield return new WaitForSecondsRealtime(_typingSpeed);
        }

        // 모든 글자 출력 완료
        MarkLineComplete();
    }

    /// <summary>
    /// 현재 문장의 텍스트를 즉시 완성합니다.
    /// </summary>
    private void CompleteLine()
    {
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }

        OnTextUpdated?.Invoke(_fullText);
        MarkLineComplete();
    }

    /// <summary>
    /// 현재 줄을 완료 상태로 마킹하고 이벤트를 발생시킵니다.
    /// </summary>
    private void MarkLineComplete()
    {
        IsLineComplete = true;
        _typingCoroutine = null;
        OnLineCompleted?.Invoke();
    }

    /// <summary>
    /// 대화를 정상 종료합니다.
    /// </summary>
    private void EndDialogue()
    {
        IsPlaying = false;
        IsLineComplete = false;

        // 콜백 발생: 코드 기반 Action + SO의 UnityEvent
        _currentDialogue.OnDialogueEnd?.Invoke();
        OnDialogueEnded?.Invoke();

        _currentDialogue = null;
        _currentLineIndex = 0;
        _fullText = null;
    }

    /// <summary>
    /// 진행 중인 대화를 콜백 없이 즉시 중단합니다 (내부용).
    /// </summary>
    private void StopDialogueImmediate()
    {
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }

        IsPlaying = false;
        IsLineComplete = false;
        _currentDialogue = null;
        _currentLineIndex = 0;
        _fullText = null;
    }

    private void OnDisable()
    {
        // 컴포넌트 비활성화 시 진행 중인 코루틴 정리
        if (_typingCoroutine != null)
        {
            StopCoroutine(_typingCoroutine);
            _typingCoroutine = null;
        }
    }
}
