using UnityEngine;

public class BossImpactEffect : MonoBehaviour
{
    [SerializeField] private float lifetime = 2f;

    private void Start()
    {
        Destroy(gameObject, lifetime);
    }
}
