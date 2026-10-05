using UnityEngine;
using UnityEngine.EventSystems;

public class Building : MonoBehaviour
{
    private HealthSystem healthSystem;
    private BuildingTypeSO buildingType;
    [SerializeField] private GameObject buildingManagementUI;

    private bool isShowingBuildingManagementUI = false;

    private void Awake()
    {
        if (buildingManagementUI == null) return;
        // Keep the cached flag in sync with the panel's authored state so the first click always toggles.
        isShowingBuildingManagementUI = buildingManagementUI.activeSelf;
        buildingManagementUI.SetActive(isShowingBuildingManagementUI);
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
        if (buildingManagementUI == null) return;
        if (!Input.GetMouseButtonDown(0)) return;
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) return;
        // While a building type is selected the click is a placement click, not a selection click.
        if (BuildingManager.Instance != null && BuildingManager.Instance.GetActiveBuildingType() != null) return;

        Collider2D collider2D = GetComponent<Collider2D>();
        if (collider2D != null && collider2D.OverlapPoint(UtilsClass.GetMouseWorldPosition()))
        {
            HandlemanagementUI();
        }
    }

    private void ShowManagementUI()
    {
        if (buildingManagementUI == null) return;
        if (isShowingBuildingManagementUI) return;
        buildingManagementUI.SetActive(true);
        isShowingBuildingManagementUI = true;
    }

    private void HideManagementUI()
    {
        if (buildingManagementUI == null) return;
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
