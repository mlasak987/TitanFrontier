using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class CelestialBodyGenerator : MonoBehaviour 
{
	[SerializeField] bool show = true;
	public bool logTimers;
	public int resolution = 300;
    public int collisionResolution = 300;
    public CelestialBodySettings body;

	bool debugDoubleUpdate = true;
	int debug_numUpdates;

	Mesh mesh;
    Mesh collisionMesh;

    ComputeBuffer vertexBuffer;

	bool shapeSettingsUpdated;
	bool shadingNoiseSettingsUpdated;

	Vector2 heightMinMax;
	Material terrainMatInstance;
    MeshCollider collider = new();

    static Dictionary<int, SphereMesh> sphereGenerators;

	void Start() 
	{
		if(InGameMode) HandleGameModeGeneration();
	}

	void Update() 
	{
		if(InEditMode && show) 
		{
			HandleEditModeGeneration();
		}
	}

	void HandleGameModeGeneration() 
	{
		if(CanGenerateMesh()) 
		{
			Dummy();
			heightMinMax = GenerateTerrainMesh(ref mesh, resolution);
			GenerateCollisionMesh(collisionResolution);

			terrainMatInstance = new Material(body.shading.terrainMaterial);
			body.shading.Initialize(body.shape);
			body.shading.SetTerrainProperties(terrainMatInstance, heightMinMax, BodyScale);
			GameObject terrainHolder = GetOrCreateMeshObject("Terrain Mesh", null, terrainMatInstance);

            if (!terrainHolder.TryGetComponent(out collider))
            {
                collider = terrainHolder.AddComponent<MeshCollider>();
            }

            var collisionBakeTimer = System.Diagnostics.Stopwatch.StartNew();
			MeshBaker.BakeMeshImmediate(collisionMesh);
			collider.sharedMesh = collisionMesh;
			LogTimer(collisionBakeTimer, "Mesh collider");
			DrawMesh();

		} else 
		{
			Debug.Log("Could not generate mesh");
		}

		ReleaseAllBuffers();
	}

	void HandleEditModeGeneration() 
	{
		if(InEditMode) 
		{
			ComputeHelper.shouldReleaseEditModeBuffers -= ReleaseAllBuffers;
			ComputeHelper.shouldReleaseEditModeBuffers += ReleaseAllBuffers;
		}

		if(CanGenerateMesh()) 
		{
			if(shapeSettingsUpdated) 
			{
				shapeSettingsUpdated = false;
				shadingNoiseSettingsUpdated = false;
				Dummy();

				var terrainMeshTimer = System.Diagnostics.Stopwatch.StartNew();
				heightMinMax = GenerateTerrainMesh(ref mesh, resolution);

				LogTimer(terrainMeshTimer, "Generate terrain mesh");
				DrawMesh();
			}
			else if(shadingNoiseSettingsUpdated) 
			{
				shadingNoiseSettingsUpdated = false;
				ComputeHelper.CreateStructuredBuffer<Vector3>(ref vertexBuffer, mesh.vertices);
				body.shading.Initialize(body.shape);
				Vector4[] shadingData = body.shading.GenerateShadingData(vertexBuffer);
				mesh.SetUVs(0, shadingData);

				debug_numUpdates++;
				if(debugDoubleUpdate && debug_numUpdates < 2) 
				{
					shadingNoiseSettingsUpdated = true;
					HandleEditModeGeneration();
				}
				if(debug_numUpdates == 2) 
				{
					debug_numUpdates = 0;
				}

			}
		}

		if(body.shading) 
		{
			body.shading.Initialize(body.shape);
			body.shading.SetTerrainProperties(body.shading.terrainMaterial, heightMinMax, BodyScale);
		}

		ReleaseAllBuffers();
	}

	public void OnShapeSettingChanged() 
	{
		shapeSettingsUpdated = true;
	}

	public void OnShadingNoiseSettingChanged() 
	{
		shadingNoiseSettingsUpdated = true;
	}

	void OnValidate() 
	{
		if(body) 
		{
			if(body.shape) 
			{
				body.shape.OnSettingChanged -= OnShapeSettingChanged;
				body.shape.OnSettingChanged += OnShapeSettingChanged;
			}
			if(body.shading) 
			{
				body.shading.OnSettingChanged -= OnShadingNoiseSettingChanged;
				body.shading.OnSettingChanged += OnShadingNoiseSettingChanged;
			}
		}

		OnShapeSettingChanged();
	}

	void Dummy() 
	{
		Vector3[] vertices = new Vector3[] { Vector3.zero };
		ComputeHelper.CreateStructuredBuffer<Vector3>(ref vertexBuffer, vertices);
		body.shape.CalculateHeights(vertexBuffer);
	}

	Vector2 GenerateTerrainMesh(ref Mesh mesh, int resolution) 
	{
		var(vertices, triangles) = CreateSphereVertsAndTris(resolution);
		ComputeHelper.CreateStructuredBuffer<Vector3>(ref vertexBuffer, vertices);

		float edgeLength =(vertices[triangles[0]] - vertices[triangles[1]]).magnitude;

		float[] heights = body.shape.CalculateHeights(vertexBuffer);

		if(body.shape.perturbVertices && body.shape.perturbCompute) 
		{
			ComputeShader perturbShader = body.shape.perturbCompute;
			float maxperturbStrength = body.shape.perturbStrength * edgeLength / 2;

			perturbShader.SetBuffer(0, "points", vertexBuffer);
			perturbShader.SetInt("numPoints", vertices.Length);
			perturbShader.SetFloat("maxStrength", maxperturbStrength);

			ComputeHelper.Run(perturbShader, vertices.Length);
			Vector3[] pertData = new Vector3[vertices.Length];
			vertexBuffer.GetData(vertices);
		}

		float minHeight = float.PositiveInfinity;
		float maxHeight = float.NegativeInfinity;
		for(int i = 0; i < heights.Length; i++) 
		{
			float height = heights[i];
			vertices[i] *= height;
			minHeight = Mathf.Min(minHeight, height);
			maxHeight = Mathf.Max(maxHeight, height);
		}

		CreateMesh(ref mesh, vertices.Length);
		mesh.SetVertices(vertices);
		mesh.SetTriangles(triangles, 0, true);
		mesh.RecalculateNormals();

		body.shading.Initialize(body.shape);
		Vector4[] shadingData = body.shading.GenerateShadingData(vertexBuffer);
		mesh.SetUVs(0, shadingData);

		var normals = mesh.normals;
		var crudeTangents = new Vector4[mesh.vertices.Length];
		for(int i = 0; i < vertices.Length; i++) 
		{
			Vector3 normal = normals[i];
			crudeTangents[i] = new Vector4(-normal.z, 0, normal.x, 1);
		}
		mesh.SetTangents(crudeTangents);

		return new Vector2(minHeight, maxHeight);
	}

	void GenerateCollisionMesh(int resolution) 
	{
		var(vertices, triangles) = CreateSphereVertsAndTris(resolution);
		ComputeHelper.CreateStructuredBuffer<Vector3>(ref vertexBuffer, vertices);

		float[] heights = body.shape.CalculateHeights(vertexBuffer);
		for(int i = 0; i < vertices.Length; i++) 
		{
			float height = heights[i];
			vertices[i] *= height;
		}

		CreateMesh(ref collisionMesh, vertices.Length);
		collisionMesh.vertices = vertices;
		collisionMesh.triangles = triangles;
	}

	void CreateMesh(ref Mesh mesh, int numVertices) 
	{
		const int vertexLimit16Bit = 1 << 16 - 1; // 65535
		if(mesh == null) mesh = new Mesh();
		else mesh.Clear();
		mesh.indexFormat =(numVertices < vertexLimit16Bit) ? UnityEngine.Rendering.IndexFormat.UInt16 : UnityEngine.Rendering.IndexFormat.UInt32;
	}

	void DrawMesh() 
	{
		GameObject terrainHolder = GetOrCreateMeshObject("Terrain Mesh", mesh, body.shading.terrainMaterial);
	}

	GameObject GetOrCreateMeshObject(string name, Mesh mesh, Material material) 
	{
		var child = transform.Find(name);
		if(!child) 
		{
			child = new GameObject(name).transform;
			child.parent = transform;
			child.localPosition = Vector3.zero;
			child.localRotation = Quaternion.identity;
			child.localScale = Vector3.one;
			child.gameObject.layer = gameObject.layer;
		}

		MeshFilter filter;
		if(!child.TryGetComponent(out filter)) filter = child.gameObject.AddComponent<MeshFilter>();
		filter.sharedMesh = mesh;

		MeshRenderer renderer;
		if(!child.TryGetComponent(out renderer)) renderer = child.gameObject.AddComponent<MeshRenderer>();
		renderer.sharedMaterial = material;

		return child.gameObject;
	}

	public float GetOceanRadius() 
	{
		if(!body.shading.hasOcean) return 0;
		return UnscaledOceanRadius * BodyScale;
	}

	float UnscaledOceanRadius 
	{
		get { return Mathf.Lerp(heightMinMax.x, 1, body.shading.oceanLevel); }
	}

	public float BodyScale 
	{
		get { return transform.localScale.x; }
	}

	(Vector3[] vertices, int[] triangles) CreateSphereVertsAndTris(int resolution) 
	{
		if(sphereGenerators == null) sphereGenerators = new Dictionary<int, SphereMesh>();
		if(!sphereGenerators.ContainsKey(resolution)) sphereGenerators.Add(resolution, new SphereMesh(resolution));

		var generator = sphereGenerators[resolution];

		var vertices = new Vector3[generator.Vertices.Length];
		var triangles = new int[generator.Triangles.Length];
		System.Array.Copy(generator.Vertices, vertices, vertices.Length);
		System.Array.Copy(generator.Triangles, triangles, triangles.Length);
		return(vertices, triangles);
	}

	void ReleaseAllBuffers() 
	{
		ComputeHelper.Release(vertexBuffer);
		if(body.shape) body.shape.ReleaseBuffers();
		if(body.shading) body.shading.ReleaseBuffers();
	}

	void OnDestroy() 
	{
		ReleaseAllBuffers();
	}

	bool CanGenerateMesh() 
	{
		return ComputeHelper.CanRunEditModeCompute && body.shape && body.shape.heightMapCompute;
	}

	void LogTimer(System.Diagnostics.Stopwatch sw, string text) 
	{
		if(logTimers) Debug.Log(text + " " + sw.ElapsedMilliseconds + " ms.");
	}

	bool InGameMode 
	{
		get { return Application.isPlaying; }
	}

	bool InEditMode 
	{
		get { return !Application.isPlaying; }
	}

	public class TerrainData 
	{
		public float[] heights;
		public Vector4[] uvs;
	}
}