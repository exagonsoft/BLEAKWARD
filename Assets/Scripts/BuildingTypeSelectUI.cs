using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BuildingTypeSelectUI : MonoBehaviour
{
    [SerializeField] private Transform buildingTemplate;
    [SerializeField] private Sprite mousePointerSprite;
    [SerializeField] private List<BuildingTypeSO> ignoreBuildingTypeList;
    private BuildingTypeListSO buildingsList;
    private Dictionary<BuildingTypeSO, Transform> buildingTypeTransformDictionary;
    private Transform mousePointerButton;

    private void Awake()
    {
        buildingTemplate.gameObject.SetActive(false);
        buildingTypeTransformDictionary = new Dictionary<BuildingTypeSO, Transform>();
        buildingsList = Resources.Load<BuildingTypeListSO>(typeof(BuildingTypeListSO).Name);
    }

    private void Start()
    {
        BuildingManager.Instance.OnActiveBuildingTypeChanged += BuildingManager_OnActiveBuildingTypeChanged;

        mousePointerButton = Instantiate(buildingTemplate, transform);
        mousePointerButton.gameObject.SetActive(true);
        mousePointerButton.Find("Icon").GetComponent<Image>().sprite = mousePointerSprite;

        mousePointerButton.GetComponent<Button>().onClick.AddListener(() =>
        {
            BuildingManager.Instance.SetActiveBuildingType(null);
        });

        foreach (BuildingTypeSO buildingType in buildingsList.list)
        {
            if(ignoreBuildingTypeList.Contains(buildingType)) continue;
            Transform buildingTransform = Instantiate(buildingTemplate, transform);
            buildingTransform.gameObject.SetActive(true);
            buildingTransform.Find("Icon").GetComponent<Image>().sprite = buildingType.sprite;

            buildingTransform.GetComponent<Button>().onClick.AddListener(() =>
            {
                BuildingManager.Instance.SetActiveBuildingType(buildingType);
            });

            buildingTypeTransformDictionary[buildingType] = buildingTransform;
        }

        UpdateActiveBuildingTypeButton();
    }

    private void BuildingManager_OnActiveBuildingTypeChanged(object sender, BuildingManager.OnActiveBuildingTypeChangedEventArgs e)
    {
        UpdateActiveBuildingTypeButton();
    }

    private void UpdateActiveBuildingTypeButton()
    {
        BuildingTypeSO activeBuildingType = BuildingManager.Instance.GetActiveBuildingType();
        mousePointerButton.Find("SelectedVFX").gameObject.SetActive(activeBuildingType == null);   
        foreach (BuildingTypeSO buildingType in buildingTypeTransformDictionary.Keys)
        {
            Transform transform = buildingTypeTransformDictionary[buildingType];
            transform.Find("SelectedVFX").gameObject.SetActive(buildingType == activeBuildingType);
        }
    }
}
