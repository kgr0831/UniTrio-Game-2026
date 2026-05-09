using UnityEngine;

/// <summary>
/// 월드 오브젝트와 플레이어 간의 상호작용을 정의하는 범용 인터페이스.
/// Bonfire, NPC, 상자 등 향후 확장 가능한 상호작용 시스템의 기반. (OCP 준수)
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 상호작용 프롬프트에 표시할 텍스트 (예: "Press F")
    /// </summary>
    string InteractionPrompt { get; }

    /// <summary>
    /// 현재 상호작용이 가능한 상태인지 판별
    /// </summary>
    bool CanInteract(GameObject player);

    /// <summary>
    /// 실제 상호작용 실행
    /// </summary>
    void Interact(GameObject player);
}
