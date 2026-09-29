using System;
using UnityEngine;

public class HealthSystem : MonoBehaviour
{
    [SerializeField] private float healthAmountMax;
    private float healthAmount;
    public event EventHandler OnDamage;
    public event EventHandler OnDead;

    private void Awake()
    {
        healthAmount = healthAmountMax;
    }

    public void Damage(float damageAmount)
    {
        healthAmount -= damageAmount;
        healthAmount = Mathf.Clamp(healthAmount, 0, healthAmountMax);

        OnDamage?.Invoke(this, EventArgs.Empty);
        if(IsDead())
        {
            OnDead?.Invoke(this, EventArgs.Empty);
        }
    }

    public bool IsDead()
    {
        return healthAmount <= 0;
    }

    public float GetHealthAmount()
    {
        return healthAmount;
    }

    public float GetHealthAmountNormalized()
    {
        return healthAmount / healthAmountMax;
    }

    public void SetAmountMax(float amountMax, bool updateHealthAmount = false)
    {
        healthAmountMax = amountMax;
        if(updateHealthAmount)
        {
            healthAmount = amountMax;
        }
    }

    public bool IsFullHealth()
    {
        return healthAmount == healthAmountMax;
    }
}
