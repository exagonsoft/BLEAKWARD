using System;
using UnityEngine;
using UnityEngine.UI;

public class BuildingDemolishButton : MonoBehaviour
{
    [SerializeField] private Building building;
    private void Awake()
    {
        GetComponent<Button>()?.onClick.AddListener(DemolishBuilding);
    }

    private void DemolishBuilding()
    {
        Destroy(building.gameObject);
    }
}
