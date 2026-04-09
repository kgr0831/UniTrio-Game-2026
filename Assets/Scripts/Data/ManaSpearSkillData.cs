using UnityEngine;

/// <summary>
/// 마나 창(Mana Spear) 스킬 데이터.
/// 지팡이 착용 시 사용 가능하며, 마우스 방향으로 투사체를 발사합니다.
/// 데미지 공식: 마나 * 0.5 + 마공 * 0.5
/// </summary>
[CreateAssetMenu(fileName = "ManaSpearSkill", menuName = "Data/Skills/ManaSpear")]
public class ManaSpearSkillData : SkillData
{
    [Header("Mana Spear Settings")]
    [SerializeField] private GameObject _spearPrefab;
    [SerializeField] private float _speed = 15f;

    protected override void OnEnable()
    {
        base.OnEnable();
        RequiredWeapon = WeaponType.Staff;
    }

    public override void Execute(GameObject player)
    {
        var stats = player.GetComponent<StatSystem>();
        if (stats == null) return;

        // 데미지 계산: 마나 * 0.5 + 마공 * 0.5
        float damage = stats.MaxMana * 0.5f + stats.TotalMagicAtk * 0.5f;

        // 마우스 방향 계산
        Vector3 mouseWorld = GetMouseWorldPosition();
        Vector2 dir = (Vector2)(mouseWorld - player.transform.position);
        if (dir.sqrMagnitude < 0.001f) dir = Vector2.right;
        else dir.Normalize();

        // 투사체 발사 (WandBehaviour의 MuzzlePoint를 찾으면 좋겠지만, 
        // 여기서는 플레이어 위치에서 약간 오프셋을 주어 발사합니다.)
        Vector3 spawnPos = player.transform.position + (Vector3)dir * 0.5f;
        
        if (_spearPrefab != null)
        {
            GameObject proj = SimpleObjectPool.Instance.Get(_spearPrefab, spawnPos, Quaternion.identity);
            
            // MagicProjectile 또는 ArrowProjectile 컴포넌트를 기대함
            var mp = proj.GetComponent<MagicProjectile>();
            if (mp != null)
            {
                mp.SetStats(_speed, damage, dir);
            }
            else
            {
                var ap = proj.GetComponent<ArrowProjectile>();
                if (ap != null)
                {
                    // ArrowProjectile은 direction을 SetStats으로 받지 않고 
                    // rotation을 따라가므로 rotation 설정 필요
                    proj.transform.right = dir;
                    ap.SetStats(_speed, damage);
                }
            }
        }
    }

    private Vector3 GetMouseWorldPosition()
    {
        Camera cam = Camera.main;
        if (cam == null) return Vector3.zero;

        Vector3 screenPos = Input.mousePosition;
        screenPos.z = -cam.transform.position.z; // 평면 0 기준 거리
        return cam.ScreenToWorldPoint(screenPos);
    }
}
