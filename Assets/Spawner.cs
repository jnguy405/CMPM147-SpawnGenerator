using UnityEngine;
using System;
using System.Collections.Generic;

public class Spawner : MonoBehaviour {
    [Header("Spawn Object")]
    public GameObject myObject;
    
    [Header("Spawn Parameters")]
    [Tooltip("Total number of objects to spawn")]
    public int totalObjects = 20;
    
    [Header("Cluster Settings")]
    [Tooltip("Fixed number of clusters (0 = use dynamic distribution)")]
    [Range(0, 20)]
    public int fixedClusterCount = 0;

    [Tooltip("Objects per cluster (if using fixed clusters)")]
    [Range(0, 20)]
    public int objectsPerCluster = 5;
    
    [Tooltip("Cluster count range (randomized within a range)")]
    public Vector2Int ClusterRange = new Vector2Int(3, 6);
    
    [Tooltip("Objects per cluster (randomized within a range)")]
    public Vector2Int ObjectsPerClusterRange = new Vector2Int(3, 8);
    
    [Header("Spatial Constraints")]
    [Tooltip("Center of the placement area (Map)")]
    public Vector3 placementCenter = Vector3.zero;
    
    [Tooltip("Size of the placement area")]
    public Vector3 placementAreaSize = new Vector3(50, 10, 50);
    
    [Tooltip("Minimum distance between each cluster (from centers)")]
    [Range(1f, 50f)]
    public float minClusterDistance = 10f;
    
    [Tooltip("Cluster radius variability (0-1)")]
    [Range(0f, 1f)]
    public float clusterRadiusVariability = 0.3f;
    
    [Tooltip("Base radius between object placement within cluster")]
    [Range(0f, 10f)]
    public float clusterBaseRadius = 5f;
    
    [Header("Ground Detection")]
    [Tooltip("Minimum height above ground. Objects will spawn at least this far above the ground.")]
    [Range(0.1f, 10f)]
    public float minHeightAboveGround = 1f;
    
    [Tooltip("Maximum height above ground. Objects will spawn no more than this far above the ground. Must be >= Min Height.")]
    [Range(0.1f, 50f)]
    public float maxHeightAboveGround = 5f;
    
    [Tooltip("Layer mask for ground detection. Only objects on these layers are considered valid ground.")]
    public LayerMask groundLayer = -1;
    
    [Tooltip("If no ground is found, objects are spawned at this fallback height. If disabled (layerMask is nothing), objects spawn at this height regardless.")]
    [Range(0f, 20f)]
    public float fallbackSpawnHeight = 10f;
    
    [Header("Exclusion Zones")]
    public List<ExclusionZone> exclusionZones = new List<ExclusionZone>();
    
    [Header("Layer Exclusion System")]
    [Tooltip("Toggle to enable layer-based exclusion")]
    public bool useLayerExclusion = true;
    
    [Tooltip("List of all layers with toggle for exclusion")]
    public LayerExclusion[] layerExclusions = new LayerExclusion[32];
    
    [Header("Randomization")]
    public int randomSeed = 0;
    public bool useRandomSeed = false;
    
    [Header("Debug Visualization")]
    public bool showDebugGizmos = true;
    public bool showGroundRays = true;
    public Color placementAreaColor = new Color(0, 1, 0, 0.2f);
    public Color clusterCenterColor = Color.red;
    public Color exclusionZoneColor = new Color(1, 0, 0, 0.3f);
    public Color groundRayColor = Color.blue;
    
    [Header("Object Management")]
    public bool destroyPreviousSpawns = true;
    
    private System.Random random;
    private List<Vector3> clusterCenters = new List<Vector3>();
    private List<List<Vector3>> clusterPositions = new List<List<Vector3>>();
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private int excludedLayerMask = 0;
    private bool areaConstraintViolated = false;
    
    [System.Serializable]
    public class ExclusionZone {
        public Vector3 center;
        public Vector3 size;
        public string zoneName = "Exclusion Zone";
    }
    
    [System.Serializable]
    public class LayerExclusion {
        public string layerName;
        public bool excludeFromSpawn = false;
        public int layerIndex;
    }
    
