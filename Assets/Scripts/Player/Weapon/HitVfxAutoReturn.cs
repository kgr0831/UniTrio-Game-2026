using UnityEngine;

/// <summary>
/// HitVFX 프리팹에 부착하는 자동 풀 반환 컴포넌트.
/// OnEnable 시 Animator를 초기화하고 클립 길이 후 SimpleObjectPool로 자동 반환합니다.
/// </summary>
[RequireComponent(typeof(Animator))]
public class HitVfxAutoReturn : MonoBehaviour
{
    private Animator _animator;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void OnEnable()
    {
        // 매번 0프레임부터 재생되도록 Animator 초기화
        _animator.Rebind();
        _animator.Update(0f);

        float dur = _animator.GetCurrentAnimatorStateInfo(0).length;
        Invoke(nameof(ReturnToPool), dur > 0f ? dur : 0.5f);
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(ReturnToPool));
    }

    private void ReturnToPool()
    {
        SimpleObjectPool.Instance.Release(gameObject);
    }
}
