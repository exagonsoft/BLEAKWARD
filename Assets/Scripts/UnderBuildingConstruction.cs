using UnityEngine;

public class UnderBuildingConstruction : MonoBehaviour
{
    public static UnderBuildingConstruction Create(Vector3 position, BuildingTypeSO buildingType)
    {
        Transform buildingUnderConstructionTransform = Resources.Load<Transform>("PFUnderBuildingContruction");
        Transform buildingUnderConstructionInstance = Instantiate(buildingUnderConstructionTransform, position, Quaternion.identity);
        UnderBuildingConstruction underBuildingConstruction = buildingUnderConstructionInstance.GetComponent<UnderBuildingConstruction>();
        underBuildingConstruction.SetBuildingType(buildingType);
        return underBuildingConstruction;
    }

    [SerializeField] private float constructionTimerMax = 5f; // Time in seconds for the building to be constructed
    private float constructionTimer;
    private BuildingTypeSO buildingType;
    private BoxCollider2D boxCollider2D;
    [SerializeField] private SpriteRenderer spriteRenderer;
    private BuildingTypeHolder buildingtypeHolder;
    private Material constructionMaterial;

    private void Awake()
    {
        boxCollider2D = GetComponent<BoxCollider2D>();
        buildingtypeHolder = GetComponent<BuildingTypeHolder>();
        constructionMaterial = spriteRenderer.material;
    }

    private void Update()
    {
        constructionTimer -= Time.deltaTime;
        constructionMaterial.SetFloat("_Progress", GetConstructionTimerNormalized());
        if (constructionTimer <= 0f)
        {
            Debug.Log("Building construction complete!");
            Instantiate(buildingType.buildingPrefab, transform.position, Quaternion.identity);
            Destroy(gameObject);
        }
    }

    private void SetBuildingType(BuildingTypeSO buildingType)
    {
        this.constructionTimerMax = buildingType.constructionTimerMax;
        this.buildingType = buildingType;
        constructionTimer = constructionTimerMax;
        spriteRenderer.sprite = buildingType.sprite;
        boxCollider2D.offset = buildingType.buildingPrefab.GetComponent<BoxCollider2D>().offset;
        boxCollider2D.size = buildingType.buildingPrefab.GetComponent<BoxCollider2D>().size;
        buildingtypeHolder.buildingType = buildingType;
    }

    public float GetConstructionTimerNormalized()
    {
        return 1 - constructionTimer / constructionTimerMax;
    }
}
