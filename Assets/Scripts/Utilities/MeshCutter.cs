using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Utility class for mesh cutting operations using EzySlice library.
/// 
/// This class provides a simplified interface for cutting meshes along planes
/// and generating deformed geometry to represent CNC cutting operations.
/// 
/// Dependencies:
/// - EzySlice (MIT License): https://github.com/DavidArayan/ezy-slice
/// - Import via Unity Package Manager or manually add to project
/// 
/// Usage:
/// - Call MeshCutter.SliceMesh() to cut a mesh along a plane
/// - Call MeshCutter.CarveChannel() to carve a path into a mesh
/// - Results include upper/lower hull meshes and cut cross-section
/// </summary>
public static class MeshCutter
{
    // ══════════════════════════════════════════════════════════════════════════
    // CONSTANTS
    // ══════════════════════════════════════════════════════════════════════════

    private const float MIN_SLICE_AREA = 0.0001f;
    private const int MAX_VERTICES_PER_CUT = 10000;

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Simple Slice
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Slices a mesh along a plane, returning two halves.
    /// </summary>
    /// <param name="originalMesh">The mesh to slice.</param>
    /// <param name="planePoint">A point on the slicing plane (world space).</param>
    /// <param name="planeNormal">Normal vector of the slicing plane (world space).</param>
    /// <param name="crossSectionMaterial">Material for the cut surface (optional).</param>
    /// <returns>Result containing upper and lower hull meshes, or null if slice failed.</returns>
    public static SliceResult SliceMesh(
        Mesh originalMesh, 
        Vector3 planePoint, 
        Vector3 planeNormal,
        Material crossSectionMaterial = null)
    {
        if (originalMesh == null)
        {
            Debug.LogWarning("[MeshCutter] Cannot slice null mesh.");
            return null;
        }

        if (planeNormal == Vector3.zero)
        {
            Debug.LogWarning("[MeshCutter] Plane normal cannot be zero.");
            return null;
        }

        planeNormal = planeNormal.normalized;

        // For EzySlice integration, we would call:
        // SlicedHull hull = originalMesh.Slice(planePoint, planeNormal, crossSectionMaterial);
        // if (hull == null) return null;
        // return new SliceResult(hull.CreateUpperHull(), hull.CreateLowerHull());

        // PLACEHOLDER: Simple approximation without EzySlice dependency
        // This creates a basic result for testing - replace with EzySlice calls
        return CreatePlaceholderSlice(originalMesh, planePoint, planeNormal);
    }

