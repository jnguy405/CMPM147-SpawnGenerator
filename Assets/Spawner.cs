/* ------------------------------------------------------------
- Jenalee Nguyen - 2026
- jnguy405@ucsc.edu
---------------------------------------------------------------   
   Function Summary (grouped by TAG)

   MAP:
   - ValidateHeightParameters()
   - GetGroundAdjustedPosition(Vector3)
   - GetGroundAdjustedPosition(Vector3, out bool)

   CLUSTER:
   - ValidateClusterParameters()
   - GenerateClusterCenters(int)
   - DistributeObjectsToClusters(int)
   - GenerateClusterPositions(Vector3, int, float)

   OBJECT:
   - SpawnClusteredObjects()
   - SpawnObject()
   - SpawnObjectAtPosition(Vector3)
   - ClearPreviousSpawns()

   DEBUG:
   - OnDrawGizmosSelected()
   - UpdateExcludedLayerMask()

   UTIL / MISC:
   - InitializeRandom()
   - InitializeLayerExclusions()
   - IsPointInBox(Vector3, Vector3, Vector3)
 ------------------------------------------------------------- */

using UnityEngine;
using System;
using System.Collections.Generic;

public class Spawner : MonoBehaviour {

    // Dropdown to display spawner configuration in inspector
    [Header("Spawner Config")]
    public SpawnerConfig config = new SpawnerConfig();

    // Initialize variables
    private System.Random random;
    private List<Vector3> clusterCenters = new List<Vector3>();                 // List of Vector3 to hold the centers of clusters
    private List<List<Vector3>> clusterPositions = new List<List<Vector3>>();   // List of List of Vector3 to hold positions within each cluster
    private List<GameObject> spawnedObjects = new List<GameObject>();           // List of GameObject to keep track of spawned objects
    private int excludedLayerMask = 0;                                          // Integer bitmask for excluded layers
    private bool areaConstraintViolated = false;                                // Boolean flag for area constraint violations

    // Start the Unity lifecycle
    // Calls random and layer exclusion initializers for setup
    void Start() {
        InitializeRandom();
        InitializeLayerExclusions();
    }

    // Validate configuration on changes in the inspector
    void OnValidate() {
        InitializeLayerExclusions();
        ValidateHeightParameters();
        ValidateClusterParameters();
    }

    // MAP: Checks if height parameters are valid and adjusts them if necessary
    private void ValidateHeightParameters() {
        if (config.minHeightAboveGround > config.maxHeightAboveGround) {
            config.maxHeightAboveGround = config.minHeightAboveGround;
        }

        if (config.maxHeightAboveGround < config.minHeightAboveGround) {
            config.minHeightAboveGround = config.maxHeightAboveGround;
        }
    }

    // CLUSTER: Checks cluster parameters and adjusts total objects if necessary
    private void ValidateClusterParameters() {
        if (config.fixedClusterCount > 0 && config.objectsPerCluster > 0) {
            int requiredObjects = config.fixedClusterCount * config.objectsPerCluster;
            if (config.totalObjects < requiredObjects) {
                config.totalObjects = requiredObjects;
            }
        }
    }

    // Initializes the random seed based on configuration
    private void InitializeRandom() {
        // Set up random number generator
        if (config.useRandomSeed) {
            random = new System.Random(config.randomSeed);
        }
        else {
            // Use a time-based seed for randomness
            random = new System.Random(Guid.NewGuid().GetHashCode());
        }
    }

    // Initializes layer exclusions based on configuration
    private void InitializeLayerExclusions() {
        // Initializes layerExclusions array if null or incorrect size
        if (config.layerExclusions == null || config.layerExclusions.Length != 32) {
            config.layerExclusions = new LayerExclusion[32];
        }

        // Populate layer names and indices
        for (int i = 0; i < 32; i++) {
            string layerName = LayerMask.LayerToName(i);
            if (string.IsNullOrEmpty(layerName)) {
                layerName = $"Layer {i} (Unused)";
            }
            // Create new LayerExclusion if null
            if (config.layerExclusions[i] == null) {
                config.layerExclusions[i] = new LayerExclusion();
            }
            // Set layer name and index
            config.layerExclusions[i].layerName = layerName;
            config.layerExclusions[i].layerIndex = i;
        }

        UpdateExcludedLayerMask();
    }

