using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ResourcesUI : MonoBehaviour
{
    private ResourceTypeListSO resourceTypeList;
    private Dictionary<ResourceTypeSO, Transform> resourceTypeTransformDictionary;
    [SerializeField] private Transform resourceTemplate;

    private void Awake()
    {
        InitializeResources();
    }

    private void Start()
    {
        SubscribeEvents();
        RenderUI();
    }

    private void InitializeResources()
    {
        resourceTypeList = Resources.Load<ResourceTypeListSO>(typeof(ResourceTypeListSO).Name);
        resourceTypeTransformDictionary = new Dictionary<ResourceTypeSO, Transform>();
        resourceTemplate.gameObject.SetActive(false);
    }

    private void RenderUI()
    {
        foreach (ResourceTypeSO resourceType in resourceTypeList.list)
        {
            resourceTemplate.Find("Icon").GetComponent<Image>().sprite = resourceType.resourceSprite;
            int resourceAmount = ResourceManger.Instance.GetResourceAmount(resourceType);
            resourceTemplate.Find("Label").GetComponent<TextMeshProUGUI>().text = resourceAmount.ToString();
            Transform resourceTransform = Instantiate(resourceTemplate, transform);
            resourceTransform.gameObject.SetActive(true);
            resourceTypeTransformDictionary[resourceType] = resourceTransform;
        }
    }

    private void UpdateResources()
    {
        foreach (KeyValuePair<ResourceTypeSO, Transform> kvp in resourceTypeTransformDictionary)
        {
            ResourceTypeSO resourceType = kvp.Key;
            Transform resourceTransform = kvp.Value;
            int resourceAmount = ResourceManger.Instance.GetResourceAmount(resourceType);
            resourceTransform.Find("Label").GetComponent<TextMeshProUGUI>().text = resourceAmount.ToString();
        }
    }

    private void SubscribeEvents()
    {
        ResourceManger.Instance.OnResourceAmountChanged += ResourceManager_OnResourceAmountChanged;
    }

    private void ResourceManager_OnResourceAmountChanged(object sender, EventArgs e)
    {
        UpdateResources();
    }
}
