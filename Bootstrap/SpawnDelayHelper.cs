// SpawnDelayHelper.cs
// Waits for terrain before spawning players
// Location: Assets/Scripts/Bootstrap/SpawnDelayHelper.cs

using System.Collections;
using UnityEngine;
using TheWaningBorder.World.Terrain;

namespace TheWaningBorder.Bootstrap
{
    public class SpawnDelayHelper : MonoBehaviour
    {
        public IEnumerator WaitForTerrainAndSpawn()
        {
            // Wait until terrain exists and has valid data
            float timeout = 5f;
            float elapsed = 0f;
            
            while (elapsed < timeout)
            {
                if (TerrainUtility.IsReady())
                {
                    Debug.Log("[SpawnDelayHelper] Terrain ready, spawning players...");
                    PlayerSpawnSystem.SpawnAllFactions();
                    Destroy(gameObject);
                    yield break;
                }
                
                elapsed += Time.deltaTime;
                yield return null;
            }
            
            Debug.LogError("[SpawnDelayHelper] Timeout waiting for terrain! Spawning anyway...");
            PlayerSpawnSystem.SpawnAllFactions();
            Destroy(gameObject);
        }
    }
}