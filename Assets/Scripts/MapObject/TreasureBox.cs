using UnityEngine;

/// <summary>
/// 보물 상자 스크립트.
/// 플레이어가 F키로 상호작용 시 특정 아이템(예: 던전 열쇠)을 획득합니다.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TreasureBox : MonoBehaviour, IInteractable
{
    [Header("보상 설정")]
    [SerializeField] private ItemData _rewardItem;
    [SerializeField] private int _rewardCount = 1;

    [Header("설정")]
    [SerializeField] private string _prompt = "F키: 보물 상자 열기";
    [Tooltip("한 번만 열 수 있는지 여부")]
    [SerializeField] private bool _oneTimeUse = true;

    private bool _isOpened = false;

    public string InteractionPrompt => _isOpened ? "비어 있음" : _prompt;

    public bool CanInteract(GameObject player)
    {
        return !_isOpened;
    }

    public void Interact(GameObject player)
    {
        if (_isOpened) return;

        Debug.Log("[TreasureBox] 보물 상자를 열었습니다.");

        if (InventoryManager.Instance != null && _rewardItem != null)
        {
            InventoryManager.Instance.AddItem(_rewardItem, _rewardCount);
            if (NotificationUI.Instance != null)
            {
                NotificationUI.Instance.ShowMessage($"{_rewardItem.Name} 획득!");
            }
        }

        if (_oneTimeUse)
        {
            _isOpened = true;
            // 애니메이션 재생 (옵션)
            var anim = GetComponent<Animator>();
            if (anim != null) anim.SetTrigger("Open");
            
            // 콜라이더를 끄거나 비주얼 변경 가능
        }
    }
}
