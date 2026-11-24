using UnityEngine;
using System;

public class Target : MonoBehaviour, IDamageable, IHitDamageReceiver
{
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private bool destroyOnDeath = false;
    [SerializeField] private float respawnDelay = 3f;
    
    private float currentHealth;
    private Vector3 startPosition;
    private Quaternion startRotation;
    private static readonly int ColorPropertyId = Shader.PropertyToID("_Color");
    private MaterialPropertyBlock flashBlock;
    private readonly System.Collections.Generic.List<Renderer> flashRenderers = new System.Collections.Generic.List<Renderer>();
    
    public event Action<float, float> OnHealthChanged;
    public event Action<DamageInfo> OnDamageTaken;
    public event Action OnDeath;
    
    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public int TotalHitsTaken { get; private set; }
    public float TotalDamageTaken { get; private set; }
    
    private void Awake()
    {
        currentHealth = maxHealth;
        startPosition = transform.position;
        startRotation = transform.rotation;
    }
    
    public void TakeDamage(DamageInfo damageInfo)
    {
        if (IsDead()) return;
        
        TotalHitsTaken++;
        TotalDamageTaken += damageInfo.damage;
        
        float previousHealth = currentHealth;
        currentHealth = Mathf.Max(0, currentHealth - damageInfo.damage);
        
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
        OnDamageTaken?.Invoke(damageInfo);
        
        FlashHitFeedback(damageInfo.isHeadshot);
        
        if (currentHealth <= 0)
        {
            Die();
        }
    }
    
    public void ApplyHitDamage(HitDamageInfo damage)
    {
        var damageInfo = new DamageInfo
        {
            damage = damage.FinalDamage,
            hitPoint = damage.Point,
            hitNormal = damage.Normal,
            isHeadshot = damage.Zone == HitZoneType.Head
        };
        TakeDamage(damageInfo);
    }
    
    public float GetHealth() => currentHealth;
    
    public bool IsDead() => currentHealth <= 0;
    
    private void FlashHitFeedback(bool isHeadshot)
    {
        GetComponentsInChildren(false, flashRenderers);
        if (flashRenderers.Count == 0)
            return;

        if (flashBlock == null)
            flashBlock = new MaterialPropertyBlock();

        flashBlock.SetColor(ColorPropertyId, isHeadshot ? Color.yellow : Color.red);
        for (int i = 0; i < flashRenderers.Count; i++)
        {
            if (flashRenderers[i] != null)
                flashRenderers[i].SetPropertyBlock(flashBlock);
        }

        CancelInvoke(nameof(ResetColor));
        Invoke(nameof(ResetColor), 0.1f);
    }
    
    private void ResetColor()
    {
        GetComponentsInChildren(true, flashRenderers);
        for (int i = 0; i < flashRenderers.Count; i++)
        {
            if (flashRenderers[i] != null)
                flashRenderers[i].SetPropertyBlock(null);
        }
    }
    
    private void Die()
    {
        OnDeath?.Invoke();
        
        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
        else
        {
            gameObject.SetActive(false);
            Invoke(nameof(Respawn), respawnDelay);
        }
    }
    
    private void Respawn()
    {
        currentHealth = maxHealth;
        TotalHitsTaken = 0;
        TotalDamageTaken = 0;
        transform.position = startPosition;
        transform.rotation = startRotation;
        gameObject.SetActive(true);
        ResetColor();
        OnHealthChanged?.Invoke(currentHealth, maxHealth);
    }
    
    public void ResetTarget()
    {
        CancelInvoke();
        Respawn();
    }
}
