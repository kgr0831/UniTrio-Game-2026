using UnityEngine;

/// <summary>
/// 대화 시스템 테스트용 트리거 스크립트.
/// 인스펙터에서 DialogueSO를 할당하고 L키를 누르면 대화를 실행합니다.
/// </summary>
public class DialogueTrigger : MonoBehaviour
{
    [Header("Test Settings")]
    [Tooltip("테스트할 대화 데이터 (인스펙터에서 SO 할당)")]
    [SerializeField] private DialogueSO _testDialogue;

    [Tooltip("대화 진행을 담당할 DialoguePlayer 컴포넌트")]
    [SerializeField] private DialoguePlayer _dialoguePlayer;

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.L) && _testDialogue != null && _dialoguePlayer != null)
        {
            if (!_dialoguePlayer.IsPlaying)
            {
                Debug.Log($"[DialogueTrigger] 대화 시작: {_testDialogue.name} (문장 수: {_testDialogue.LineCount})");
                _dialoguePlayer.StartDialogue(_testDialogue);
            }
            else
            {
                Debug.Log("[DialogueTrigger] 대화가 이미 진행 중입니다.");
            }
        }
    }
}
