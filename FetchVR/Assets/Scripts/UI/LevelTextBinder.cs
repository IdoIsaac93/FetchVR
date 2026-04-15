using TMPro;
using UnityEngine;

namespace FetchVR.UI
{
    public class LevelTextBinder : MonoBehaviour
    {
        [SerializeField] private TMP_Text targetText;
        [SerializeField] private string prefix = "Level ";

        public void SetLevelText(int level)
        {
            if (targetText == null)
            {
                return;
            }

            targetText.text = $"{prefix}{level}";
        }
    }
}
