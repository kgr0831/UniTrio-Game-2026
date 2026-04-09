using UnityEngine;

/// <summary>
/// I키로 인벤토리 패널을 열고 닫습니다.
/// 항상 켜져 있는 부모(Canvas 등)에 부착하고
/// _inventoryPanel에 InventoryPanel을 연결하세요.
/// </summary>
public class InventoryToggle : MonoBehaviour
{
    [SerializeField] private KeyCode   _toggleKey      = KeyCode.I;
    [SerializeField] private GameObject _inventoryPanel;

    private void Update()
    {
        if (Input.GetKeyDown(_toggleKey) && _inventoryPanel != null)
        {
            bool next = !_inventoryPanel.activeSelf;
            _inventoryPanel.SetActive(next);
        }
    }
}
