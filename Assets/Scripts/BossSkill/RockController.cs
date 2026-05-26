using UnityEngine;

public class RockController : MonoBehaviour
{
    public float speed = 20.0f;
    public float lifetime = 5f;
    public Quaternion moveRotation;
    public GameObject impactEffectPrefab;

    private float _elapsed;

    void Update()
    {
        Vector3 direction = moveRotation * Vector3.up;
        direction.z = 0;
        transform.position += direction.normalized * speed * Time.deltaTime;

        _elapsed += Time.deltaTime;
        if (_elapsed >= lifetime)
            DestroyWithEffect();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player"))
        {
            DestroyWithEffect();
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        if (!collision.collider.CompareTag("Boss"))
        {
            DestroyWithEffect();
        }
    }

    private void DestroyWithEffect()
    {
        CameraShakeController.Instance?.Shake(0.15f, 0.25f);

        if (impactEffectPrefab != null)
        {
            GameObject fx = Object.Instantiate(impactEffectPrefab, transform.position, Quaternion.identity);
            Object.Destroy(fx, 2f);
        }

        Destroy(gameObject);
    }
}
