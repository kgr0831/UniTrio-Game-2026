using UnityEngine;

/// <summary>
/// 대화 시스템에서 사용되는 인물(캐릭터) 데이터 컨테이너.
/// 인물의 이름과 일러스트 스프라이트를 저장합니다.
/// CreateAssetMenu를 통해 에디터에서 손쉽게 에셋을 생성할 수 있습니다.
/// </summary>
[CreateAssetMenu(fileName = "NewCharacter", menuName = "Data/Dialogue/Character")]
public class CharacterSO : ScriptableObject
{
    [Header("Character Info")]
    [Tooltip("인물 이름 (대화창에 표시)")]
    public string CharacterName;

    [Tooltip("인물 일러스트 이미지 (대화창 왼쪽에 표시)")]
    public Sprite Portrait;
}