    /// <summary>
    /// Slices a GameObject's mesh and creates new GameObjects for the halves.
    /// </summary>
    /// <param name="target">The GameObject to slice (must have MeshFilter).</param>
    /// <param name="planePoint">A point on the slicing plane (world space).</param>
    /// <param name="planeNormal">Normal vector of the slicing plane (world space).</param>
    /// <param name="crossSectionMaterial">Material for the cut surface (optional).</param>
    /// <returns>Array of two GameObjects (upper, lower), or null if slice failed.</returns>
    public static GameObject[] SliceGameObject(
        GameObject target,
        Vector3 planePoint,
        Vector3 planeNormal,
        Material crossSectionMaterial = null)
    {
        if (target == null)
        {
            Debug.LogWarning("[MeshCutter] Cannot slice null GameObject.");
            return null;
        }

        MeshFilter meshFilter = target.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
        {
            Debug.LogWarning("[MeshCutter] Target must have a MeshFilter with a valid mesh.");
            return null;
        }

        // Transform plane to local space
        Vector3 localPoint = target.transform.InverseTransformPoint(planePoint);
        Vector3 localNormal = target.transform.InverseTransformDirection(planeNormal).normalized;

        SliceResult result = SliceMesh(meshFilter.sharedMesh, localPoint, localNormal, crossSectionMaterial);
        if (result == null)
            return null;

        // Create new GameObjects for the halves
        MeshRenderer originalRenderer = target.GetComponent<MeshRenderer>();
        Material[] originalMaterials = originalRenderer != null ? originalRenderer.sharedMaterials : null;

        GameObject upperObj = CreateSliceGameObject(
            target.name + "_Upper",
            result.upperHull,
            target.transform,
            originalMaterials,
            crossSectionMaterial
        );

        GameObject lowerObj = CreateSliceGameObject(
            target.name + "_Lower",
            result.lowerHull,
            target.transform,
            originalMaterials,
            crossSectionMaterial
        );

        return new GameObject[] { upperObj, lowerObj };
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Channel Carving (CNC-style cutting)
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Carves a channel along a path into a mesh (simulates CNC router cutting).
    /// </summary>
    /// <param name="originalMesh">The mesh to carve into.</param>
    /// <param name="pathPoints">World-space points defining the cutting path.</param>
    /// <param name="toolDiameter">Diameter of the cutting tool in meters.</param>
    /// <param name="cutDepth">Depth of the cut in meters.</param>
    /// <param name="transform">Transform of the mesh's GameObject.</param>
    /// <returns>Modified mesh with channel carved, or original mesh if carving failed.</returns>
    public static Mesh CarveChannel(
        Mesh originalMesh,
        List<Vector3> pathPoints,
        float toolDiameter,
        float cutDepth,
        Transform transform)
    {
        if (originalMesh == null || pathPoints == null || pathPoints.Count < 2)
        {
            Debug.LogWarning("[MeshCutter] Invalid parameters for CarveChannel.");
            return originalMesh;
        }

        // Create a working copy of the mesh
        Mesh workingMesh = Object.Instantiate(originalMesh);
        workingMesh.name = originalMesh.name + "_Carved";

        // Get vertices and modify them based on proximity to the path
        Vector3[] vertices = workingMesh.vertices;
        bool anyModified = false;

        float toolRadius = toolDiameter / 2f;

        for (int i = 0; i < vertices.Length; i++)
        {
            // Convert vertex to world space
            Vector3 worldVertex = transform.TransformPoint(vertices[i]);

            // Check distance to path
            float distanceToPath = GetDistanceToPath(worldVertex, pathPoints);

            if (distanceToPath < toolRadius)
            {
                // Vertex is within cutting area - push it down
                float cutAmount = CalculateCutDepth(distanceToPath, toolRadius, cutDepth);
                
                // Apply cut in local space (assuming Y is up)
                Vector3 localCutDirection = transform.InverseTransformDirection(Vector3.down);
                vertices[i] += localCutDirection * cutAmount;
                anyModified = true;
            }
        }

        if (anyModified)
        {
            workingMesh.vertices = vertices;
            workingMesh.RecalculateNormals();
            workingMesh.RecalculateBounds();
            Debug.Log("[MeshCutter] Channel carved successfully.");
        }
        else
        {
            Debug.Log("[MeshCutter] No vertices affected by carving operation.");
        }

        return workingMesh;
    }

    /// <summary>
    /// Creates a deformed mesh representing material removal along a cutting path.
    /// This uses vertex displacement rather than boolean operations for performance.
    /// </summary>
    /// <param name="originalMesh">The mesh to deform.</param>
    /// <param name="pathPoints">Local-space points defining the cutting path.</param>
    /// <param name="pathData">PathData containing tool and cut parameters.</param>
    /// <returns>Deformed mesh.</returns>
    public static Mesh DeformAlongPath(Mesh originalMesh, List<Vector3> pathPoints, PathData pathData)
    {
        if (originalMesh == null || pathPoints == null || pathPoints.Count < 2 || pathData == null)
        {
            Debug.LogWarning("[MeshCutter] Invalid parameters for DeformAlongPath.");
            return originalMesh;
        }

        Mesh deformedMesh = Object.Instantiate(originalMesh);
        deformedMesh.name = originalMesh.name + "_Deformed";

        Vector3[] vertices = deformedMesh.vertices;
        float toolRadius = pathData.toolDiameter / 2f;
        float totalDepth = pathData.TotalDepth;

        for (int i = 0; i < vertices.Length; i++)
        {
            Vector3 vertex = vertices[i];
            float distanceToPath = GetDistanceToPath2D(vertex, pathPoints);

            if (distanceToPath < toolRadius)
            {
                // Calculate smooth cut profile (rounded bottom like end mill)
                float normalizedDist = distanceToPath / toolRadius;
                float profileDepth = Mathf.Sqrt(1f - normalizedDist * normalizedDist) * totalDepth;
                
                // Only cut into the top surface (assuming Y+ is up)
                if (vertex.y > -totalDepth)
                {
                    vertices[i].y = Mathf.Min(vertex.y, -profileDepth);
                }
            }
        }

        deformedMesh.vertices = vertices;
        deformedMesh.RecalculateNormals();
        deformedMesh.RecalculateBounds();

        return deformedMesh;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PUBLIC API - Utilities
    // ══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Generates a simple box mesh with specified dimensions.
    /// Useful for creating workpiece blanks.
    /// </summary>
    /// <param name="dimensions">Size of the box (width, height, depth).</param>
    /// <returns>Box mesh centered at origin.</returns>
    public static Mesh GenerateBoxMesh(Vector3 dimensions)
    {
        Mesh mesh = new Mesh();
        mesh.name = "GeneratedBox";

        float w = dimensions.x / 2f;
        float h = dimensions.y / 2f;
        float d = dimensions.z / 2f;

        Vector3[] vertices = new Vector3[]
        {
            // Front face
            new Vector3(-w, -h, -d), new Vector3(w, -h, -d), new Vector3(w, h, -d), new Vector3(-w, h, -d),
            // Back face
            new Vector3(w, -h, d), new Vector3(-w, -h, d), new Vector3(-w, h, d), new Vector3(w, h, d),
            // Top face
            new Vector3(-w, h, -d), new Vector3(w, h, -d), new Vector3(w, h, d), new Vector3(-w, h, d),
            // Bottom face
            new Vector3(-w, -h, d), new Vector3(w, -h, d), new Vector3(w, -h, -d), new Vector3(-w, -h, -d),
            // Left face
            new Vector3(-w, -h, d), new Vector3(-w, -h, -d), new Vector3(-w, h, -d), new Vector3(-w, h, d),
            // Right face
            new Vector3(w, -h, -d), new Vector3(w, -h, d), new Vector3(w, h, d), new Vector3(w, h, -d)
        };

        int[] triangles = new int[]
        {
            0,2,1, 0,3,2,       // Front
            4,6,5, 4,7,6,       // Back
            8,10,9, 8,11,10,    // Top
            12,14,13, 12,15,14, // Bottom
            16,18,17, 16,19,18, // Left
            20,22,21, 20,23,22  // Right
        };

        Vector3[] normals = new Vector3[]
        {
            Vector3.back, Vector3.back, Vector3.back, Vector3.back,
            Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward,
            Vector3.up, Vector3.up, Vector3.up, Vector3.up,
            Vector3.down, Vector3.down, Vector3.down, Vector3.down,
            Vector3.left, Vector3.left, Vector3.left, Vector3.left,
            Vector3.right, Vector3.right, Vector3.right, Vector3.right
        };

        Vector2[] uvs = new Vector2[]
        {
            new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
            new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
            new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
            new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
            new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1),
            new Vector2(0,0), new Vector2(1,0), new Vector2(1,1), new Vector2(0,1)
        };

        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.RecalculateBounds();

        return mesh;
    }

    /// <summary>
    /// Creates a high-resolution plane mesh suitable for vertex deformation.
    /// </summary>
    /// <param name="width">Width of the plane.</param>
    /// <param name="height">Height (depth) of the plane.</param>
    /// <param name="resolutionX">Number of vertices along X axis.</param>
    /// <param name="resolutionZ">Number of vertices along Z axis.</param>
    /// <returns>Subdivided plane mesh.</returns>
    public static Mesh GenerateSubdividedPlane(float width, float height, int resolutionX, int resolutionZ)
    {
        Mesh mesh = new Mesh();
        mesh.name = "SubdividedPlane";

        int vertexCount = resolutionX * resolutionZ;
        int triangleCount = (resolutionX - 1) * (resolutionZ - 1) * 6;

        Vector3[] vertices = new Vector3[vertexCount];
        Vector3[] normals = new Vector3[vertexCount];
        Vector2[] uvs = new Vector2[vertexCount];
        int[] triangles = new int[triangleCount];

        float halfWidth = width / 2f;
        float halfHeight = height / 2f;

        int vertIndex = 0;
        for (int z = 0; z < resolutionZ; z++)
        {
            for (int x = 0; x < resolutionX; x++)
            {
                float xPos = (float)x / (resolutionX - 1) * width - halfWidth;
                float zPos = (float)z / (resolutionZ - 1) * height - halfHeight;

                vertices[vertIndex] = new Vector3(xPos, 0f, zPos);
                normals[vertIndex] = Vector3.up;
                uvs[vertIndex] = new Vector2((float)x / (resolutionX - 1), (float)z / (resolutionZ - 1));
                vertIndex++;
            }
        }

        int triIndex = 0;
        for (int z = 0; z < resolutionZ - 1; z++)
        {
            for (int x = 0; x < resolutionX - 1; x++)
            {
                int bottomLeft = z * resolutionX + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = bottomLeft + resolutionX;
                int topRight = topLeft + 1;

                triangles[triIndex++] = bottomLeft;
                triangles[triIndex++] = topLeft;
                triangles[triIndex++] = bottomRight;

                triangles[triIndex++] = bottomRight;
                triangles[triIndex++] = topLeft;
                triangles[triIndex++] = topRight;
            }
        }

        mesh.vertices = vertices;
        mesh.normals = normals;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();

        return mesh;
    }

    // ══════════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ══════════════════════════════════════════════════════════════════════════

    private static SliceResult CreatePlaceholderSlice(Mesh originalMesh, Vector3 planePoint, Vector3 planeNormal)
    {
        // Placeholder: Just return copies of original mesh
        // Replace this with actual EzySlice integration
        Debug.LogWarning("[MeshCutter] Using placeholder slice - integrate EzySlice for real cutting.");
        
        Mesh upperHull = Object.Instantiate(originalMesh);
        upperHull.name = originalMesh.name + "_UpperHull";
        
        Mesh lowerHull = Object.Instantiate(originalMesh);
        lowerHull.name = originalMesh.name + "_LowerHull";

        return new SliceResult(upperHull, lowerHull);
    }

    private static GameObject CreateSliceGameObject(
        string name,
        Mesh mesh,
        Transform originalTransform,
        Material[] originalMaterials,
        Material crossSectionMaterial)
    {
        if (mesh == null)
            return null;

        GameObject obj = new GameObject(name);
        obj.transform.position = originalTransform.position;
        obj.transform.rotation = originalTransform.rotation;
        obj.transform.localScale = originalTransform.localScale;

        MeshFilter filter = obj.AddComponent<MeshFilter>();
        filter.mesh = mesh;

        MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
        
        // Set up materials
        List<Material> materials = new List<Material>();
        if (originalMaterials != null)
            materials.AddRange(originalMaterials);
        if (crossSectionMaterial != null)
            materials.Add(crossSectionMaterial);
        renderer.materials = materials.ToArray();

        // Add collider
        MeshCollider collider = obj.AddComponent<MeshCollider>();
        collider.sharedMesh = mesh;
        collider.convex = true;

        // Add rigidbody
        Rigidbody rb = obj.AddComponent<Rigidbody>();
        rb.mass = CalculateMeshMass(mesh, 600f); // Default wood density

        return obj;
    }

    private static float GetDistanceToPath(Vector3 point, List<Vector3> pathPoints)
    {
        float minDistance = float.MaxValue;

        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            float dist = GetDistanceToLineSegment(point, pathPoints[i], pathPoints[i + 1]);
            minDistance = Mathf.Min(minDistance, dist);
        }

        return minDistance;
    }

