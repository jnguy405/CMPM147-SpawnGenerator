using UnityEngine;
using System;
using System.Collections.Generic;

[System.Serializable]
public class SpawnerData
{
    [Header("Spawn Settings")]
    public string spawnerName = "New Spawner";
    public bool isEnabled = true;
    public int spawnOrder = 0;
    
    [Header("Spawn Object")]
    public GameObject spawnPrefab;
    
    [Header("Spawn Parameters")]
    [Tooltip("Total number of objects to spawn")]
    public int totalObjects = 20;
    
    [Header("Cluster Settings")]
    [Tooltip("Fixed number of clusters (0 = use dynamic distribution)")]
    public int fixedClusterCount = 0;
    
    [Tooltip("Dynamic cluster count range")]
    public Vector2Int dynamicClusterRange = new Vector2Int(3, 6);
    
    [Tooltip("Objects per cluster (if using fixed clusters)")]
    public int objectsPerCluster = 5;
    
    [Tooltip("Dynamic objects per cluster range")]
    public Vector2Int dynamicObjectsPerCluster = new Vector2Int(3, 8);
    
    [Header("Spatial Constraints")]
    [Tooltip("Center of the placement area")]
    public Vector3 placementCenter = Vector3.zero;
    
    [Tooltip("Size of the placement area")]
    public Vector3 placementAreaSize = new Vector3(50, 10, 50);
    
    [Tooltip("Minimum distance between cluster centers")]
    public float minClusterDistance = 10f;
    
    [Tooltip("Cluster radius variability (0-1)")]
    [Range(0f, 1f)]
    public float clusterRadiusVariability = 0.3f;
    
    [Tooltip("Base radius for object placement within cluster")]
    public float clusterBaseRadius = 5f;
    
    [Header("Ground Detection")]
    [Tooltip("Minimum height above ground")]
    public float minHeightAboveGround = 1f;
    
    [Tooltip("Maximum height above ground")]
    public float maxHeightAboveGround = 5f;
    
    [Tooltip("Layer mask for ground detection")]
    public LayerMask groundLayer = -1;
    
    [Tooltip("If no ground is found, use this fallback height")]
    public float fallbackSpawnHeight = 10f;
    
    [Header("Exclusion Zones")]
    public List<ExclusionZoneData> exclusionZones = new List<ExclusionZoneData>();
    
    [Header("Randomization")]
    public int randomSeed = 0;
    public bool useRandomSeed = false;
    
    [Header("Object Settings")]
    public bool addRigidbody = true;
    public bool randomizeRotation = false;
    public Vector3 rotationRange = Vector3.zero;
    public Vector3 scaleRangeMin = Vector3.one;
    public Vector3 scaleRangeMax = Vector3.one;
    
    [Header("Performance")]
    public bool poolObjects = false;
    public int poolSize = 50;
    
    // Runtime data (not serialized)
    [System.NonSerialized]
    public List<GameObject> spawnedObjects = new List<GameObject>();
    
    [System.NonSerialized]
    public List<Vector3> clusterCenters = new List<Vector3>();
    
    [System.NonSerialized]
    public List<List<Vector3>> clusterPositions = new List<List<Vector3>>();
}

[System.Serializable]
public class ExclusionZoneData
{
    public string zoneName = "Exclusion Zone";
    public Vector3 center;
    public Vector3 size = Vector3.one * 10f;
    public Color debugColor = new Color(1, 0, 0, 0.3f);
    public bool isActive = true;
}

// Custom PropertyDrawer for SpawnerData
#if UNITY_EDITOR
[UnityEditor.CustomPropertyDrawer(typeof(SpawnerData))]
public class SpawnerDataDrawer : UnityEditor.PropertyDrawer
{
    private Dictionary<string, bool> foldoutStates = new Dictionary<string, bool>();
    
