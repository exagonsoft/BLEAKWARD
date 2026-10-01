using System;
using UnityEngine;
using UnityEngine.EventSystems;

public class BuildingManager : MonoBehaviour
{

    #region Class Fields

    public static BuildingManager Instance { get; private set; }

    public event EventHandler<OnActiveBuildingTypeChangedEventArgs> OnActiveBuildingTypeChanged;

    public class OnActiveBuildingTypeChangedEventArgs
    {
        public BuildingTypeSO activeBuildingType;
    }

    [SerializeField] private Building townHallBuilding;

    private BuildingTypeListSO buildingTypeList;
    private BuildingTypeSO activeBuildingType;
    private Camera mainCamera;

    #endregion

    #region Unity Methods

    private void Awake()
    {
        if(Instance != null)
        {
            Debug.LogError("There is more than one BuildingManager instance!");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializeGameResources();
    }

    private void Start()
    {
        GetProperties();
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject())
        {
            if(activeBuildingType != null)
            {
                string onBuildError = "";
                if (!CanSpawnBuilding(activeBuildingType, UtilsClass.GetMouseWorldPosition(), out onBuildError))
                {
                    ToolTipUI.Instance.Show(onBuildError, new ToolTipUI.ToolTipTimer { timer = 2f });
                }
                else
                {
                    if (ResourceManger.Instance.CanAfford(activeBuildingType.constructionResourceCosts))
                    {
                        ResourceManger.Instance.SpendResources(activeBuildingType.constructionResourceCosts);
                        //Instantiate(activeBuildingType.buildingPrefab, UtilsClass.GetMouseWorldPosition(), Quaternion.identity);
                        UnderBuildingConstruction.Create(UtilsClass.GetMouseWorldPosition(), activeBuildingType);
                        Vector2 buildingColliderArea = activeBuildingType.buildingPrefab.GetComponent<BoxCollider2D>().size;
                        Collider2D[] detailColliders = Physics2D.OverlapBoxAll(UtilsClass.GetMouseWorldPosition(), buildingColliderArea, 0f);
                        foreach (Collider2D detailCollider2D in detailColliders)
                        {
                            DetailNode detailNode = detailCollider2D.GetComponent<DetailNode>();
                            if (detailNode != null)
                            {
                                detailNode.DestroyDetailNode();
                            }

                        }
                    }
                    else
                    {
                        onBuildError = $"Insufficient resources. \n{activeBuildingType.GetConstructionResources()}";
                        ToolTipUI.Instance.Show(onBuildError, new ToolTipUI.ToolTipTimer { timer = 2f });
                        return;
                    }
                }
                

            }
        }
    }

    #endregion


    #region Private Methods

    private void GetProperties()
    {
        mainCamera = Camera.main;
        
    }

    private void InitializeGameResources()
    {
        buildingTypeList = Resources.Load<BuildingTypeListSO>(typeof(BuildingTypeListSO).Name);
        activeBuildingType = null;
    }

    private bool CanSpawnBuilding(BuildingTypeSO buildingType, Vector3 position, out string reason)
    {
        reason = "";
        BoxCollider2D boxCollider2D = buildingType.buildingPrefab.GetComponent<BoxCollider2D>();

        Collider2D[] collider2DArray = Physics2D.OverlapBoxAll(position + (Vector3)boxCollider2D.offset, boxCollider2D.size, 0f);

        
        foreach (Collider2D collider2D in collider2DArray)
        {
            collider2D.TryGetComponent<DetailNode>(out DetailNode detailNode);
            collider2D.TryGetComponent<Proyectil>(out Proyectil proyectil);
            if (detailNode != null || proyectil != null)
            {
                continue;
            }
            else
            {
                reason = "\nCannot spawn building here.";
                return false;
            }
        }

        collider2DArray = Physics2D.OverlapCircleAll(position, buildingType.minConstructionRadius);

        foreach (Collider2D collider2D in collider2DArray)
        {
            collider2D.TryGetComponent<BuildingTypeHolder>(out BuildingTypeHolder buildingTypeHolder);
            if(buildingTypeHolder != null)
            {
                if(buildingTypeHolder.buildingType == buildingType)
                {
                    reason = "\nToo close to another building of the same type.";
                    return false;
                }
            }
        }

        float maxConstructionRadius = 25f;
        collider2DArray = Physics2D.OverlapCircleAll(position, maxConstructionRadius);

        foreach (Collider2D collider2D in collider2DArray)
        {
            collider2D.TryGetComponent<BuildingTypeHolder>(out BuildingTypeHolder buildingTypeHolder);
            if (buildingTypeHolder != null)
            {
               return true;
            }
        }

        reason = "\nToo far from any other building.";
        return false;
    }

    #endregion

    #region Public Methods

    public void SetActiveBuildingType(BuildingTypeSO buildingType)
    {
        this.activeBuildingType = buildingType;
        OnActiveBuildingTypeChanged?.Invoke(this, new OnActiveBuildingTypeChangedEventArgs { activeBuildingType = buildingType });
    }

    public BuildingTypeSO GetActiveBuildingType()
    {
        return activeBuildingType;
    }

    public Building GetTownHallBuilding()
    {
        return townHallBuilding;
    }

    

    #endregion
}
