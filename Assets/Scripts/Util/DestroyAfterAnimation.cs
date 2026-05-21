using UnityEngine;

/// <summary>
/// Animator 애니메이션이 1회 재생 완료된 후 자동으로 GameObject를 파괴합니다.
/// VFX처럼 한 번만 재생하고 사라져야 하는 오브젝트에 부착합니다.
/// </summary>
public class DestroyAfterAnimation : MonoBehaviour
{
    private Animator _animator;
    private bool _hasStarted = false;

    private void Awake()
    {
        _animator = GetComponent<Animator>();
    }

    private void Update()
    {
        if (_animator == null)
        {
            Destroy(gameObject);
            return;
        }

        AnimatorStateInfo stateInfo = _animator.GetCurrentAnimatorStateInfo(0);

        // 애니메이션이 시작되었는지 확인
        if (!_hasStarted)
        {
            if (stateInfo.normalizedTime > 0f)
                _hasStarted = true;
            return;
        }

        // 1회 재생 완료 (normalizedTime >= 1.0)
        if (stateInfo.normalizedTime >= 1.0f)
        {
            Destroy(gameObject);
        }
    }
}
