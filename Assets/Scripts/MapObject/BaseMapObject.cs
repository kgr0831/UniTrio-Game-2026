using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public abstract class BaseMapObject : MonoBehaviour
{
    public MapObjectData data;
    
    protected float currentHealth;
    protected SpriteRenderer spriteRenderer;
    protected GameObject indicatorInstance;
    protected Animator animator;

    protected virtual void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    protected virtual void OnEnable()
    {
        if (data == null) return;

        currentHealth = data.maxHealth;
        
        // 1. 스프라이트 적용
        if (spriteRenderer != null)
        {
            spriteRenderer.sprite = data.visualSprite;
        }

        // 2. 인디케이터 초기화
        if (indicatorInstance == null && data.indicatorPrefab != null)
        {
            indicatorInstance = Instantiate(data.indicatorPrefab, transform);
            SetupIndicator();
        }
        
        SetIndicator(false);
    }

    public virtual IEnumerator Gimic()
    {
        SetIndicator(true);
        yield return new WaitForSeconds(2.0f);
        SetIndicator(false);
        animator.SetTrigger("Die");
    }

    public virtual void SetupIndicator()
    {
        // Pivot이 Bottom인 Square 기준으로 세팅
        indicatorInstance.transform.localScale = new Vector3(data.indicatorWidth, data.indicatorLength, 1f);
        // 부모의 중심에서 Y축(위쪽)으로 Offset만큼 이동
        indicatorInstance.transform.localPosition = new Vector3(0, data.indicatorOffset, 0);
    }

    public void SetIndicator(bool active)
    {
        if (indicatorInstance != null) indicatorInstance.SetActive(active);
        Debug.Log("성공");
    }

    // 인디케이터의 OnTrigger에서 호출할 함수
    public virtual void OnDetected(bool isDetected) => SetIndicator(isDetected);

    public virtual void TakeDamage(float damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Gimic();
        }
    }
}