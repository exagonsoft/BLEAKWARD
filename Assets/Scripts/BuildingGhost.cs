using System;
using UnityEngine;

public class BuildingGhost : MonoBehaviour
{
    [SerializeField] private GameObject spriteGameObject;

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
            Show(e.activeBuildingType);
        }
    }

    private void Hide()
    {
        spriteGameObject.SetActive(false);
    }

    private void Show(BuildingTypeSO buildingType)
    {
        spriteGameObject.SetActive(true);
        spriteGameObject.GetComponent<SpriteRenderer>().sprite = buildingType.sprite;
    }

}
