using UnityEngine;

public class QuickSlotManager : MonoBehaviour
{
    // 인스펙터에서 하이어라키에 있는 8개 슬롯을 순서대로 드래그해서 넣으세요.
    public InventorySlot[] quickSlots; 

    void Update()
    {
        // 1~8 키 입력 시 해당 인덱스의 슬롯 사용
        for (int i = 0; i < quickSlots.Length; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                if (quickSlots[i].currentData != null)
                {
                    quickSlots[i].currentData.Use();
                }
            }
        }
    }
}
