using System;
using System.Collections;
using UnityEngine;

public class Tree : BaseMapObject
{
    [Header("Tree Specifics")]
    public GameObject woodLogPrefab; // 나무가 파괴될 때 나올 아이템

    public void Start()
    {
        transform.localScale = new Vector3(3, 3, 1);
    }

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
        Debug.Log("코루틴실행");
        GameObject indicator = IndicatorManager.Instance.SpawnIndicator(data.indicatorSprite);
        indicator.transform.localScale = new Vector3(3, 3, 1);
        indicator.transform.position = transform.position;
        yield return new WaitForSeconds(2.0f);
        StartCoroutine(IndicatorManager.Instance.AttackBasedIndicator(indicator));
        yield return base.Gimic();
    }
    

    public void Update()
    {
        if (Input.GetKey(KeyCode.E)) StartCoroutine(Gimic());
    }
}