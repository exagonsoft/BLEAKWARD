using System;
using UnityEngine;

public class BuildingGhost : MonoBehaviour
{
    [SerializeField] private GameObject spriteGameObject;
    [SerializeField] private ResourcesNearByOverlayUI resourcesNearByOverlayUI;

    private void Awake()
    {
        Hide();
    }

    private void Start()
    {
        BuildingManager.Instance.OnActiveBuildingTypeChanged += BuildingManager_OnActiveBuildingTypeChanged;
    }
    

    private void Update()
    {
        transform.position = UtilsClass.GetMouseWorldPosition();
    }

    private void BuildingManager_OnActiveBuildingTypeChanged(object sender, BuildingManager.OnActiveBuildingTypeChangedEventArgs e)
    {
        if (e.activeBuildingType == null)
        {
            Hide();
        }
        else
        {
            Show(e.activeBuildingType.sprite);
            if (e.activeBuildingType.isResourceGenerator)
            {
                resourcesNearByOverlayUI.Show(e.activeBuildingType.resourceGeneratorData);
            }
            else
            {
                resourcesNearByOverlayUI.Hide();
            }
        }
    }

    private void Hide()
    {
        spriteGameObject.SetActive(false);
        resourcesNearByOverlayUI.Hide();
    }

    private void Show(Sprite sprite)
    {
        spriteGameObject.SetActive(true);
        
        spriteGameObject.GetComponent<SpriteRenderer>().sprite = sprite;
    }

}
