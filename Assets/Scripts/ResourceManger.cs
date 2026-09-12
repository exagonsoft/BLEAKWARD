using System;
using System.Collections.Generic;
using UnityEngine;

public class ResourceManger : MonoBehaviour
{

    public static ResourceManger Instance { get; private set; }

    public event EventHandler OnResourceAmountChanged;
    private Dictionary<ResourceTypeSO, int> resourceAmountDictionary;
    private ResourceTypeListSO resourceTypeList;

    private void Awake()
    {
        if (Instance != null)
        {
            Debug.LogError("There is more than one ResourceManger instance!");
            Destroy(gameObject);
            return;
        }
        Instance = this;
        InitializeGameResources();
    }

    private void Start()
    {
    }

    private void Update()
    {

    }

    private void InitializeGameResources()
    {
        resourceAmountDictionary = new Dictionary<ResourceTypeSO, int>();
        resourceTypeList = Resources.Load<ResourceTypeListSO>(typeof(ResourceTypeListSO).Name);

        foreach (ResourceTypeSO resourceType in resourceTypeList.list)
        {
            resourceAmountDictionary[resourceType] = 0;
        }
    }


    public void AddResource(ResourceTypeSO resourceType, int amount)
    {
        if (resourceAmountDictionary.ContainsKey(resourceType))
        {
            resourceAmountDictionary[resourceType] += amount;
            OnResourceAmountChanged?.Invoke(this, EventArgs.Empty);
        }
        else
        {
            Debug.LogWarning("Resource type not found in the dictionary: " + resourceType.resourceType);
        }
    }

    public int GetResourceAmount(ResourceTypeSO resourceType)
    {
        return resourceAmountDictionary[resourceType];
    }
}