    // Updates the excluded layer mask based on current exclusions
    private void UpdateExcludedLayerMask() {
        excludedLayerMask = 0;

        if (!config.useLayerExclusion || config.layerExclusions == null) return;

        // Build the excluded layer mask - each excluded layer sets its bit in the mask
        foreach (var layerExclusion in config.layerExclusions) {
            if (layerExclusion != null && layerExclusion.excludeFromSpawn) {
                excludedLayerMask |= 1 << layerExclusion.layerIndex;
            }
        }
    }

    // CLUSTER: Main method to spawn clustered objects based on configuration
    public void SpawnClusteredObjects() {
        // Reset previous state
        ClearPreviousSpawns();
        clusterCenters.Clear();
        clusterPositions.Clear();
        areaConstraintViolated = false;

        // Initialize random if not already done
        if (random == null) InitializeRandom();
        UpdateExcludedLayerMask();
        ValidateHeightParameters();
        ValidateClusterParameters();

        // Determine number of clusters from configuration
        int clusterCount = config.fixedClusterCount > 0 ? config.fixedClusterCount :
                          random.Next(config.ClusterRange.x, config.ClusterRange.y + 1);

        // Adjust cluster count if there are leftover objects (remainder)
        int actualClusterCount = clusterCount;
        if (config.fixedClusterCount > 0 && (config.totalObjects % config.objectsPerCluster) != 0) {
            actualClusterCount++; // Add extra cluster for remainder objects
        }

        GenerateClusterCenters(actualClusterCount);

        // DEBUG: Warn if area constraints are violated
        if (areaConstraintViolated) {
            Debug.LogWarning($"Placement area is too small for {actualClusterCount} clusters with minimum distance {config.minClusterDistance}. " +
                           $"Consider: (1) Increasing Placement Area Size, (2) Decreasing Cluster Base Radius, " +
                           $"(3) Reducing Cluster Count, or (4) Decreasing Min Cluster Distance.");
        }

        // Distribute objects to clusters and spawn them - given ranged or fixed counts
        List<int> objectsPerClusterList = DistributeObjectsToClusters(clusterCount);
        int totalSpawned = 0;

        // Spawn objects for each cluster - gets positions and instantiates objects
        // Positions are stored and used for gizmo visualization
        for (int i = 0; i < objectsPerClusterList.Count; i++) {
            float radius = config.clusterBaseRadius * (1 + (float)random.NextDouble() * config.clusterRadiusVariability);
            List<Vector3> positions = GenerateClusterPositions(
                clusterCenters[i],
                objectsPerClusterList[i],
                radius
            );

            clusterPositions.Add(positions);

            foreach (Vector3 position in positions) {
                SpawnObjectAtPosition(position);
                totalSpawned++;
            }
        }
        // DEBUG: Displays total spawned and cluster count
        Debug.Log($"Spawned {totalSpawned} objects in {objectsPerClusterList.Count} clusters.");
    }

    // OBJECT: Spawns a single object at a random position within a defined range
    public void SpawnObject() {
        int spawnPointX = UnityEngine.Random.Range(-10, 10);
        int spawnPointZ = UnityEngine.Random.Range(-10, 10);

        Vector3 groundPosition = GetGroundAdjustedPosition(new Vector3(spawnPointX, 0, spawnPointZ));
        groundPosition.y += UnityEngine.Random.Range(10, 20);
        SpawnObjectAtPosition(groundPosition);
    }

    // OBJECT: Spawns the configured object at the specified position - adds Rigidbody if missing
    private void SpawnObjectAtPosition(Vector3 position) {
        if (config.myObject == null) {
            Debug.LogError("No spawn object assigned!");
            return;
        }

        GameObject spawnedObj = Instantiate(config.myObject, position, Quaternion.identity);

        if (spawnedObj.GetComponent<Rigidbody>() == null) {
            Rigidbody rb = spawnedObj.AddComponent<Rigidbody>();
            rb.useGravity = true;
        }

        spawnedObjects.Add(spawnedObj);
    }

    // CLEANUP: Clears previously spawned objects if configured to do so
    public void ClearPreviousSpawns() {
        if (!config.destroyPreviousSpawns) return;

        foreach (GameObject obj in spawnedObjects) {
            if (obj != null) {
                Destroy(obj);
            }
        }
        spawnedObjects.Clear();
    }

