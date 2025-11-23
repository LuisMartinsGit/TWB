using UnityEngine;

namespace TheWaningBorder.GameCamera
{
    public static class GameCamera{

        public static void Ensure() => EnsureGameCameraOnce();

        private static void EnsureGameCameraOnce()
        {
            var cam = Camera.main;
            if (cam != null && !cam.enabled) cam.enabled = true;

            if (cam == null)
            {
                var camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                cam = camGO.AddComponent<Camera>();
                cam.enabled = true;
                cam.clearFlags = CameraClearFlags.Skybox;

                // --- Top-down perspective settings ---
                cam.orthographic = false;
                cam.fieldOfView = 40f;     // tighter FOV = less distortion, more RTS clarity
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 5000f;

                camGO.AddComponent<AudioListener>();

                // Higher and steeper for classic RTS readability
                camGO.transform.position = new Vector3(0f, 55f, -10f);
                camGO.transform.rotation = Quaternion.Euler(55f, 0f, 0f);
            }
            else
            {
                if (!cam.CompareTag("MainCamera")) cam.tag = "MainCamera";
                if (cam.GetComponent<AudioListener>() == null) cam.gameObject.AddComponent<AudioListener>();
            }

            var listeners = Object.FindObjectsOfType<AudioListener>();
            foreach (var l in listeners)
                if (l != null && l.gameObject != cam.gameObject)
                    Object.Destroy(l);

            // Check if a rig already exists in the scene
            var existingRig = Object.FindObjectOfType<RTSCameraRig>();
            if (existingRig == null)
            {
                // Create a rig parent for the camera
                var rigGO = new GameObject("CameraRig");
                var rig = rigGO.AddComponent<RTSCameraRig>();
                
                // Position the rig at origin
                rigGO.transform.position = new Vector3(0f, 0f, 0f);
                
                // The rig will initialize and parent the camera properly
                rig.mainCamera = cam;
                
                // If FogOfWarManager exists, sync world bounds
                var fow = Object.FindObjectOfType<FogOfWarManager>();
                if (fow != null)
                {
                    rig.worldMin = fow.WorldMin;
                    rig.worldMax = fow.WorldMax;
                }

            }
            else if (existingRig.mainCamera == null)
            {
                // Rig exists but doesn't have camera reference
                existingRig.mainCamera = cam;

            }
        }
    }
}