    private static float GetDistanceToPath2D(Vector3 point, List<Vector3> pathPoints)
    {
        // 2D distance on XZ plane (ignoring Y)
        float minDistance = float.MaxValue;
        Vector2 point2D = new Vector2(point.x, point.z);

        for (int i = 0; i < pathPoints.Count - 1; i++)
        {
            Vector2 a = new Vector2(pathPoints[i].x, pathPoints[i].z);
            Vector2 b = new Vector2(pathPoints[i + 1].x, pathPoints[i + 1].z);
            
            float dist = GetDistanceToLineSegment2D(point2D, a, b);
            minDistance = Mathf.Min(minDistance, dist);
        }

        return minDistance;
    }

    private static float GetDistanceToLineSegment(Vector3 point, Vector3 lineStart, Vector3 lineEnd)
    {
        Vector3 line = lineEnd - lineStart;
        float lineLengthSq = line.sqrMagnitude;
        
        if (lineLengthSq < 0.0001f)
            return Vector3.Distance(point, lineStart);

        float t = Mathf.Clamp01(Vector3.Dot(point - lineStart, line) / lineLengthSq);
        Vector3 projection = lineStart + t * line;
        
        return Vector3.Distance(point, projection);
    }

    private static float GetDistanceToLineSegment2D(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
    {
        Vector2 line = lineEnd - lineStart;
        float lineLengthSq = line.sqrMagnitude;
        
        if (lineLengthSq < 0.0001f)
            return Vector2.Distance(point, lineStart);

        float t = Mathf.Clamp01(Vector2.Dot(point - lineStart, line) / lineLengthSq);
        Vector2 projection = lineStart + t * line;
        
        return Vector2.Distance(point, projection);
    }

    private static float CalculateCutDepth(float distanceToPath, float toolRadius, float maxDepth)
    {
        // Creates a rounded bottom profile like an end mill
        float normalizedDist = distanceToPath / toolRadius;
        return Mathf.Sqrt(1f - normalizedDist * normalizedDist) * maxDepth;
    }

    private static float CalculateMeshMass(Mesh mesh, float density)
    {
        if (mesh == null)
            return 1f;

        // Approximate volume using bounding box
        Vector3 size = mesh.bounds.size;
        float volume = size.x * size.y * size.z;
        
        return volume * density;
    }
}

/// <summary>
/// Result of a mesh slice operation.
/// </summary>
public class SliceResult
{
    /// <summary>The upper half of the sliced mesh (above the cutting plane).</summary>
    public Mesh upperHull;

    /// <summary>The lower half of the sliced mesh (below the cutting plane).</summary>
    public Mesh lowerHull;

    /// <summary>True if the slice was successful and produced two halves.</summary>
    public bool IsValid => upperHull != null && lowerHull != null;

    public SliceResult(Mesh upper, Mesh lower)
    {
        upperHull = upper;
        lowerHull = lower;
    }
}
