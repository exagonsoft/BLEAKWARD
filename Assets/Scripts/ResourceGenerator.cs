using UnityEngine;

public class ResourceGenerator : MonoBehaviour
{
    private float timer;
    private float timerMax;
    private ResourceGeneratorData resourceGeneratorData;

    private void Awake()
    {
        resourceGeneratorData = GetComponent<BuildingTypeHolder>().buildingType.resourceGeneratorData;
        timerMax = resourceGeneratorData.timerMax;
        timer = timerMax;
    }

    private void Start()
    {
        Collider2D[] colliderArray = Physics2D.OverlapCircleAll(transform.position, resourceGeneratorData.resourceDetectionRadius);
        int nearbyResourceAmount = 0;

        foreach (Collider2D collider2D in colliderArray)
        {
             ResourceNode resourceNode = collider2D.GetComponent<ResourceNode>();
            if(resourceNode != null)
            {
                if(resourceNode.resourceType == resourceGeneratorData.resourceType)
                {
                    nearbyResourceAmount++;
                }
            }
        }
        nearbyResourceAmount = Mathf.Clamp(nearbyResourceAmount, 0, resourceGeneratorData.maxResourceAmount);
        if(nearbyResourceAmount == 0)
        {
            enabled = false;
        }
        else
        {
            timerMax = resourceGeneratorData.timerMax / nearbyResourceAmount;
        }
    }

    private void Update()
    {
        timer -= Time.deltaTime;
        if(timer <= 0)
        {
            timer += timerMax;
            ResourceManger.Instance.AddResource(resourceGeneratorData.resourceType, resourceGeneratorData.ammountPerGeneration);
        }
    }
}
