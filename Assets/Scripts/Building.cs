using UnityEngine;
using UnityEngine.UI;

public class Building : MonoBehaviour
{
    private HealthSystem healthSystem;
    private BuildingTypeSO buildingType;
    [SerializeField] private GameObject buildingManagementUI;

    private bool isShowingBuildingManagementUI = false;

    private Button managementButton;

    private void Awake()
    {
        managementButton = transform.GetComponentInChildren<Button>();
        Debug.Log("Management Button: " + managementButton);
        managementButton?.onClick.AddListener(() =>
        {
            HandlemanagementUI();
        });
    }

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

    private void ShowManagementUI()
    {
        if (isShowingBuildingManagementUI) return;
        buildingManagementUI.SetActive(true);
        isShowingBuildingManagementUI = true;
    }

    private void HideManagementUI()
    {
        if (!isShowingBuildingManagementUI) return;
        buildingManagementUI.SetActive(false);
        isShowingBuildingManagementUI = false;
    }

    private void HealthSystem_OnDead(object sender, System.EventArgs e)
    {
        Destroy(gameObject);
    }

    public void HandlemanagementUI()
    {
        if (isShowingBuildingManagementUI)
        {
            HideManagementUI();
            Debug.Log("Management UI hidden");
        }
        else
        {
            ShowManagementUI();
            Debug.Log("Management UI shown");
        }
    }
}
