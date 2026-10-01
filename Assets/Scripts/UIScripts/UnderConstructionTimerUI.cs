using UnityEngine;
using UnityEngine.UI;

public class UnderConstructionTimerUI : MonoBehaviour
{
    [SerializeField] private Image constructionTimerImage;
    [SerializeField] private UnderBuildingConstruction underBuildingConstruction;

    private void Update()
    {
        if (underBuildingConstruction != null)
        {
            constructionTimerImage.fillAmount = underBuildingConstruction.GetConstructionTimerNormalized();
        }
    }
}
