/*
  COPYRIGHT 2025 - PROPERTY OF TOBII AB
  -------------------------------------
  2025 TOBII AB - KARLSROVAGEN 2D, DANDERYD 182 53, SWEDEN - All Rights Reserved.

  NOTICE:  All information contained herein is, and remains, the property of Tobii AB and its suppliers, if any.
  The intellectual and technical concepts contained herein are proprietary to Tobii AB and its suppliers and may be
  covered by U.S.and Foreign Patents, patent applications, and are protected by trade secret or copyright law.
  Dissemination of this information or reproduction of this material is strictly forbidden unless prior written
  permission is obtained from Tobii AB.
*/

using UnityEngine;

namespace Tobii
{
    /// <summary>
    /// Allows all GameObjects with IGazeFocasable to receive GazeFocusChanged events.
    /// </summary>
    public class G2OHandling : MonoBehaviour
    {
        private float maxDistance = 100f; // The maximum distance the raycast can travel
        private IGazeFocusable lastObjectGazedAt;

        public void OnGazePoint(Vector2 gazePoint)
        {
            Vector2 screen = new Vector2(gazePoint.x * Screen.width, Screen.height - (gazePoint.y * Screen.height));
            // Create a ray from the camera to the gaze point position
            Ray ray = Camera.main.ScreenPointToRay(screen);

            // Perform the raycast
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, maxDistance))
            {
                var currentGazableObject = hit.transform.GetComponent<IGazeFocusable>();

                if (currentGazableObject != null)
                {
                    if (currentGazableObject != lastObjectGazedAt)
                    {
                        if (lastObjectGazedAt != null)
                            lastObjectGazedAt.GazeFocusChanged(false);
                        currentGazableObject.GazeFocusChanged(true);
                        lastObjectGazedAt = currentGazableObject;
                    }
                }
                else
                {
                    if (lastObjectGazedAt != null)
                        lastObjectGazedAt.GazeFocusChanged(false);
                    lastObjectGazedAt = null;
                }
            }
            else
            {
                if (lastObjectGazedAt != null)
                    lastObjectGazedAt.GazeFocusChanged(false);
                lastObjectGazedAt = null;
            }
        }
    }
}
