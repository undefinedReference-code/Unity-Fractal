using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

using static Unity.Mathematics.math;
using float3x4 = Unity.Mathematics.float3x4;
using quaternion = Unity.Mathematics.quaternion;

public class FractalJob : MonoBehaviour
{
	[BurstCompile(FloatPrecision.Standard, FloatMode.Fast, CompileSynchronously = true)]
	struct UpdateFractalLevelJob : IJobFor
	{
		public float spinAngleDelta;
		public float scale;
		
		[ReadOnly]
		public NativeArray<FractalPart> parents;
		public NativeArray<FractalPart> parts;
		[WriteOnly]	
		public NativeArray<float3x4> matrices;
		
		public void Execute(int i)
		{
			FractalPart parent = parents[i / 5];
			FractalPart part = parts[i];
			// As everything spins around its local up axis the delta rotation is the rightmost operand.
			part.spinAngle += spinAngleDelta;
			// Transform global quaternion from local quaternion
			// Quaternion worldRotation= transform.parent.rotation * localRotation;
			part.worldRotation = mul(parent.worldRotation, mul(part.rotation, quaternion.RotateY(part.spinAngle)));
			//  we use local position since all li-ci object has the same parent 
			//  the parent's rotation should also affect the direction of its offset
			part.worldPosition = parent.worldPosition 
			                     + mul(parent.worldRotation, (1.5f * scale * part.direction));
			// write part back
			parts[i] = part;
			float3x3 r = float3x3(part.worldRotation) * scale;
			matrices[i] = float3x4(r.c0, r.c1, r.c2, part.worldPosition);
		}
	}
	
	[SerializeField, Range(1, 8)] int depth = 4;

	[SerializeField] Mesh mesh;

	[SerializeField] Material material;
	
	static readonly int matricesId = Shader.PropertyToID("_Matrices");
	static MaterialPropertyBlock propertyBlock;
	struct FractalPart {
		public float3 direction;
		public quaternion rotation;
		// we don't need transform and game object	
		public float3 worldPosition;
		public quaternion worldRotation;
		public float spinAngle;
	}
	
	NativeArray<FractalPart>[] parts;
	NativeArray<float3x4>[] matrices;
	ComputeBuffer[] matricesBuffers;
	
	static float3[] directions = {
		up(), right(), left(), forward(), back()
	};

	static quaternion[] rotations = {
		quaternion.identity,
		quaternion.RotateZ(-0.5f * PI), quaternion.RotateZ(0.5f * PI),
		quaternion.RotateX(0.5f * PI), quaternion.RotateX(-0.5f * PI)
	};
	
	FractalPart  CreatePart (int childIndex) {
		// FractalPart‘s directions and rotations is local.
		// we don't need gameobject compare to FractalSlowButFlat
		return new FractalPart {
			direction = directions[childIndex],
			rotation = rotations[childIndex],
		};
	}

	private void OnEnable()
	{
		parts = new NativeArray<FractalPart>[depth];
		matrices = new NativeArray<float3x4>[depth];
		matricesBuffers = new ComputeBuffer[depth];
		// root object only one
		int stride = 12 * 4;
		int length = 1;
		for (int i = 0; i < parts.Length; i++) {
			parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent);
			matrices[i] = new NativeArray<float3x4>(length, Allocator.Persistent);
			matricesBuffers[i] = new ComputeBuffer(length, stride);
			// each part has 5 children, so *= 5
			length *= 5;
		}
		
		parts[0][0] = CreatePart(0);
		// li means level in fractal
		for (int li = 1; li < parts.Length; li++)
		{
			NativeArray<FractalPart> levelParts = parts[li];
			// levelParts.Length represents how many objects there are in each level.
			// fpi代表一个父物体的所有子物体（一共五个儿子）
			for (int fpi = 0; fpi < levelParts.Length; fpi += 5) {
				for (int ci = 0; ci < 5; ci++) {
					// ci遍历一个父物体的所有子物体
					// In outer loop we use fpi += 5, so here should be fpi + ci
					levelParts[fpi + ci] = CreatePart(ci);
				}
			}
		}

		if (propertyBlock == null)
			propertyBlock = new MaterialPropertyBlock();
	}
	
	void OnDisable () {
		for (int i = 0; i < matricesBuffers.Length; i++) {
			matricesBuffers[i].Release();
			parts[i].Dispose();
			matrices[i].Dispose();	
		}

		parts = null;
		matrices = null;
		matricesBuffers = null;
	}
	
	void Update () {
		// rotation speed
		float spinAngleDelta = PI * 22.5f * Time.deltaTime / 180f;
		// root part
		FractalPart rootPart = parts[0][0];
		rootPart.spinAngle += spinAngleDelta;
		// rotation should be write to game object
		rootPart.worldRotation  = mul(rootPart.rotation, quaternion.RotateY(rootPart.spinAngle));
		
		// No matter how we move the object that mounts the script,
		// the position of the fractal generation will not change.	
		// We can fix this by incorporating the game object's rotation
		// and position into the root object matrix in Update.
		rootPart.worldRotation = transform.rotation * rootPart.worldRotation;
		rootPart.worldPosition = transform.position;
		
		// rootPart is not a reference by value, to write back.
		parts[0][0] = rootPart;
		float objectScale = transform.localScale.x;
		float3x3 r = float3x3(rootPart.worldRotation) * objectScale;
		matrices[0][0] = float3x4(r.c0, r.c1, r.c2, rootPart.worldPosition);
		
		float scale = objectScale;
		JobHandle jobHandle = default;
		for (int li = 1; li < parts.Length; li++) {
			scale *= 0.5f;

			var job = new UpdateFractalLevelJob()
			{
				spinAngleDelta = spinAngleDelta,
				scale = scale, 
				parents = parts[li - 1],
				parts = parts[li],
				matrices = matrices[li]
			};
			jobHandle = job.Schedule(parts[li].Length, jobHandle);
		}
		jobHandle.Complete();
		
		var bounds = new Bounds(Vector3.zero, objectScale * 3f * Vector3.one);
		for (int i = 0; i < matricesBuffers.Length; i++)
		{
			ComputeBuffer buffer = matricesBuffers[i];
			buffer.SetData(matrices[i]);
			propertyBlock.SetBuffer(matricesId, buffer);
			Graphics.DrawMeshInstancedProcedural(mesh, 0, material, bounds, buffer.count, propertyBlock);
		}
	}

	private void OnValidate()
	{
		if (parts != null && enabled)
		{
			OnDisable();
			OnEnable();
		} 
	}
}