    // MAP: Adjusts the given position to align with the ground using raycasting
    private Vector3 GetGroundAdjustedPosition(Vector3 originalPosition) {
        bool hitExcluded;
        return GetGroundAdjustedPosition(originalPosition, out hitExcluded);
    }

    // MAP: Adjusts based on ground raycast, checks for excluded layers
    private Vector3 GetGroundAdjustedPosition(Vector3 originalPosition, out bool hitExcludedLayer) {
        Vector3 raycastStart = originalPosition + Vector3.up * 100f;
        hitExcludedLayer = false;

        if (config.showGroundRays) {
            Debug.DrawRay(raycastStart, Vector3.down * 200f, config.groundRayColor, 2f);
        }

        RaycastHit hit;
        // Checks for ground hit using raycast and returns the adjusted position
        if (Physics.Raycast(raycastStart, Vector3.down, out hit, 200f, ~0)) {
            int hitLayerMask = 1 << hit.collider.gameObject.layer;

            if (config.useLayerExclusion && (hitLayerMask & excludedLayerMask) != 0) {
                hitExcludedLayer = true;
                return originalPosition;
            }

            if ((hitLayerMask & config.groundLayer) == 0) {
                hitExcludedLayer = true;
                return originalPosition;
            }

            float randomHeight = (float)random.NextDouble();
            float heightAboveGround = config.minHeightAboveGround +
                                    randomHeight * (config.maxHeightAboveGround - config.minHeightAboveGround);

            Vector3 adjustedPosition = hit.point + Vector3.up * heightAboveGround;
            adjustedPosition.x = originalPosition.x;
            adjustedPosition.z = originalPosition.z;

            if (config.showGroundRays) {
                Debug.DrawLine(hit.point, adjustedPosition, Color.green, 2f);
            }

            return adjustedPosition;
        }
        else {
            // DEBUG: Warn if no ground detected and use fallback height
            Debug.LogWarning($"No ground detected at position {originalPosition}. Using fallback height.");

            Vector3 fallbackPosition = originalPosition;
            fallbackPosition.y = config.fallbackSpawnHeight;

            return fallbackPosition;
        }
    }

    // CLUSTER: Generates cluster centers (midpoints) while respecting exclusion zones and minimum distances
    private void GenerateClusterCenters(int clusterCount) {
        int maxAttempts = 100;

        for (int i = 0; i < clusterCount; i++) {
            Vector3 candidate;
            bool validPosition;
            int attempts = 0;

            // Try to find a valid cluster center position
            do {
                validPosition = true;
                attempts++;

                float x = (float)(random.NextDouble() * 2 - 1) * config.placementAreaSize.x / 2 + config.placementCenter.x;
                float y = config.placementCenter.y;
                float z = (float)(random.NextDouble() * 2 - 1) * config.placementAreaSize.z / 2 + config.placementCenter.z;
                candidate = new Vector3(x, y, z);

                // Check against exclusion zones
                foreach (var zone in config.exclusionZones) {
                    if (IsPointInBox(candidate, zone.center, zone.size)) {
                        validPosition = false;
                        break;
                    }
                }

                // Check minimum distance from existing cluster centers
                if (validPosition && clusterCenters.Count > 0) {
                    foreach (Vector3 existingCenter in clusterCenters) {
                        float distance = Vector2.Distance(
                            new Vector2(candidate.x, candidate.z),
                            new Vector2(existingCenter.x, existingCenter.z));

                        if (distance < config.minClusterDistance) {
                            validPosition = false;
                            break;
                        }
                    }
                }

                // If too many attempts, accept the position and flag constraint violation
                if (attempts > maxAttempts) {
                    areaConstraintViolated = true;
                    validPosition = true;
                }
            }
            while (!validPosition);

            bool hitExcluded;
            candidate = GetGroundAdjustedPosition(candidate, out hitExcluded);

            if (hitExcluded && attempts < maxAttempts) {
                i--; // Retry this cluster
                continue;
            }

            clusterCenters.Add(candidate);
        }
    }

