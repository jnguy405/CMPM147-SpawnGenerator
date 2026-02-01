using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;

public class Spawner : MonoBehaviour {
    [Header("Spawn Object")]
    public GameObject mySphere;
    
    [Header("Spawn Parameters")]
    [Tooltip("Total number of objects to spawn")]
    [Range(1, 500)]
    public int totalObjects = 20;
    
    [Header("Cluster Settings")]
    [Tooltip("Fixed number of clusters (0 = use dynamic distribution)")]
    [Range(0, 20)]
    public int fixedClusterCount = 0;
    
    [Tooltip("Dynamic cluster count range")]
    public Vector2Int dynamicClusterRange = new Vector2Int(3, 6);
    
    [Tooltip("Objects per cluster (if using fixed clusters)")]
    [Range(0, 20)]
    public int objectsPerCluster = 5;
    
    [Tooltip("Dynamic objects per cluster range")]
    public Vector2Int dynamicObjectsPerCluster = new Vector2Int(3, 8);
    
    [Header("Spatial Constraints")]
    [Tooltip("Center of the placement area")]
    public Vector3 placementCenter = Vector3.zero;
    
    [Tooltip("Size of the placement area")]
    public Vector3 placementAreaSize = new Vector3(50, 10, 50);
    
    [Tooltip("Minimum distance between cluster centers")]
    [Range(1f, 50f)]
    public float minClusterDistance = 10f;
    
    [Tooltip("Cluster radius variability (0-1)")]
    [Range(0f, 1f)]
    public float clusterRadiusVariability = 0.3f;
    
    [Tooltip("Base radius for object placement within cluster")]
    [Range(0f, 10f)]
    public float clusterBaseRadius = 5f;
    
    [Header("Ground Detection")]
    [Tooltip("Minimum height above ground")]
    [Range(0f, 10f)]
    public float minHeightAboveGround = 1f;
    
    [Tooltip("Maximum height above ground")]
    [Range(0f, 50f)]
    public float maxHeightAboveGround = 5f;
    
    [Tooltip("Layer mask for ground detection")]
    public LayerMask groundLayer = -1;
    
    [Tooltip("If no ground is found, use this fallback height")]
    [Range(0f, 20f)]
    public float fallbackSpawnHeight = 10f;
    
    [Header("Exclusion Zones")]
    public List<ExclusionZone> exclusionZones = new List<ExclusionZone>();
    
    [Header("Layer Exclusion System")]
    [Tooltip("Toggle to enable layer-based exclusion")]
    public bool useLayerExclusion = true;
    
    [Tooltip("List of all layers with toggle for exclusion")]
    public LayerExclusion[] layerExclusions = new LayerExclusion[32];
    
    [Tooltip("Minimum distance from excluded objects")]
    [Range(0f, 10f)]
    public float exclusionBuffer = 1f;
    
    [Tooltip("Check size for exclusion detection")]
    [Range(0.1f, 5f)]
    public float exclusionCheckRadius = 0.5f;
    
    [Header("Randomization")]
    public int randomSeed = 0;
    public bool useRandomSeed = false;
    
    [Header("Debug Visualization")]
    public bool showDebugGizmos = true;
    public bool showGroundRays = true;
    public bool showLayerExclusionGizmos = false;
    public Color placementAreaColor = new Color(0, 1, 0, 0.2f);
    public Color clusterCenterColor = Color.red;
    public Color exclusionZoneColor = new Color(1, 0, 0, 0.3f);
    public Color layerExclusionColor = new Color(1, 0.5f, 0, 0.4f);
    public Color groundRayColor = Color.blue;
    
    [Header("Object Management")]
    public bool destroyPreviousSpawns = true;
    
    private System.Random random;
    private List<Vector3> clusterCenters = new List<Vector3>();
    private List<List<Vector3>> clusterPositions = new List<List<Vector3>>();
    private List<GameObject> spawnedObjects = new List<GameObject>();
    private int excludedLayerMask = 0;
    
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
        
