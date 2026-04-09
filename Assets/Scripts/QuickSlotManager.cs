using UnityEngine;

public class QuickSlotManager : MonoBehaviour
{
    // 인스펙터에서 하이어라키에 있는 8개 슬롯을 순서대로 드래그해서 넣으세요.
    public InventorySlot[] quickSlots; 

    private void Start()
    {
        ClearAllSlots();
    }

    public void ClearAllSlots()
    {
        if (quickSlots == null) return;
        foreach (var slot in quickSlots)
        {
            if (slot != null) slot.RefreshSlot(null, 0);
        }
        Debug.Log("[QuickSlot] 모든 슬롯 초기화 완료");
    }

    private void UseItem(InventorySlot slot)

    {
        ItemData data = slot.currentData;

        // 1. 타입별 사용 로직 처리
        if (data is ConsumableData consumable)
        {
            Debug.Log($"{consumable.Name} 사용 (회복량: {consumable.HealAmount})");
            // 소모품은 사용 후 수량 차감
            ConsumeItem(slot);
        }
        else if (data is WeaponData weapon)
        {
            Debug.Log($"{weapon.Name} 장착 (공격력: {weapon.AttackBonus})");
            // 장비는 보통 소모되지 않으므로 수량 차감 없음
        }

        else if (data is SkillData skill)
        {
            Debug.Log($"{skill.Name} 스킬 발동!");
            // 스킬은 개수 개념이 없으므로 로직만 실행
        }
        else
        {
            Debug.Log($"{data?.Name}은(는) 사용할 수 없는 아이템입니다.");
        }
    }

    // 소모품 전용 차감 로직
    private void ConsumeItem(InventorySlot slot)
    {
        int newCount = slot.currentCount - 1;

        if (newCount <= 0)
        {
            // 수량이 다 떨어지면 슬롯 비우기
            slot.RefreshSlot(null, 0);
        }
        else
        {
            // 수량 갱신 및 UI 반영
            slot.RefreshSlot(slot.currentData, newCount);
        }
    }

    void Update()
    {
        // 1~8 키 입력 체크
        for (int i = 0; i < quickSlots.Length; i++)
        {
            // i가 0이면 Alpha1, 1이면 Alpha2... 순서대로 대응
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                if (quickSlots[i] != null && quickSlots[i].currentData != null)
                {
                    UseItem(quickSlots[i]);
                }
            }
        }
    }
}