    // CLUSTER: Distributes total objects into clusters based on fixed or ranged configuration
    private List<int> DistributeObjectsToClusters(int clusterCount) {
        List<int> distribution = new List<int>();

        if (config.fixedClusterCount > 0) {
            int objectsAllocated = clusterCount * config.objectsPerCluster;

            if (objectsAllocated > config.totalObjects) {
                config.totalObjects = objectsAllocated;
                // DEBUG: Warn if totalObjects was increased based on fixed cluster parameters
                Debug.LogWarning($"totalObjects was increased to {config.totalObjects} to match fixed clusters ({clusterCount} * {config.objectsPerCluster}).");
            }

            int remainingObjects = config.totalObjects - objectsAllocated;

            for (int i = 0; i < clusterCount; i++) {
                distribution.Add(config.objectsPerCluster);
            }

            if (remainingObjects > 0) {
                distribution.Add(remainingObjects);
            }
        }
        else {
            // Remainder distribution for ranged clusters (leftover objects go to last cluster)
            int remainingObjects = config.totalObjects;

            for (int i = 0; i < clusterCount; i++) {
                if (i == clusterCount - 1) {
                    distribution.Add(remainingObjects);
                }
                else {
                    int min = Mathf.Max(1, config.ObjectsPerClusterRange.x);
                    int max = Mathf.Min(config.ObjectsPerClusterRange.y, remainingObjects - (clusterCount - i - 1));

                    int clusterObjects = random.Next(min, max + 1);
                    distribution.Add(clusterObjects);
                    remainingObjects -= clusterObjects;
                }
            }
        }

        return distribution;
    }

    // CLUSTER: Generates positions within a cluster around a center point (based on center, count, radius)
    private List<Vector3> GenerateClusterPositions(Vector3 center, int count, float radius) {
        List<Vector3> positions = new List<Vector3>();
        int maxAttemptsPerPosition = 20;

        for (int i = 0; i < count; i++) {
            Vector3 position;
            bool validPosition;
            int attempts = 0;

            // Gets a valid position within the cluster radius
            do {
                attempts++;
                validPosition = true;

                Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * radius;
                Vector3 randomOffset = new Vector3(randomCircle.x, 0, randomCircle.y);
                position = center + randomOffset;

                // Generates position within placement area size bounds
                position.x = Mathf.Clamp(position.x,
                    config.placementCenter.x - config.placementAreaSize.x / 2,
                    config.placementCenter.x + config.placementAreaSize.x / 2);
                position.z = Mathf.Clamp(position.z,
                    config.placementCenter.z - config.placementAreaSize.z / 2,
                    config.placementCenter.z + config.placementAreaSize.z / 2);

                bool hitExcluded;
                Vector3 groundPos = GetGroundAdjustedPosition(position, out hitExcluded);

                if (hitExcluded) {
                    validPosition = false;
                    if (attempts >= maxAttemptsPerPosition) {
                        // DEBUG: Warn if unable to find valid position in cluster after max attempts
                        Debug.LogWarning($"Could not find valid position in cluster, skipping object");
                        break;
                    }
                    continue;
                }

                position = groundPos;

            } while (!validPosition && attempts < maxAttemptsPerPosition);

            if (validPosition) {
                positions.Add(position);
            }
        }

        return positions;
    }

    // UTIL: Checks if a point is inside a box defined by center and size
    private bool IsPointInBox(Vector3 point, Vector3 boxCenter, Vector3 boxSize) {
        return Mathf.Abs(point.x - boxCenter.x) <= boxSize.x / 2 &&
               Mathf.Abs(point.y - boxCenter.y) <= boxSize.y / 2 &&
               Mathf.Abs(point.z - boxCenter.z) <= boxSize.z / 2;
    }

    // DEBUG: Visualizes placement area, exclusion zones, cluster centers, and object positions
    void OnDrawGizmosSelected() {
        if (!config.showDebugGizmos) return;

        Gizmos.color = config.placementAreaColor;
        Gizmos.DrawWireCube(config.placementCenter, config.placementAreaSize);

        Gizmos.color = config.exclusionZoneColor;
        foreach (var zone in config.exclusionZones) {
            Gizmos.DrawWireCube(zone.center, zone.size);
        }

        Gizmos.color = config.clusterCenterColor;
        foreach (Vector3 center in clusterCenters) {
            Gizmos.DrawSphere(center, 0.5f);
            Gizmos.DrawWireSphere(center, config.minClusterDistance);
        }

        foreach (List<Vector3> cluster in clusterPositions) {
            foreach (Vector3 pos in cluster) {
                Gizmos.DrawWireSphere(pos, 0.3f);
            }
        }
    }
}
