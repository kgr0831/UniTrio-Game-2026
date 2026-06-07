using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 개별 대화 문장 데이터.
/// 각 문장마다 화자(CharacterSO)와 텍스트 내용을 지정합니다.
/// </summary>
[System.Serializable]
public struct DialogueLine
{
    [Tooltip("이 문장을 말하는 인물")]
    public CharacterSO Speaker;

    [TextArea(2, 5)]
    [Tooltip("대화 텍스트 내용")]
    public string Text;
}

/// <summary>
/// 대화 이벤트 데이터 컨테이너 (ScriptableObject).
/// 대화에 참여하는 인물 목록, 문장 리스트, 시작/종료 이벤트를 관리합니다.
/// </summary>
[CreateAssetMenu(fileName = "NewDialogue", menuName = "Data/Dialogue/Dialogue")]
public class DialogueSO : ScriptableObject
{
    [Header("Participants")]
    [Tooltip("이 대화에 참여하는 인물들")]
    public CharacterSO[] Participants;

    [Header("Dialogue Lines")]
    [Tooltip("대화 문장 리스트 (순서대로 출력됨)")]
    public DialogueLine[] Lines;

    [Header("Events")]
    [Tooltip("대화 시작 시 호출되는 이벤트")]
    public UnityEvent OnDialogueStart;

    [Tooltip("대화 종료 시 호출되는 이벤트")]
    public UnityEvent OnDialogueEnd;

    /// <summary>
    /// 총 문장 개수 (읽기 전용 프로퍼티).
    /// 에디터 표시 및 런타임 범위 체크에 활용됩니다.
    /// </summary>
    public int LineCount => Lines != null ? Lines.Length : 0;
}
