using UnityEngine;

public class RockController : MonoBehaviour
{

    public float speed = 20.0f;
    public Quaternion moveRotation;
    void Update()
    {
        // 1. 쿼터니언과 월드 기준 앞방향(Vector3.forward)을 곱해 회전된 방향 벡터를 구함
        Vector3 direction = moveRotation * Vector3.up;
        direction.z = 0;

        // 2. 구한 방향 벡터(월드 기준)로 오브젝트를 이동시킴
        transform.position += direction.normalized * speed * Time.deltaTime;
    }
}
