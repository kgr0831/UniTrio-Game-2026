using UnityEngine;
using System.Collections.Generic;

public class InventoryGenerator : MonoBehaviour
{
    [Header("Settings")]
    public GameObject slotPrefab;   // InventorySlot 프리팹
    public Transform container;     // Grid Layout Group이 붙은 부모(InventoryPanel)
    
    [Range(1, 10)] public int columns = 5; // 가로 개수
    [Range(1, 10)] public int rows = 4;    // 세로 개수
    
    [Header("Main Inventory Guard")]
    public bool isMainInventory = false; // 메인 인벤토리인 경우에만 켜서 시작 템을 지급하고 매니저의 메인 인덱스로 사용

    // 슬롯들을 관리하기 위한 리스트
    private List<InventorySlot> allSlots = new List<InventorySlot>();

    public IReadOnlyList<InventorySlot> AllSlots => allSlots;

    [Header("Starter Items")]
    [Tooltip("게임 시작 시 지급할 기본 무기들")]
    public List<WeaponData> starterWeapons;

    [Tooltip("게임 시작 시 지급할 기본 아이템들 (소모품 등)")]
    public List<ItemData> starterItems;


    private void Awake()
    {
        GenerateEmptySlots();
    }

    private void OnEnable()
    {
        // 씬 로드 및 오브젝트 활성화 시점에 매니저에 안전하게 등록 및 미러 동기화 요청
        if (InventoryManager.Instance != null)
        {
            InventoryManager.Instance.RegisterGenerator(this);
            if (!isMainInventory)
            {
                InventoryManager.Instance.RequestSyncFromMain(this);
            }
        }
    }

    [ContextMenu("Generate Inventory")] // 인스펙터 우클릭 메뉴로 테스트 가능
    public void GenerateEmptySlots()
    {
        // 사용자가 씬에서 강제로 슬롯을 삭제했을 경우를 대비해 Missing(null) 상태의 리스트 정리
        allSlots.RemoveAll(slot => slot == null);

        // 핫픽스: 게임 실행(Play) 중일 때만 중복 생성 방지 적용 
        // (에디터 뷰에서는 언제든 재시도할 수 있도록 허용)
        if (Application.isPlaying && allSlots.Count > 0) return;

        // 기존에 생성된 슬롯이 있다면 제거 (에디터와 런타임 호환을 위해 역순 삭제 및 DestroyImmediate 분기)
        for (int i = container.childCount - 1; i >= 0; i--)
        {
            if (Application.isPlaying)
                Destroy(container.GetChild(i).gameObject);
            else
                DestroyImmediate(container.GetChild(i).gameObject);
        }
        allSlots.Clear();

        // 전체 개수 (가로 * 세로) 만큼 생성
        int totalSlots = columns * rows;

        for (int i = 0; i < totalSlots; i++)
        {
            GameObject go = Instantiate(slotPrefab, container);
            go.name = $"Slot_{i}"; // 디버깅용 이름 설정

            InventorySlot slot = go.GetComponent<InventorySlot>();
            slot.slotIndex = i; // 동기화용 인덱스 할당
            
            // 초기 상태는 데이터가 없는 빈 슬롯으로 설정
            slot.RefreshSlot(null, 0);
            
            allSlots.Add(slot);
        }

        Debug.Log($"{columns}x{rows} 인벤토리 생성 완료!");

        if (!isMainInventory) return; // 메인 인벤토리가 아닌 경우 시작 아이템 지급을 생략합니다.

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