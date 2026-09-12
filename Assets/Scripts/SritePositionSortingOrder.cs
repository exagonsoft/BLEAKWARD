using UnityEngine;

public class SritePositionSortingOrder : MonoBehaviour
{
    [SerializeField] private bool runOnce = false;
    [SerializeField] private float positionOffsetY;
    private SpriteRenderer spriteRenderer;
    [SerializeField] private float presitionSortingOrder = 5f;
    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
    }

    private void LateUpdate()
    {
        spriteRenderer.sortingOrder = (int)(-(transform.position.y + positionOffsetY) * presitionSortingOrder);
        if(runOnce)
        {
            Destroy(this);
        }
    }
}
