using UnityEngine;

public class GridIndicator : MonoBehaviour
{
    private BaseMapObject parentObject;

    void Awake()
    {
        // 부모 오브젝트에서 BaseMapObject 컴포넌트를 찾아 참조를 보관합니다.
        parentObject = GetComponentInParent<BaseMapObject>();
        
        if (parentObject == null)
        {
            Debug.LogWarning($"{gameObject.name}: 부모 오브젝트에서 BaseMapObject를 찾을 수 없습니다.");
        }
    }

    // 1. 플레이어가 인디케이터 영역에 들어왔을 때
    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player") && parentObject != null)
        {
            transform.parent.GetComponent<BaseMapObject>().OnDetected(true);
        }
    }
}