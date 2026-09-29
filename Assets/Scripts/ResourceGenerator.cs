using UnityEngine;

public class ResourceGenerator : MonoBehaviour
{
    private float timer;
    private float timerMax;
    private ResourceGeneratorData resourceGeneratorData;
    private float generatedResourceAmount;
    private int nearbyResourceAmount = 0;

    public static int GetNearByResourceAmmount(ResourceGeneratorData resourceGeneratorData, Vector3 position)
    {
        Collider2D[] colliderArray = Physics2D.OverlapCircleAll(position, resourceGeneratorData.resourceDetectionRadius);
        int nearbyResourceAmount = 0;
        foreach (Collider2D collider2D in colliderArray)
        {
            ResourceNode resourceNode = collider2D.GetComponent<ResourceNode>();
            if (resourceNode != null)
            {
                if (resourceNode.resourceType == resourceGeneratorData.resourceType)
                {
                    nearbyResourceAmount++;
                }
            }
        }
        nearbyResourceAmount = Mathf.Clamp(nearbyResourceAmount, 0, resourceGeneratorData.maxResourceAmount);
        return nearbyResourceAmount;
    }   

    private void Awake()
    {
        resourceGeneratorData = GetComponent<BuildingTypeHolder>().buildingType.resourceGeneratorData;
        timerMax = resourceGeneratorData.timerMax;
        timer = timerMax;

        nearbyResourceAmount = GetNearByResourceAmmount(resourceGeneratorData, transform.position);

        if (nearbyResourceAmount == 0)
        {
            enabled = false;
        }
        else
        {
            float performanceMultiplier = nearbyResourceAmount / (float)resourceGeneratorData.maxResourceAmount;
            generatedResourceAmount = resourceGeneratorData.ammountPerGeneration * performanceMultiplier;
            timerMax = resourceGeneratorData.timerMax / performanceMultiplier;
        }

    }

    private void Start()
    {
        
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if(timer <= 0)
        {
            timer += timerMax;
            ResourceManger.Instance.AddResource(resourceGeneratorData.resourceType, (int)generatedResourceAmount);
        }
    }

    public ResourceGeneratorData GetResourceData()
    {
        return resourceGeneratorData;
    }

    public float GetTimerNormalized()
    {
        return timer / timerMax;
    }

    public float GetAmmountGeneratedPerSecound()
    {
        return generatedResourceAmount;
    }
}
