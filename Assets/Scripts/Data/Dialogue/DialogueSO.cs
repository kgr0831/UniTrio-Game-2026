using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 플레이어 선택지 데이터. NextDialogueId가 비어 있으면 대화 종료.
/// </summary>
[System.Serializable]
public class DialogueChoice
{
    [Tooltip("선택지 버튼에 표시할 텍스트")]
    public string Text;

    [Tooltip("이 선택지를 고르면 이어서 재생할 대화 ID (비우면 대화 종료)")]
    public string NextDialogueId;
}

/// <summary>
/// 개별 대화 문장 데이터.
/// Choices 배열이 비어있으면 일반 진행, 값이 있으면 선택지 UI를 표시합니다.
/// </summary>
[System.Serializable]
public struct DialogueLine
{
    [Tooltip("이 문장을 말하는 인물")]
    public CharacterSO Speaker;

    [TextArea(2, 5)]
    [Tooltip("대화 텍스트 내용")]
    public string Text;

    [Tooltip("선택지 목록 (비어있으면 일반 진행)")]
    public DialogueChoice[] Choices;
}

/// <summary>
/// 대화 이벤트 데이터 컨테이너 (ScriptableObject).
/// </summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Data/Dialogue/Dialogue")]
public class DialogueSO : ScriptableObject
{
    [Header("Participants")]
    [Tooltip("이 대화에 참여하는 인물들 (인덱스 0=좌측, 1=우측 초상화)")]
    public CharacterSO[] Participants;

    [Header("Dialogue Lines")]
    [Tooltip("대화 문장 리스트 (순서대로 출력됨)")]
    public DialogueLine[] Lines;

    [Header("Events")]
    [Tooltip("대화 시작 시 호출되는 이벤트")]
    public UnityEvent OnDialogueStart;

    [Tooltip("대화 종료 시 호출되는 이벤트")]
    public UnityEvent OnDialogueEnd;

    public int LineCount => Lines != null ? Lines.Length : 0;
}
