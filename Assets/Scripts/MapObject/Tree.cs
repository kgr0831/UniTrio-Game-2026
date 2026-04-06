using System;
using System.Collections;
using UnityEngine;

public class Tree : BaseMapObject
{
    [Header("Tree Specifics")]
    public GameObject woodLogPrefab; // 나무가 파괴될 때 나올 아이템

    // 나무가 데미지를 입을 때 흔들리는 연출 등을 추가할 수 있음
    public override void TakeDamage(float damage)
    {
        // 부모의 데미지 로직 실행 (체력 감소 및 비활성화 체크)
        base.TakeDamage(damage);

        if (currentHealth > 0)
        {
            Debug.Log($"{data.objectName}: 나무가 흔들립니다!");
        }
        else
        {
            SpawnLogs();
        }
    }

    private void SpawnLogs()
    {
        Debug.Log("나무가 쓰러지며 통나무를 드랍합니다.");
        // Instantiate(woodLogPrefab, transform.position, Quaternion.identity);
    }

    public override IEnumerator Gimic()
    {
        yield return base.Gimic();
    }

    // 필요하다면 감지되었을 때 나무의 색상을 밝게 바꾸는 등의 오버라이드 가능
    public override void OnDetected(bool isDetected)
    {
        base.OnDetected(isDetected);
        
    }

    public void Update()
    {
        if (Input.GetKey(KeyCode.E)) StartCoroutine(Gimic());
    }
}