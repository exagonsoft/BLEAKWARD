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
            if(activeBuildingType != null && CanSpawnBuilding(activeBuildingType, UtilsClass.GetMouseWorldPosition()))
            {
                Instantiate(activeBuildingType.buildingPrefab, UtilsClass.GetMouseWorldPosition(), Quaternion.identity);
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

    private bool CanSpawnBuilding(BuildingTypeSO buildingType, Vector3 position)
    {
        BoxCollider2D boxCollider2D = buildingType.buildingPrefab.GetComponent<BoxCollider2D>();

        Collider2D[] collider2DArray = Physics2D.OverlapBoxAll(position + (Vector3)boxCollider2D.offset, boxCollider2D.size, 0f);
       
        bool isAreaClear = collider2DArray.Length == 0;
        if (!isAreaClear)
        {
            return false;
        }

        collider2DArray = Physics2D.OverlapCircleAll(position, buildingType.minConstructionRadius);

        foreach (Collider2D collider2D in collider2DArray)
        {
            collider2D.TryGetComponent<BuildingTypeHolder>(out BuildingTypeHolder buildingTypeHolder);
            if(buildingTypeHolder != null)
            {
                if(buildingTypeHolder.buildingType == buildingType)
                {
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

    

    #endregion
}
