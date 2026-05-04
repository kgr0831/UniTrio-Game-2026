using UnityEngine;
using System.Collections;
using UnityEditor.Experimental.GraphView; // 이 부분이 반드시 필요합니다!

public class IndicatorManager : MonoBehaviour
{
    public static IndicatorManager Instance { get; private set; }
    
    private void Awake()
    {
        Instance = this;
    }
    
    public GameObject SpawnIndicator(Sprite sprite)
    {
        GameObject indicator = new GameObject("indicator");
        
        SpriteRenderer indiRenderer = indicator.AddComponent<SpriteRenderer>();
        indiRenderer.sprite = sprite;
        indiRenderer.sortingOrder = 7;
        
        return indicator;
    }

    public IEnumerator AttackBasedIndicator(GameObject indicator)
    {
        Debug.Log("공격작동");
        //피격후 바로 파괴
        SpriteRenderer indiRenderer = indicator.GetComponent<SpriteRenderer>();
        indiRenderer.sprite = null;
        PolygonCollider2D polygonCollider = indicator.AddComponent<PolygonCollider2D>();
        polygonCollider.isTrigger = true;
        indicator.layer = 8;
        yield return new WaitForSeconds(0.3f);
        Destroy(indicator);
    }

    public IEnumerator LongAttackBasedIndicator(GameObject indicator)
    {
        SpriteRenderer indiRenderer = indicator.GetComponent<SpriteRenderer>();
        indiRenderer.sprite = null;
        Destroy(indicator);
        return null;
    }
}
