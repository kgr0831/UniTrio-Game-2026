using UnityEngine;
using System.Collections.Generic;

public class InventoryGenerator : MonoBehaviour
{
    [Header("Settings")]
    public GameObject slotPrefab;   // InventorySlot 프리팹
    public Transform container;     // Grid Layout Group이 붙은 부모(InventoryPanel)
    
    [Range(1, 10)] public int columns = 5; // 가로 개수
    [Range(1, 10)] public int rows = 4;    // 세로 개수

    // 슬롯들을 관리하기 위한 리스트
    private List<InventorySlot> allSlots = new List<InventorySlot>();

    [Header("Starter Items")]
    [Tooltip("게임 시작 시 지급할 기본 무기들")]
    public List<WeaponData> starterWeapons;

    [Tooltip("게임 시작 시 지급할 기본 아이템들 (소모품 등)")]
    public List<ItemData> starterItems;


    void Start()
    {
        GenerateEmptySlots();
    }

    [ContextMenu("Generate Inventory")] // 인스펙터 우클릭 메뉴로 테스트 가능
    public void GenerateEmptySlots()
    {
        // 기존에 생성된 슬롯이 있다면 제거
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }
        allSlots.Clear();

        // 전체 개수 (가로 * 세로) 만큼 생성
        int totalSlots = columns * rows;

        for (int i = 0; i < totalSlots; i++)
        {
            GameObject go = Instantiate(slotPrefab, container);
            go.name = $"Slot_{i}"; // 디버깅용 이름 설정

            InventorySlot slot = go.GetComponent<InventorySlot>();
            
            // 초기 상태는 데이터가 없는 빈 슬롯으로 설정
            slot.RefreshSlot(null, 0);
            
            allSlots.Add(slot);
        }

        Debug.Log($"{columns}x{rows} 인벤토리 생성 완료!");

        // 시작 무기 지급
        int slotIndex = 0;
        if (starterWeapons != null)
        {
            for (int i = 0; i < starterWeapons.Count; i++)
            {
                if (slotIndex < allSlots.Count && starterWeapons[i] != null)
                {
                    allSlots[slotIndex].RefreshSlot(starterWeapons[i], 1);
                    slotIndex++;
                }
            }
        }

        // 시작 아이템 지급 (소모품 등)
        if (starterItems != null)
        {
            for (int i = 0; i < starterItems.Count; i++)
            {
                if (slotIndex < allSlots.Count && starterItems[i] != null)
                {
                    allSlots[slotIndex].RefreshSlot(starterItems[i], 3);
                    slotIndex++;
                }
            }
        }
    }
}