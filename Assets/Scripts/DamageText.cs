using UnityEngine;
using TMPro; // 🌟 텍스트매쉬 프로(TMP) 전용 네임스페이스 추가!

/// <summary>
/// 적 타격 시 머리 위로 데미지가 떠오르고 사라지는 UI 처리 스크립트.
/// 프리팹 최상단에 부착하여 사용합니다. (TextMeshPro 기반)
/// </summary>
public class DamageText : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float _lifetime = 1f; // 떠있는 시간
    [SerializeField] private float _floatSpeed = 2f; // 위로 올라가는 속도
    [SerializeField] private Vector3 _randomScatter = new Vector3(0.5f, 0.5f, 0f); // 무작위 분산
    
    // 기존 TextMesh 대신 압도적인 화질의 TMP를 사용합니다!
    private TextMeshPro _textMesh;
    private Color _originalColor;

    public void Setup(int damage)
    {
        _textMesh = GetComponent<TextMeshPro>();
        
        if (_textMesh != null)
        {
            // TMP는 SortingOrder 설정이 컴포넌트 내부에 직관적으로 들어있습니다.
            _textMesh.sortingLayerID = SortingLayer.NameToID("Default");
            _textMesh.sortingOrder = 9999; // Z-가림 완벽 방지
        }

        // 생성 시 약간 무작위로 위치 비틀어주기
        transform.position += new Vector3(
            Random.Range(-_randomScatter.x, _randomScatter.x),
            Random.Range(-_randomScatter.y, _randomScatter.y),
            -2f // Z축을 앞으로 당겨 충돌 방지
        );

        if (_textMesh != null)
        {
            _textMesh.text = damage.ToString();
            _originalColor = _textMesh.color;
        }

        Destroy(gameObject, _lifetime);
    }

    private void Update()
    {
        transform.position += Vector3.up * (_floatSpeed * Time.deltaTime);

        // 부드러운 투명도 알파 페이드 아웃 연출
        if (_textMesh != null)
        {
            _originalColor.a -= Time.deltaTime / _lifetime;
            _textMesh.color = _originalColor;
        }
    }
}
