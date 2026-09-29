using TMPro;
using UnityEngine;

public class ToolTipUI : MonoBehaviour
{
    public static ToolTipUI Instance { get; private set; }

    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private RectTransform backgroundTransform;
    [SerializeField] private RectTransform canvasRectTransform;

    private RectTransform rectTransform;
    private ToolTipTimer toolTipTimer;

    private void Awake()
    {
        Instance = this;
        rectTransform = transform.GetComponent<RectTransform>();
        Hide();
    }

    private void Update()
    {
        TooltipPosition();
    }

    private void SetText(string text) 
    {
        label.text = text;
        label.ForceMeshUpdate();

        Vector2 textSize = label.GetRenderedValues(false);
        Vector2 padding = new Vector2(8, 8);
        backgroundTransform.sizeDelta = textSize + padding;
    }

    private void TooltipPosition()
    {
        HandleFollowMouse();

        if (toolTipTimer != null)
        {
            toolTipTimer.timer -= Time.deltaTime;
            if (toolTipTimer.timer <= 0)
            {
                Hide();
            }
        }
    }

    private void HandleFollowMouse()
    {
        Vector2 anchoredPosition = Input.mousePosition / canvasRectTransform.localScale.x;
        if (anchoredPosition.x + backgroundTransform.rect.width > canvasRectTransform.rect.width)
        {
            anchoredPosition.x = canvasRectTransform.rect.width - backgroundTransform.rect.width;
        }

        if (anchoredPosition.x < 0)
        {
            anchoredPosition.x = 0;
        }

        if (anchoredPosition.y + backgroundTransform.rect.height > canvasRectTransform.rect.height)
        {
            anchoredPosition.y = canvasRectTransform.rect.height - backgroundTransform.rect.height;
        }

        if (anchoredPosition.y < 0)
        {
            anchoredPosition.y = 0;
        }

        rectTransform.anchoredPosition = anchoredPosition;
    }

    public void Show(string text, ToolTipTimer toolTipTimer = null)
    {
        this.toolTipTimer = toolTipTimer;
        gameObject.SetActive(true);
        SetText(text);
        HandleFollowMouse();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public class ToolTipTimer
    {
        public float timer;
    }
}