    void Start() {
        InitializeRandom();
        InitializeLayerExclusions();
    }
    
    void OnValidate() {
        InitializeLayerExclusions();
        ValidateHeightParameters();
        ValidateClusterParameters();
    }
    
    private void ValidateHeightParameters() {
        // Ensure min height doesn't exceed max height
        if (minHeightAboveGround > maxHeightAboveGround) {
            maxHeightAboveGround = minHeightAboveGround;
        }
        
        // Ensure max height doesn't go below min height
        if (maxHeightAboveGround < minHeightAboveGround) {
            minHeightAboveGround = maxHeightAboveGround;
        }
    }
    
    private void ValidateClusterParameters() {
        // If using fixed clusters, ensure totalObjects is at least as many as fixedClusterCount * objectsPerCluster
        if (fixedClusterCount > 0 && objectsPerCluster > 0) {
            int requiredObjects = fixedClusterCount * objectsPerCluster;
            if (totalObjects < requiredObjects) {
                totalObjects = requiredObjects;
            }
        }
    }
    
    private void InitializeRandom() {
        if (useRandomSeed) {
            random = new System.Random(randomSeed);
        }
        else {
            random = new System.Random(Guid.NewGuid().GetHashCode());
        }
    }
    
    private void InitializeLayerExclusions() {
        if (layerExclusions == null || layerExclusions.Length != 32) {
            layerExclusions = new LayerExclusion[32];
        }
        
        for (int i = 0; i < 32; i++) {
            string layerName = LayerMask.LayerToName(i);
            if (string.IsNullOrEmpty(layerName)) {
                layerName = $"Layer {i} (Unused)";
            }
            
            if (layerExclusions[i] == null) {
                layerExclusions[i] = new LayerExclusion();
            }
            
            layerExclusions[i].layerName = layerName;
            layerExclusions[i].layerIndex = i;
        }
        
        UpdateExcludedLayerMask();
    }
    
    private void UpdateExcludedLayerMask() {
        excludedLayerMask = 0;
        
        if (!useLayerExclusion || layerExclusions == null) return;
        
        foreach (var layerExclusion in layerExclusions) {
            if (layerExclusion != null && layerExclusion.excludeFromSpawn) {
                excludedLayerMask |= 1 << layerExclusion.layerIndex;
            }
        }
    }
    
