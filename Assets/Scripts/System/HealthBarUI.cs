using UnityEngine;

public class HealthBarUI : MonoBehaviour
{
    [SerializeField] private HealthSystem healthSystem;
    private Transform barTransform;

    private void Awake()
    {
        barTransform = transform.Find("bar");
    }

    public void Start()
    {
        healthSystem.OnDamage += HealthSystem_OnDamage;
        UpdateBar();
        UpdateHealthBarVisible();
    }

    private void HealthSystem_OnDamage(object sender, System.EventArgs e)
    {
        UpdateBar();
        UpdateHealthBarVisible();
    }

    private void UpdateBar() {
        barTransform.localScale = new Vector3(healthSystem.GetHealthAmountNormalized(), 1, 1);
    }

    private void UpdateHealthBarVisible()
    {
        if(healthSystem.IsFullHealth())
        {
            gameObject.SetActive(false);
        }
        else 
        {
            gameObject.SetActive(true);
        }
    }

}
        