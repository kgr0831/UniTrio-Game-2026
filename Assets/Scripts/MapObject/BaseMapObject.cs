using System.Collections;
using UnityEngine;

[RequireComponent(typeof(SpriteRenderer))]
public abstract class BaseMapObject : MonoBehaviour
{
    public MapObjectData data;
    
    protected float currentHealth;
    protected SpriteRenderer spriteRenderer;
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
    }

    public virtual IEnumerator Gimic()
    {
        return null;
    }
    
    

    public virtual void TakeDamage(float damage)
    {
        currentHealth -= damage;
        if (currentHealth <= 0)
        {
            Gimic();
        }
    }
}