using UnityEngine;

[System.Serializable]
public class ResourceGeneratorData
{
    public float timerMax = 1f;
    public int ammountPerGeneration = 1;
    public float resourceDetectionRadius = 5f;
    public int maxResourceAmount = 5;
    public ResourceTypeSO resourceType;
}
