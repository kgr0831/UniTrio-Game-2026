using UnityEngine;

public interface IUseable {
    int ID { get; }      // 서버 저장용 고유 번호
    string Name { get; } 
    Sprite Icon { get; } 
    void Use();          // 아이템은 개수 차감, 스킬은 마나 소모 등
}
