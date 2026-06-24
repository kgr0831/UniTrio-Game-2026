using System;

namespace Core
{
    /// <summary>
    /// 아이템 관련 전역 이벤트를 관리하는 정적 클래스 (DIP 준수).
    /// Producers(아이템 수집기 등)와 Consumers(인벤토리 매니저 등) 간의 결합도를 낮춥니다.
    /// </summary>
    public static class ItemEvents
    {
        /// <summary>
        /// 아이템이 수집되었을 때 발생합니다.
        /// (ItemData data, int count)
        /// </summary>
        public static Action<ItemData, int> OnItemCollected;

        public static void TriggerItemCollected(ItemData data, int count)
        {
            OnItemCollected?.Invoke(data, count);
        }

        /// <summary>
        /// 인벤토리 내용(추가, 소모 등)이 변경되었을 때 발생합니다.
        /// 이를 구독하여 레시피 UI 등에서 수량을 실시간 갱신할 수 있습니다.
        /// </summary>
        public static Action OnInventoryChanged;

        public static void TriggerInventoryChanged()
        {
            OnInventoryChanged?.Invoke();
        }
    }
}
