using System.Collections;
using UnityEngine;

/// <summary>
/// 에너미의 체력과 하얀색 점멸 쉐이더(피격 효과)를 전담하는 컴포넌트.
/// Enemy 태그를 가진 오브젝트에 부착하고 SpriteFlash 쉐이더용 Material을 설정해주세요.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class Enemy : MonoBehaviour
{
    [Header("Status")]
    [SerializeField] private int _maxHealth = 100;
    private int _currentHealth;

    [Header("Hit Flash Effect (Shader)")]
    [SerializeField] private float _flashDuration = 0.15f; // 점멸 유지 시간
    
    private SpriteRenderer _spriteRenderer;
    private Coroutine _flashCoroutine;

    // 쉐이더 파라미터 해시 캐싱 (성능 향상)
    private static readonly int HashFlashAmount = Shader.PropertyToID("_FlashAmount");
    private MaterialPropertyBlock _mpb;

    private void Awake()
    {
        _currentHealth = _maxHealth;
        _spriteRenderer = GetComponent<SpriteRenderer>();
        
        // PropertyBlock 초기화 (머티리얼 개별 분할 없이 단일 객체 속성만 값 덮어쓰기)
        _mpb = new MaterialPropertyBlock();
    }

    /// <summary>
    /// 외부 히트박스(SwordHitbox 등)에서 타격 시 호출되는 함수
    /// </summary>
    public void TakeDamage(int damageAmount)
    {
        _currentHealth -= damageAmount;
        
        // 점멸 중첩 방지를 위해 기존 코루틴 취소 후 새로 재생
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
        }
        _flashCoroutine = StartCoroutine(FlashRoutine());

        if (_currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// 쉐이더의 _FlashAmount 값을 1로 올렸다가 원래대로(0) 내리는 점멸 효과 진행
    /// </summary>
    private IEnumerator FlashRoutine()
    {
        // PropertyBlock 설정 시 SpriteRenderer 안의 머티리얼을 교체/복제하지 않고 변수만 주입합니다.
        // 1. 점멸 켜기
        _spriteRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(HashFlashAmount, 1f);
        _spriteRenderer.SetPropertyBlock(_mpb);

        // 지정된 시간만큼 하얗게 유지
        yield return new WaitForSeconds(_flashDuration);

        // 2. 점멸 끄기
        _spriteRenderer.GetPropertyBlock(_mpb);
        _mpb.SetFloat(HashFlashAmount, 0f);
        _spriteRenderer.SetPropertyBlock(_mpb);
    }

    private void Die()
    {
        // TODO: 에너미 사망 처리 로직
        Debug.Log($"[{gameObject.name}] 적 처치됨!");
        Destroy(gameObject); // 일단은 파괴
    }
}
