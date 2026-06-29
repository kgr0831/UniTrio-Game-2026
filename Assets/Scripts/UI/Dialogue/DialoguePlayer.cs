using System;
using System.Collections;
using UnityEngine;

/// <summary>
/// 대화 진행 핵심 로직 (SRP: 대화 상태·타자기 효과·선택지 대기만 담당).
/// </summary>
public class DialoguePlayer : MonoBehaviour, IDialoguePlayer
{
    [Header("Typing Settings")]
    [SerializeField] private float _typingSpeed = 0.05f;

    public event Action OnDialogueStarted;
    public event Action OnDialogueEnded;
    public event Action<CharacterSO, string> OnLineChanged;
    public event Action<string> OnTextUpdated;
    public event Action OnLineCompleted;
    public event Action<DialogueChoice[]> OnChoicesRequired;

    public bool IsPlaying { get; private set; }
    public bool IsLineComplete { get; private set; }
    public bool IsWaitingForChoice { get; private set; }
    public bool IsAdvanceLocked { get; private set; }
    public int CurrentLineIndex => _currentLineIndex;
    public DialogueSO CurrentDialogue => _currentDialogue;

    private DialogueSO _currentDialogue;
    private int _currentLineIndex;
    private string _fullText;
    private Coroutine _typingCoroutine;

    public void StartDialogue(DialogueSO dialogue)
    {
        if (dialogue == null || dialogue.LineCount == 0)
        {
            Debug.LogWarning("[DialoguePlayer] 대화 데이터가 비어있거나 null입니다.");
            return;
        }

        if (IsPlaying) StopDialogueImmediate();

        _currentDialogue = dialogue;
        _currentLineIndex = 0;
        IsPlaying = true;
        IsWaitingForChoice = false;

        OnDialogueStarted?.Invoke();
        _currentDialogue.OnDialogueStart?.Invoke();

        DisplayCurrentLine();
    }

    public void Advance()
    {
        if (!IsPlaying || IsWaitingForChoice || IsAdvanceLocked) return;

        if (!IsLineComplete)
            CompleteLine();
        else
            AdvanceLine();
    }

    public void LockAdvance()   => IsAdvanceLocked = true;
    public void UnlockAdvance() => IsAdvanceLocked = false;

    public void SelectChoice(DialogueSO nextDialogue)
    {
        if (!IsWaitingForChoice) return;
        IsWaitingForChoice = false;

        if (nextDialogue != null)
            StartDialogue(nextDialogue);
        else
            EndDialogue();
    }

    private void AdvanceLine()
    {
        _currentLineIndex++;
        if (_currentLineIndex < _currentDialogue.LineCount)
            DisplayCurrentLine();
        else
            EndDialogue();
    }

    private void DisplayCurrentLine()
    {
        DialogueLine line = _currentDialogue.Lines[_currentLineIndex];
        _fullText = line.Text;
        IsLineComplete = false;

        OnLineChanged?.Invoke(line.Speaker, _fullText);

        if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
        _typingCoroutine = StartCoroutine(TypeTextCoroutine());
    }

    private IEnumerator TypeTextCoroutine()
    {
        int charIndex = 0;
        while (charIndex < _fullText.Length)
        {
            charIndex++;
            OnTextUpdated?.Invoke(_fullText.Substring(0, charIndex));
            yield return new WaitForSecondsRealtime(_typingSpeed);
        }
        MarkLineComplete();
    }

    private void CompleteLine()
    {
        if (_typingCoroutine != null) { StopCoroutine(_typingCoroutine); _typingCoroutine = null; }
        OnTextUpdated?.Invoke(_fullText);
        MarkLineComplete();
    }

    private void MarkLineComplete()
    {
        IsLineComplete = true;
        _typingCoroutine = null;

        DialogueLine line = _currentDialogue.Lines[_currentLineIndex];
        if (line.Choices != null && line.Choices.Length > 0)
        {
            IsWaitingForChoice = true;
            OnChoicesRequired?.Invoke(line.Choices);
        }
        else
        {
            OnLineCompleted?.Invoke();
        }
    }

    private void EndDialogue()
    {
        IsPlaying = false;
        IsLineComplete = false;
        IsWaitingForChoice = false;
        IsAdvanceLocked = false;

        _currentDialogue.OnDialogueEnd?.Invoke();
        OnDialogueEnded?.Invoke();

        _currentDialogue = null;
        _currentLineIndex = 0;
        _fullText = null;
    }

    private void StopDialogueImmediate()
    {
        if (_typingCoroutine != null) { StopCoroutine(_typingCoroutine); _typingCoroutine = null; }
        IsPlaying = false;
        IsLineComplete = false;
        IsWaitingForChoice = false;
        _currentDialogue = null;
        _currentLineIndex = 0;
        _fullText = null;
    }

    private void OnDisable()
    {
        if (_typingCoroutine != null) { StopCoroutine(_typingCoroutine); _typingCoroutine = null; }
    }
}