    public override float GetPropertyHeight(UnityEditor.SerializedProperty property, GUIContent label)
    {
        float height = UnityEditor.EditorGUIUtility.singleLineHeight * 1.2f; // Main label
        
        if (IsFoldoutOpen(property.propertyPath))
        {
            // Calculate total height when expanded
            height += UnityEditor.EditorGUIUtility.singleLineHeight * 30; // Basic fields
            height += UnityEditor.EditorGUIUtility.singleLineHeight * 5;  // Spacing between sections
            
            // Add height for exclusion zones array
            var exclusionZonesProp = property.FindPropertyRelative("exclusionZones");
            if (exclusionZonesProp.isExpanded)
            {
                height += UnityEditor.EditorGUIUtility.singleLineHeight * 2; // Array header
                for (int i = 0; i < exclusionZonesProp.arraySize; i++)
                {
                    height += UnityEditor.EditorGUIUtility.singleLineHeight * 4;
                }
            }
            else
            {
                height += UnityEditor.EditorGUIUtility.singleLineHeight;
            }
        }
        
        return height;
    }
    
    public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
    {
        UnityEditor.EditorGUI.BeginProperty(position, label, property);
        
        // Draw foldout
        Rect foldoutRect = new Rect(position.x, position.y, position.width, UnityEditor.EditorGUIUtility.singleLineHeight);
        bool isExpanded = UnityEditor.EditorGUI.Foldout(foldoutRect, IsFoldoutOpen(property.propertyPath), label, true);
        SetFoldoutState(property.propertyPath, isExpanded);
        
        if (isExpanded)
        {
            UnityEditor.EditorGUI.indentLevel++;
            float yOffset = UnityEditor.EditorGUIUtility.singleLineHeight * 1.2f;
            
            // Basic Settings
            DrawPropertyField(ref position, ref yOffset, property, "spawnerName", "Spawner Name");
            DrawPropertyField(ref position, ref yOffset, property, "isEnabled", "Enabled");
            DrawPropertyField(ref position, ref yOffset, property, "spawnOrder", "Spawn Order");
            
            // Spawn Object
            DrawPropertyField(ref position, ref yOffset, property, "spawnPrefab", "Spawn Prefab");
            
            // Spawn Parameters
            UnityEditor.EditorGUI.LabelField(new Rect(position.x, position.y + yOffset, position.width, UnityEditor.EditorGUIUtility.singleLineHeight), "Spawn Parameters", UnityEditor.EditorStyles.boldLabel);
            yOffset += UnityEditor.EditorGUIUtility.singleLineHeight * 1.2f;
            
            DrawPropertyField(ref position, ref yOffset, property, "totalObjects", "Total Objects");
            
            // Cluster Settings
            UnityEditor.EditorGUI.LabelField(new Rect(position.x, position.y + yOffset, position.width, UnityEditor.EditorGUIUtility.singleLineHeight), "Cluster Settings", UnityEditor.EditorStyles.boldLabel);
            yOffset += UnityEditor.EditorGUIUtility.singleLineHeight * 1.2f;
            
            DrawPropertyField(ref position, ref yOffset, property, "fixedClusterCount", "Fixed Cluster Count");
            if (property.FindPropertyRelative("fixedClusterCount").intValue == 0)
            {
                DrawPropertyField(ref position, ref yOffset, property, "dynamicClusterRange", "Dynamic Cluster Range");
                DrawPropertyField(ref position, ref yOffset, property, "dynamicObjectsPerCluster", "Objects Per Cluster Range");
            }
            else
            {
                DrawPropertyField(ref position, ref yOffset, property, "objectsPerCluster", "Objects Per Cluster");
            }
            
            // Spatial Constraints
            UnityEditor.EditorGUI.LabelField(new Rect(position.x, position.y + yOffset, position.width, UnityEditor.EditorGUIUtility.singleLineHeight), "Spatial Constraints", UnityEditor.EditorStyles.boldLabel);
            yOffset += UnityEditor.EditorGUIUtility.singleLineHeight * 1.2f;
            
            DrawPropertyField(ref position, ref yOffset, property, "placementCenter", "Placement Center");
            DrawPropertyField(ref position, ref yOffset, property, "placementAreaSize", "Placement Area Size");
            DrawPropertyField(ref position, ref yOffset, property, "minClusterDistance", "Min Cluster Distance");
            DrawPropertyField(ref position, ref yOffset, property, "clusterRadiusVariability", "Radius Variability");
            DrawPropertyField(ref position, ref yOffset, property, "clusterBaseRadius", "Base Radius");
            
            // Ground Detection
            UnityEditor.EditorGUI.LabelField(new Rect(position.x, position.y + yOffset, position.width, UnityEditor.EditorGUIUtility.singleLineHeight), "Ground Detection", UnityEditor.EditorStyles.boldLabel);
            yOffset += UnityEditor.EditorGUIUtility.singleLineHeight * 1.2f;
            
            DrawPropertyField(ref position, ref yOffset, property, "minHeightAboveGround", "Min Height");
            DrawPropertyField(ref position, ref yOffset, property, "maxHeightAboveGround", "Max Height");
            DrawPropertyField(ref position, ref yOffset, property, "groundLayer", "Ground Layer");
            DrawPropertyField(ref position, ref yOffset, property, "fallbackSpawnHeight", "Fallback Height");
            
            // Exclusion Zones
            var exclusionZonesProp = property.FindPropertyRelative("exclusionZones");
            UnityEditor.EditorGUI.LabelField(new Rect(position.x, position.y + yOffset, position.width, UnityEditor.EditorGUIUtility.singleLineHeight), "Exclusion Zones", UnityEditor.EditorStyles.boldLabel);
            yOffset += UnityEditor.EditorGUIUtility.singleLineHeight;
            
            exclusionZonesProp.isExpanded = UnityEditor.EditorGUI.Foldout(
                new Rect(position.x, position.y + yOffset, position.width, UnityEditor.EditorGUIUtility.singleLineHeight),
                exclusionZonesProp.isExpanded,
                $"Zones ({exclusionZonesProp.arraySize})",
                true
            );
            yOffset += UnityEditor.EditorGUIUtility.singleLineHeight;
            
            if (exclusionZonesProp.isExpanded)
            {
                // Array size control
                Rect arraySizeRect = new Rect(position.x + 20, position.y + yOffset, position.width - 40, UnityEditor.EditorGUIUtility.singleLineHeight);
                exclusionZonesProp.arraySize = UnityEditor.EditorGUI.IntField(arraySizeRect, "Size", exclusionZonesProp.arraySize);
                yOffset += UnityEditor.EditorGUIUtility.singleLineHeight * 1.2f;
                
                // Draw each zone
                for (int i = 0; i < exclusionZonesProp.arraySize; i++)
                {
                    var zoneProp = exclusionZonesProp.GetArrayElementAtIndex(i);
                    
                    Rect zoneRect = new Rect(position.x + 20, position.y + yOffset, position.width - 40, UnityEditor.EditorGUIUtility.singleLineHeight * 4);
                    UnityEditor.EditorGUI.PropertyField(zoneRect, zoneProp, new GUIContent($"Zone {i}"));
                    yOffset += UnityEditor.EditorGUIUtility.singleLineHeight * 4;
                }
            }
            
            // Randomization
            UnityEditor.EditorGUI.LabelField(new Rect(position.x, position.y + yOffset, position.width, UnityEditor.EditorGUIUtility.singleLineHeight), "Randomization", UnityEditor.EditorStyles.boldLabel);
            yOffset += UnityEditor.EditorGUIUtility.singleLineHeight * 1.2f;
            
            DrawPropertyField(ref position, ref yOffset, property, "useRandomSeed", "Use Fixed Seed");
            if (property.FindPropertyRelative("useRandomSeed").boolValue)
            {
                DrawPropertyField(ref position, ref yOffset, property, "randomSeed", "Random Seed");
            }
            
            // Object Settings
            UnityEditor.EditorGUI.LabelField(new Rect(position.x, position.y + yOffset, position.width, UnityEditor.EditorGUIUtility.singleLineHeight), "Object Settings", UnityEditor.EditorStyles.boldLabel);
            yOffset += UnityEditor.EditorGUIUtility.singleLineHeight * 1.2f;
            
            DrawPropertyField(ref position, ref yOffset, property, "addRigidbody", "Add Rigidbody");
            DrawPropertyField(ref position, ref yOffset, property, "randomizeRotation", "Randomize Rotation");
            if (property.FindPropertyRelative("randomizeRotation").boolValue)
            {
                DrawPropertyField(ref position, ref yOffset, property, "rotationRange", "Rotation Range");
            }
            DrawPropertyField(ref position, ref yOffset, property, "scaleRangeMin", "Scale Min");
            DrawPropertyField(ref position, ref yOffset, property, "scaleRangeMax", "Scale Max");
            
            // Performance
            UnityEditor.EditorGUI.LabelField(new Rect(position.x, position.y + yOffset, position.width, UnityEditor.EditorGUIUtility.singleLineHeight), "Performance", UnityEditor.EditorStyles.boldLabel);
            yOffset += UnityEditor.EditorGUIUtility.singleLineHeight * 1.2f;
            
            DrawPropertyField(ref position, ref yOffset, property, "poolObjects", "Use Object Pooling");
            if (property.FindPropertyRelative("poolObjects").boolValue)
            {
                DrawPropertyField(ref position, ref yOffset, property, "poolSize", "Pool Size");
            }
            
            UnityEditor.EditorGUI.indentLevel--;
        }
        
        UnityEditor.EditorGUI.EndProperty();
    }
    
