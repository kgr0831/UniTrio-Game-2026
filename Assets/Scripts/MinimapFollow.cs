using UnityEngine;

public class MinimapFollow : MonoBehaviour
{
    public Transform target;
    // 카메라가 캐릭터로부터 떨어져 있을 거리 (Z축)
    public float offsetZ = -10f; 

    void LateUpdate()
    {
        if (target == null) return;

        // 플레이어의 X, Y 좌표를 그대로 가져옵니다.
        Vector3 newPosition = new Vector3(target.position.x, target.position.y, offsetZ);

        // 카메라 위치 업데이트
        transform.position = newPosition;
    }
}