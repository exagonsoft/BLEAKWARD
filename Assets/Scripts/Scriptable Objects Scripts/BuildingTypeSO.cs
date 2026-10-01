using UnityEngine;

[CreateAssetMenu(fileName = "BuildingTypeSO", menuName = "Scriptable Objects/BuildingType")]
public class BuildingTypeSO : ScriptableObject
{
    public string buildingName;
    public Transform buildingPrefab;
    public Sprite sprite;
    public float minConstructionRadius;
    public bool isResourceGenerator;
    public ResourceGeneratorData resourceGeneratorData;
    public ResourceAmmount[] constructionResourceCosts;
    public float healthAmountMax;
    public float constructionTimerMax;

    public string GetConstructionResources()
    {
        string resources = "";
        foreach (ResourceAmmount resourceAmmount in constructionResourceCosts)
        {
            resources += $"<color=#{resourceAmmount.resourceType.resourceColorHex}>{resourceAmmount.resourceType.resourceTypeShortName}: {resourceAmmount.ammount}</color>\n";
        }
        return resources;
    }
}