    private void DrawPropertyField(ref Rect position, ref float yOffset, UnityEditor.SerializedProperty parentProperty, string propertyName, string displayName = null)
    {
        var prop = parentProperty.FindPropertyRelative(propertyName);
        if (prop != null)
        {
            Rect rect = new Rect(position.x, position.y + yOffset, position.width, UnityEditor.EditorGUIUtility.singleLineHeight);
            
            // Use custom display name or property name
            string labelText = displayName ?? FormatPropertyName(propertyName);
            UnityEditor.EditorGUI.PropertyField(rect, prop, new GUIContent(labelText));
            yOffset += UnityEditor.EditorGUIUtility.singleLineHeight * 1.2f;
        }
    }
    
    private string FormatPropertyName(string propertyName)
    {
        // Simple formatting: add spaces before capital letters
        System.Text.StringBuilder sb = new System.Text.StringBuilder();
        
        for (int i = 0; i < propertyName.Length; i++)
        {
            if (i > 0 && char.IsUpper(propertyName[i]) && !char.IsUpper(propertyName[i - 1]))
            {
                sb.Append(' ');
            }
            sb.Append(propertyName[i]);
        }
        
        return sb.ToString();
    }
    
    private bool IsFoldoutOpen(string propertyPath)
    {
        if (foldoutStates.ContainsKey(propertyPath))
            return foldoutStates[propertyPath];
        return false;
    }
    