    public void SpawnClusteredObjects() {
        ClearPreviousSpawns();
        clusterCenters.Clear();
        clusterPositions.Clear();
        areaConstraintViolated = false;
        
        if (random == null) InitializeRandom();
        UpdateExcludedLayerMask();
        ValidateHeightParameters();
        ValidateClusterParameters();
        
        int clusterCount = fixedClusterCount > 0 ? fixedClusterCount : 
                          random.Next(ClusterRange.x, ClusterRange.y + 1);
        
        // Calculate actual cluster count including remainder cluster if needed
        int actualClusterCount = clusterCount;
        if (fixedClusterCount > 0 && (totalObjects % objectsPerCluster) != 0) {
            actualClusterCount++; // Add extra cluster for remainder objects
        }
        
        GenerateClusterCenters(actualClusterCount);
        
        // Warn if placement area is too small
        if (areaConstraintViolated) {
            Debug.LogWarning($"Placement area is too small for {actualClusterCount} clusters with minimum distance {minClusterDistance}. " +
                           $"Consider: (1) Increasing Placement Area Size, (2) Decreasing Cluster Base Radius, " +
                           $"(3) Reducing Cluster Count, or (4) Decreasing Min Cluster Distance.");
        }
        
        List<int> objectsPerClusterList = DistributeObjectsToClusters(clusterCount);
        int totalSpawned = 0;
        
        for (int i = 0; i < objectsPerClusterList.Count; i++) {
            float radius = clusterBaseRadius * (1 + (float)random.NextDouble() * clusterRadiusVariability);
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
        
        Debug.Log($"Spawned {totalSpawned} objects in {objectsPerClusterList.Count} clusters.");
    }
    
    public void SpawnSphere() {
        int spawnPointX = UnityEngine.Random.Range(-10, 10);
        int spawnPointZ = UnityEngine.Random.Range(-10, 10);
        
        Vector3 groundPosition = GetGroundAdjustedPosition(new Vector3(spawnPointX, 0, spawnPointZ));
        groundPosition.y += UnityEngine.Random.Range(10, 20);
        SpawnObjectAtPosition(groundPosition);
    }
    
    private void SpawnObjectAtPosition(Vector3 position) {
        if (myObject == null) {
            Debug.LogError("No spawn object assigned!");
            return;
        }
        
        GameObject spawnedObj = Instantiate(myObject, position, Quaternion.identity);
        
        if (spawnedObj.GetComponent<Rigidbody>() == null) {
            Rigidbody rb = spawnedObj.AddComponent<Rigidbody>();
            rb.useGravity = true;
        }
        
        spawnedObjects.Add(spawnedObj);
    }
    
    public void ClearPreviousSpawns() {
        if (!destroyPreviousSpawns) return;
        
        foreach (GameObject obj in spawnedObjects) {
            if (obj != null) {
                Destroy(obj);
            }
        }
        spawnedObjects.Clear();
    }
    
    private Vector3 GetGroundAdjustedPosition(Vector3 originalPosition) {
        bool hitExcluded;
        return GetGroundAdjustedPosition(originalPosition, out hitExcluded);
    }
    
    private Vector3 GetGroundAdjustedPosition(Vector3 originalPosition, out bool hitExcludedLayer) {
        Vector3 raycastStart = originalPosition + Vector3.up * 100f;
        hitExcludedLayer = false;
        
        if (showGroundRays) {
            Debug.DrawRay(raycastStart, Vector3.down * 200f, groundRayColor, 2f);
        }
        
        RaycastHit hit;
        if (Physics.Raycast(raycastStart, Vector3.down, out hit, 200f, ~0)) {
            int hitLayerMask = 1 << hit.collider.gameObject.layer;
            
            // Check if hit excluded layer
            if (useLayerExclusion && (hitLayerMask & excludedLayerMask) != 0) {
                hitExcludedLayer = true;
                return originalPosition;
            }
            
            // Check if hit allowed ground layer
            if ((hitLayerMask & groundLayer) == 0) {
                hitExcludedLayer = true;
                return originalPosition;
            }
            
            // Valid ground hit
            float randomHeight = (float)random.NextDouble();
            float heightAboveGround = minHeightAboveGround + 
                                    randomHeight * (maxHeightAboveGround - minHeightAboveGround);
            
            Vector3 adjustedPosition = hit.point + Vector3.up * heightAboveGround;
            adjustedPosition.x = originalPosition.x;
            adjustedPosition.z = originalPosition.z;
            
            if (showGroundRays) {
                Debug.DrawLine(hit.point, adjustedPosition, Color.green, 2f);
            }
            
            return adjustedPosition;
        }
        else {
            Debug.LogWarning($"No ground detected at position {originalPosition}. Using fallback height.");
            
            Vector3 fallbackPosition = originalPosition;
            fallbackPosition.y = fallbackSpawnHeight;
            
            return fallbackPosition;
        }
    }
    
    private void GenerateClusterCenters(int clusterCount) {
        int maxAttempts = 100;
        
        for (int i = 0; i < clusterCount; i++) {
            Vector3 candidate;
            bool validPosition;
            int attempts = 0;
            
            do {
                validPosition = true;
                attempts++;
                
                float x = (float)(random.NextDouble() * 2 - 1) * placementAreaSize.x / 2 + placementCenter.x;
                float y = placementCenter.y;
                float z = (float)(random.NextDouble() * 2 - 1) * placementAreaSize.z / 2 + placementCenter.z;
                candidate = new Vector3(x, y, z);
                
                // Check exclusion zones
                foreach (var zone in exclusionZones) {
                    if (IsPointInBox(candidate, zone.center, zone.size)) {
                        validPosition = false;
                        break;
                    }
                }
                
                // Check distance from other clusters
                if (validPosition && clusterCenters.Count > 0) {
                    foreach (Vector3 existingCenter in clusterCenters) {
                        float distance = Vector2.Distance(
                            new Vector2(candidate.x, candidate.z), 
                            new Vector2(existingCenter.x, existingCenter.z));
                            
                        if (distance < minClusterDistance) {
                            validPosition = false;
                            break;
                        }
                    }
                }
                
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
    
    private List<int> DistributeObjectsToClusters(int clusterCount) {
        List<int> distribution = new List<int>();
        
        if (fixedClusterCount > 0) {
            // Fixed cluster count with fixed objects per cluster
            int objectsAllocated = clusterCount * objectsPerCluster;
            int remainingObjects = totalObjects - objectsAllocated;
            
            for (int i = 0; i < clusterCount; i++) {
                distribution.Add(objectsPerCluster);
            }
            
            // Handle remainder: spawn leftover objects in an additional cluster
            if (remainingObjects > 0) {
                distribution.Add(remainingObjects);
            }
        }
        else {
            // Dynamic cluster distribution
            int remainingObjects = totalObjects;
            
            for (int i = 0; i < clusterCount; i++) {
                if (i == clusterCount - 1) {
                    distribution.Add(remainingObjects);
                }
                else {
                    int min = Mathf.Max(1, ObjectsPerClusterRange.x);
                    int max = Mathf.Min(ObjectsPerClusterRange.y, remainingObjects - (clusterCount - i - 1));
                    
                    int clusterObjects = random.Next(min, max + 1);
                    distribution.Add(clusterObjects);
                    remainingObjects -= clusterObjects;
                }
            }
        }
        
        return distribution;
    }
    
    private List<Vector3> GenerateClusterPositions(Vector3 center, int count, float radius) {
        List<Vector3> positions = new List<Vector3>();
        int maxAttemptsPerPosition = 20;
        
        for (int i = 0; i < count; i++) {
            Vector3 position;
            bool validPosition;
            int attempts = 0;
            
            do {
                attempts++;
                validPosition = true;
                
                Vector2 randomCircle = UnityEngine.Random.insideUnitCircle * radius;
                Vector3 randomOffset = new Vector3(randomCircle.x, 0, randomCircle.y);
                position = center + randomOffset;
                
                // Clamp to placement area
                position.x = Mathf.Clamp(position.x, 
                    placementCenter.x - placementAreaSize.x / 2, 
                    placementCenter.x + placementAreaSize.x / 2);
                position.z = Mathf.Clamp(position.z, 
                    placementCenter.z - placementAreaSize.z / 2, 
                    placementCenter.z + placementAreaSize.z / 2);
                
                // Get ground position with exclusion check
                bool hitExcluded;
                Vector3 groundPos = GetGroundAdjustedPosition(position, out hitExcluded);
                
                if (hitExcluded) {
                    validPosition = false;
                    if (attempts >= maxAttemptsPerPosition) {
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
    
    private bool IsPointInBox(Vector3 point, Vector3 boxCenter, Vector3 boxSize) {
        return Mathf.Abs(point.x - boxCenter.x) <= boxSize.x / 2 &&
               Mathf.Abs(point.y - boxCenter.y) <= boxSize.y / 2 &&
               Mathf.Abs(point.z - boxCenter.z) <= boxSize.z / 2;
    }
    
    void OnDrawGizmosSelected() {
        if (!showDebugGizmos) return;
        
        Gizmos.color = placementAreaColor;
        Gizmos.DrawWireCube(placementCenter, placementAreaSize);
        
        Gizmos.color = exclusionZoneColor;
        foreach (var zone in exclusionZones) {
            Gizmos.DrawWireCube(zone.center, zone.size);
        }
        
        Gizmos.color = clusterCenterColor;
        foreach (Vector3 center in clusterCenters) {
            Gizmos.DrawSphere(center, 0.5f);
            Gizmos.DrawWireSphere(center, minClusterDistance);
        }
        
        foreach (List<Vector3> cluster in clusterPositions) {
            foreach (Vector3 pos in cluster) {
                Gizmos.DrawWireSphere(pos, 0.3f);
            }
        }
    }
}