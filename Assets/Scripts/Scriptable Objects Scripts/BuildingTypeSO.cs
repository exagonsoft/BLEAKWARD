using UnityEngine;

[CreateAssetMenu(fileName = "BuildingTypeSO", menuName = "Scriptable Objects/BuildingType")]
public class BuildingTypeSO : ScriptableObject
{
    public string buildingName;
    public Transform buildingPrefab;
    public Sprite sprite;
    public float minConstructionRadius;
    public ResourceGeneratorData resourceGeneratorData;
}
