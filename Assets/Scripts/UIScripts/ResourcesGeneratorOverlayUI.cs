using TMPro;
using UnityEngine;

public class ResourcesGeneratorOverlayUI : MonoBehaviour
{
    [SerializeField] private ResourceGenerator resourceGenerator;
    [SerializeField] private SpriteRenderer icon;
    [SerializeField] private TextMeshPro label;
    [SerializeField] private Transform bar;

    private void Start()
    {
        ResourceGeneratorData resourceData = resourceGenerator.GetResourceData();
        icon.sprite = resourceData.resourceType.resourceSprite;
        label.GetComponent<TextMeshPro>().text = resourceGenerator.GetAmmountGeneratedPerSecound().ToString("F1");
    }

    private void Update()
    {
        bar.localScale = new Vector3(1 - resourceGenerator.GetTimerNormalized(), 1, 1);
    }
}
