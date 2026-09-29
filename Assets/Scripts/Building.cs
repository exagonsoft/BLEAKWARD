using UnityEngine;

public class Building : MonoBehaviour
{
    private HealthSystem healthSystem;
    private BuildingTypeSO buildingType;

    private void Start()
    {
        buildingType = GetComponent<BuildingTypeHolder>().buildingType;
        healthSystem = GetComponent<HealthSystem>();
        healthSystem.SetAmountMax(buildingType.healthAmountMax, true);
        healthSystem.OnDead += HealthSystem_OnDead;
    }

    private void Update()
    {
        ///
    }

    private void HealthSystem_OnDead(object sender, System.EventArgs e)
    {
        Destroy(gameObject);
    }
}
