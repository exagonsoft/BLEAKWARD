using TMPro;
using UnityEngine;

public class ResourcesNearByOverlayUI : MonoBehaviour
{
    [SerializeField] TextMeshPro label;
    [SerializeField] SpriteRenderer icon;
    private ResourceGeneratorData resourceGeneratorData;

    private void Update() {
        int resourceAmount = ResourceGenerator.GetNearByResourceAmmount(resourceGeneratorData, transform.position);
        float resourcePercentage = (float)resourceAmount / resourceGeneratorData.maxResourceAmount;
        label.SetText($"{(resourcePercentage * 100f).ToString("F1")}%");
    }

    public void Show(ResourceGeneratorData resourceGeneratorData)
    {
        this.resourceGeneratorData = resourceGeneratorData;
        gameObject.SetActive(true);

        icon.sprite = resourceGeneratorData.resourceType.resourceSprite;
        
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }
}
