using UnityEngine;

namespace FetchVR.Dog
{
    /// <summary>
    /// In game mode, keeps the fetch goal aligned to the XR camera on the XZ plane.
    /// In training mode, the goal is positioned by FetchArea and this script disables itself.
    /// </summary>
    public class GoalFollowCamera : MonoBehaviour
    {
        [Tooltip("XR camera or head transform to follow in game mode.")]
        [SerializeField] private Transform xrCamera;

        [Tooltip("World-space Y position used for the goal collider.")]
        [SerializeField] private float groundY = 0f;

        [Tooltip("Optional explicit area reference. If omitted, this script finds the FetchArea using this goal.")]
        [SerializeField] private FetchArea fetchArea;

        private void Awake()
        {
            if (fetchArea == null)
            {
                FetchArea[] areas = FindObjectsByType<FetchArea>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
                foreach (FetchArea area in areas)
                {
                    if (area != null && area.goal == gameObject)
                    {
                        fetchArea = area;
                        break;
                    }
                }
            }

            if (fetchArea != null && !fetchArea.isGameMode)
            {
                enabled = false;
            }
        }

        private void LateUpdate()
        {
            if (xrCamera == null)
            {
                return;
            }

            transform.position = new Vector3(
                xrCamera.position.x,
                groundY,
                xrCamera.position.z
            );
        }
    }
}
