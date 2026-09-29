using UnityEngine;

[CreateAssetMenu(fileName = "ResourceTypeSO", menuName = "Scriptable Objects/ResourceType")]
public class ResourceTypeSO : ScriptableObject
{
    public string resourceType;
    public string resourceTypeShortName;
    public Sprite resourceSprite;
    public string resourceColorHex;
}
