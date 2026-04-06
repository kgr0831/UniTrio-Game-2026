using UnityEngine;

public class QuickSlotManager : MonoBehaviour
{
    // 인스펙터에서 하이어라키에 있는 8개 슬롯을 순서대로 드래그해서 넣으세요.
    public InventorySlot[] quickSlots; 

    // 신버전 ItemData는 순수 데이터 클래스이므로 사용 로직은 여기서 타입별로 처리
    private void UseItem(InventorySlot slot)
    {
        ItemData data = slot.currentData;

        if (data is ConsumableData consumable)
        {
            Debug.Log($"{consumable.Name} 사용 (회복량: {consumable.HealAmount})");
            // TODO: 실제 플레이어 힐 연동 (HealingSystem, Milestone 3)
        }
        else if (data is WeaponData weapon)
        {
            Debug.Log($"{weapon.Name} 장착");
            // TODO: WeaponSlotManager 연동 (Milestone 3)
        }
        else
        {
            Debug.Log($"{data.Name} 사용");
        }

        // 수량 차감
        slot.currentCount--;
        if (slot.currentCount <= 0)
            slot.currentCount = 0;
    }

    void Update()
    {
        // 1~8 키 입력 시 해당 인덱스의 슬롯 사용
        for (int i = 0; i < quickSlots.Length; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                if (quickSlots[i].currentData != null)
                {
                    UseItem(quickSlots[i]);
                }
            }
        }
    }
}
