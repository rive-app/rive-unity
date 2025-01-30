using UnityEngine;

namespace Rive.Components
{

    internal class CameraHelper
    {
        private static Camera[] s_camerasInScene;


        /// <summary>
        /// Gets a valid camera in the scene to submit command buffer commands with.
        /// </summary>
        /// <returns> A valid camera in the scene. </returns>
        public static Camera GetRenderCameraInScene()
        {

            Camera camera = Camera.main;

            if (camera != null)
            {
                return camera;
            }

            int cameraCount = Camera.allCamerasCount;

            if (cameraCount == 0)
            {
                return null;
            }

            if (s_camerasInScene == null || s_camerasInScene.Length < cameraCount)
            {
                s_camerasInScene = new Camera[cameraCount];
            }

            // This only returns enabled cameras
            Camera.GetAllCameras(s_camerasInScene);

            return s_camerasInScene[0];

        }
    }
}
