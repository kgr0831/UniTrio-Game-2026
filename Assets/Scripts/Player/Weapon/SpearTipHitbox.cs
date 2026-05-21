using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 창 끝(Tip) 전용 크리티컬 타격 컴포넌트.
/// 0.5×0.5 크기의 BoxCollider2D 트리거를 사용하여,
/// 창 끝 사거리에 적을 맞출 경우 1.5배 데미지 + hit-a_0 VFX를 생성합니다.
/// 
/// 일반 히트박스(SwordHitbox)보다 먼저 처리되며,
/// tipHit으로 맞은 적은 SwordHitbox의 _hitThisSwing에 등록하여 중복 타격을 방지합니다.
/// </summary>
public class SpearTipHitbox : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private PlayerEntity _playerEntity;
    [SerializeField] private float _baseDamage = 0f;

    [Header("Critical Multiplier")]
    [Tooltip("창 끝 타격 시 데미지 배율")]
    [SerializeField] private float _tipDamageMultiplier = 1.5f;

    [Header("Target Tags")]
    [SerializeField] private string[] _targetTags = new string[] { "Enemy", "Tree" };

    [Header("Damage Text")]
    [SerializeField] private GameObject _damageTextPrefab;

    // 참조 (SpearBehaviour에서 주입)
    private WeaponBehaviourBase _weaponBehaviour;
    private SwordHitbox _mainHitbox; // 일반 히트박스와 중복 방지용
    private HashSet<int> _tipHitThisSwing = new HashSet<int>();

    // hit-a_0 VFX 프리팹 (Resources에서 로드)
    private RuntimeAnimatorController _hitVfxController;
    private Sprite[] _hitVfxSprites;

    private void Awake()
    {
        _weaponBehaviour = GetComponentInParent<WeaponBehaviourBase>();

        // Resources에서 hit-a_0 AnimatorController 로드
        _hitVfxController = Resources.Load<RuntimeAnimatorController>("Spritessheets/hit-a_0");
        // hit-a 스프라이트 로드 (첫 번째 프레임용)
        _hitVfxSprites = Resources.LoadAll<Sprite>("Spritessheets/hit-a");
    }

    /// <summary>
    /// SpearBehaviour에서 호출하여 참조를 주입합니다.
    /// </summary>
    public void Initialize(PlayerEntity playerEntity, SwordHitbox mainHitbox, GameObject damageTextPrefab)
    {
        _playerEntity = playerEntity;
        _mainHitbox = mainHitbox;
        _damageTextPrefab = damageTextPrefab;
    }

    public void ResetSwingHits()
    {
        _tipHitThisSwing.Clear();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 태그 검증
        bool isValidTag = false;
        if (_targetTags != null)
        {
            foreach (var t in _targetTags)
            {
                if (other.CompareTag(t))
                {
                    isValidTag = true;
                    break;
                }
            }
        }
        if (!isValidTag) return;

        IDamageable target = other.GetComponentInParent<IDamageable>();
        if (target == null || !target.IsAlive) return;

        Component targetComp = target as Component;
        if (targetComp == null) return;
        int targetId = targetComp.gameObject.GetInstanceID();

        // 이미 이번 스윙에서 팁으로 맞은 적이면 무시
        if (!_tipHitThisSwing.Add(targetId)) return;

        // 일반 히트박스가 이미 이 적을 처리했으면 팁 데미지 스킵 (왼쪽 방향 중복 방지)
        if (_mainHitbox != null && _mainHitbox.ContainsHit(targetId)) return;

        // 일반 히트박스의 hitThisSwing에도 등록 → 중복 데미지 방지
        if (_mainHitbox != null)
        {
            _mainHitbox.RegisterExternalHit(targetId);
        }

        Vector3 targetPosition = other.transform.position;
        Vector3 hitPoint = other.ClosestPoint(transform.position);

        // 데미지 계산: 기본 데미지 * 1.5배
        float atk = _playerEntity != null ? _playerEntity.TotalAtk : 0f;
        float damage = DamageCalculator.CalcOutgoingDamage(atk, _baseDamage);

        // 강타(Bash) 배율 적용
        if (_weaponBehaviour != null)
        {
            float bashMult = _weaponBehaviour.CurrentSwingBashMultiplier;
            damage *= bashMult;
        }

        // 크리티컬 배율 적용
        damage *= _tipDamageMultiplier;

        target.TakeDamage(damage, gameObject);

        // 카메라 흔들림 + 히트스톱 (일반보다 강하게)
        if (CameraShakeController.Instance != null)
            CameraShakeController.Instance.Shake(0.12f, 0.15f);
        if (HitStopManager.Instance != null)
            HitStopManager.Instance.TriggerHitStop(0.06f);

        // 히트 스턴 적용
        float stunDuration = 0.25f;
        HitStaggerHandler stagger = other.GetComponentInParent<HitStaggerHandler>();
        if (stagger != null)
            stagger.ApplyStagger(stunDuration);

        HealthSystem hp = other.GetComponentInParent<HealthSystem>();
        if (hp != null)
            hp.OverrideFlashTimer(stunDuration);

        // hit-a_0 VFX를 타격 위치에 생성
        SpawnTipHitVFX(hitPoint);

        // 데미지 텍스트 표시
        SpawnDamageText(other, damage);

        // 속성 게이지 연동 (이벤트 발송)
        Transform playerRoot = _playerEntity != null ? _playerEntity.transform : transform.root;
        bool isFirstHit = _tipHitThisSwing.Count <= 1;
        HitEventManager.NotifyHit(playerRoot.position, targetPosition, isFirstHit);
    }

    private void SpawnTipHitVFX(Vector3 hitPoint)
    {
        // hit-a_0 애니메이션 VFX 오브젝트 생성
        GameObject vfxObj = new GameObject("SpearTipHitVFX");
        vfxObj.transform.position = hitPoint;
        vfxObj.transform.localScale = Vector3.one * 10f; // 스케일 10배

        SpriteRenderer sr = vfxObj.AddComponent<SpriteRenderer>();
        sr.sortingLayerName = "Weapons";
        sr.sortingOrder = 30;

        // 첫 번째 스프라이트 설정
        if (_hitVfxSprites != null && _hitVfxSprites.Length > 0)
            sr.sprite = _hitVfxSprites[0];

        // Animator 추가 및 컨트롤러 할당
        if (_hitVfxController != null)
        {
            Animator animator = vfxObj.AddComponent<Animator>();
            animator.runtimeAnimatorController = _hitVfxController;
            // 1회만 재생 후 자동 파괴
            vfxObj.AddComponent<DestroyAfterAnimation>();
        }
        else
        {
            // 컨트롤러 없으면 바로 파괴
            Object.Destroy(vfxObj, 0.5f);
        }
    }

    private void SpawnDamageText(Collider2D enemyCollider, float damageAmount)
    {
        if (_damageTextPrefab == null) return;

        Vector3 spawnPos = enemyCollider.bounds.center + Vector3.up * 0.5f;

        GameObject textObj = SimpleObjectPool.Instance.Get(_damageTextPrefab, spawnPos, Quaternion.identity);
        DamageText dmgText = textObj.GetComponent<DamageText>();
        if (dmgText != null) dmgText.Setup(Mathf.RoundToInt(damageAmount));
    }

}