        public LayerExclusion(string name, int index) {
            layerName = name;
            layerIndex = index;
        }
    }
    
    void Start() {
        InitializeRandom();
        InitializeLayerExclusions();
    }
    
    void OnValidate() {
        InitializeLayerExclusions();
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
        // Populate layer exclusions array with all Unity layers
        if (layerExclusions == null || layerExclusions.Length != 32) {
            layerExclusions = new LayerExclusion[32];
        }
        
        for (int i = 0; i < 32; i++) {
            string layerName = LayerMask.LayerToName(i);
            if (string.IsNullOrEmpty(layerName)) {
                layerName = $"Layer {i} (Unused)";
            }
            
            if (layerExclusions[i] == null) {
                layerExclusions[i] = new LayerExclusion(layerName, i);
            }
            else {
                layerExclusions[i].layerName = layerName;
                layerExclusions[i].layerIndex = i;
            }
        }
        
        // Update excluded layer mask
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
        spawnedObjects.Clear();
        
        if (random == null) InitializeRandom();
        UpdateExcludedLayerMask();
        
        int clusterCount = fixedClusterCount > 0 ? fixedClusterCount : 
                          random.Next(dynamicClusterRange.x, dynamicClusterRange.y + 1);
        
        GenerateClusterCenters(clusterCount);
        
        List<int> objectsPerClusterList = DistributeObjectsToClusters(clusterCount);
        
        for (int i = 0; i < clusterCount; i++) {
            float radius = clusterBaseRadius * (1 + (float)random.NextDouble() * clusterRadiusVariability);
            List<Vector3> positions = GenerateClusterPositions(
                clusterCenters[i], 
                objectsPerClusterList[i], 
                radius
            );
            
            clusterPositions.Add(positions);
            
            foreach (Vector3 position in positions) {
                SpawnObjectAtPosition(position);
            }
        }
        
        Debug.Log($"Spawned {totalObjects} objects in {clusterCount} clusters. Excluded layers mask: {excludedLayerMask}");
    }
    
    public void SpawnSphere() {
        int spawnPointX = UnityEngine.Random.Range(-10, 10);
        int spawnPointZ = UnityEngine.Random.Range(-10, 10);
        
        Vector3 groundPosition = GetGroundAdjustedPosition(new Vector3(spawnPointX, 0, spawnPointZ));
        groundPosition.y += UnityEngine.Random.Range(10, 20);

        SpawnObjectAtPosition(groundPosition);
    }
    
    private void SpawnObjectAtPosition(Vector3 position) {
        if (mySphere == null) {
            Debug.LogError("No spawn object assigned!");
            return;
        }
        
        GameObject spawnedObj = Instantiate(mySphere, position, Quaternion.identity);
        
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
        Vector3 raycastStart = originalPosition + Vector3.up * 100f;
        
        if (showGroundRays) {
            Debug.DrawRay(raycastStart, Vector3.down * 200f, groundRayColor, 2f);
        }
        
        RaycastHit hit;
        if (Physics.Raycast(raycastStart, Vector3.down, out hit, 200f, groundLayer)) {
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
    
    private bool IsPositionValidByLayer(Vector3 position) {
        if (!useLayerExclusion || excludedLayerMask == 0) return true;
        
        // Check if position is on an excluded layer
        Collider[] colliders = Physics.OverlapSphere(position, exclusionCheckRadius, excludedLayerMask);
        
        if (colliders.Length > 0) {
            return false;
        }
        
        // Check buffer distance from excluded objects
        if (exclusionBuffer > 0) {
            colliders = Physics.OverlapSphere(position, exclusionBuffer, excludedLayerMask);
            if (colliders.Length > 0) {
                return false;
            }
        }
        
        return true;
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
                
                foreach (var zone in exclusionZones) {
                    if (IsPointInBox(candidate, zone.center, zone.size)) {
                        validPosition = false;
                        break;
                    }
                }
                
                if (validPosition && useLayerExclusion && excludedLayerMask != 0) {
                    if (!IsPositionValidByLayer(candidate)) {
                        validPosition = false;
                    }
                }
                
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
                    Debug.LogWarning($"Could not find valid position for cluster {i}, placing anyway");
                    validPosition = true;
                }
            }
            while (!validPosition);
            
            candidate = GetGroundAdjustedPosition(candidate);
            clusterCenters.Add(candidate);
        }
    }
    
    private List<int> DistributeObjectsToClusters(int clusterCount) {
        List<int> distribution = new List<int>();
        
        if (fixedClusterCount > 0) {
            for (int i = 0; i < clusterCount; i++) {
                distribution.Add(objectsPerCluster);
            }
        }
        else {
            int remainingObjects = totalObjects;
            
            for (int i = 0; i < clusterCount; i++) {
                if (i == clusterCount - 1) {
                    distribution.Add(remainingObjects);
                }
                else {
                    int min = Mathf.Max(1, dynamicObjectsPerCluster.x);
                    int max = Mathf.Min(dynamicObjectsPerCluster.y, remainingObjects - (clusterCount - i - 1));
                    
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
                
                position.x = Mathf.Clamp(position.x, 
                    placementCenter.x - placementAreaSize.x / 2, 
                    placementCenter.x + placementAreaSize.x / 2);
                position.z = Mathf.Clamp(position.z, 
                    placementCenter.z - placementAreaSize.z / 2, 
                    placementCenter.z + placementAreaSize.z / 2);
                
                if (useLayerExclusion && excludedLayerMask != 0 && !IsPositionValidByLayer(position)) {
                    validPosition = false;
                    if (attempts >= maxAttemptsPerPosition) {
                        Debug.LogWarning($"Could not find valid position in cluster, skipping object");
                        break;
                    }
                    continue;
                }
                
                position = GetGroundAdjustedPosition(position);
                validPosition = true;
                
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
        
        if (showLayerExclusionGizmos && useLayerExclusion) {
            Gizmos.color = layerExclusionColor;
            foreach (Vector3 center in clusterCenters) {
                Gizmos.DrawWireSphere(center, exclusionBuffer);
            }
            
            foreach (List<Vector3> cluster in clusterPositions) {
                foreach (Vector3 pos in cluster) {
                    Gizmos.DrawWireSphere(pos, exclusionCheckRadius);
                }
            }
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