    private void SetFoldoutState(string propertyPath, bool isOpen)
    {
        if (foldoutStates.ContainsKey(propertyPath))
            foldoutStates[propertyPath] = isOpen;
        else
            foldoutStates.Add(propertyPath, isOpen);
    }
}

// Custom PropertyDrawer for ExclusionZoneData
[UnityEditor.CustomPropertyDrawer(typeof(ExclusionZoneData))]
public class ExclusionZoneDataDrawer : UnityEditor.PropertyDrawer
{
    public override float GetPropertyHeight(UnityEditor.SerializedProperty property, GUIContent label)
    {
        return UnityEditor.EditorGUIUtility.singleLineHeight * 4;
    }
    
    public override void OnGUI(Rect position, UnityEditor.SerializedProperty property, GUIContent label)
    {
        UnityEditor.EditorGUI.BeginProperty(position, label, property);
        
        float yOffset = 0;
        float lineHeight = UnityEditor.EditorGUIUtility.singleLineHeight;
        
        // Zone Name
        var nameProp = property.FindPropertyRelative("zoneName");
        UnityEditor.EditorGUI.PropertyField(
            new Rect(position.x, position.y + yOffset, position.width, lineHeight),
            nameProp,
            new GUIContent("Zone Name")
        );
        yOffset += lineHeight * 1.2f;
        
        // Is Active
        var activeProp = property.FindPropertyRelative("isActive");
        UnityEditor.EditorGUI.PropertyField(
            new Rect(position.x, position.y + yOffset, position.width, lineHeight),
            activeProp,
            new GUIContent("Active")
        );
        yOffset += lineHeight * 1.2f;
        
        // Center
        var centerProp = property.FindPropertyRelative("center");
        UnityEditor.EditorGUI.PropertyField(
            new Rect(position.x, position.y + yOffset, position.width, lineHeight),
            centerProp,
            new GUIContent("Center")
        );
        yOffset += lineHeight * 1.2f;
        
        // Size
        var sizeProp = property.FindPropertyRelative("size");
        UnityEditor.EditorGUI.PropertyField(
            new Rect(position.x, position.y + yOffset, position.width, lineHeight),
            sizeProp,
            new GUIContent("Size")
        );
        
        UnityEditor.EditorGUI.EndProperty();
    }
}
#endif