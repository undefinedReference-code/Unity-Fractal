using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;	
using UnityEngine;

public class FractalJob : MonoBehaviour
{
	struct UpdateFractalLevelJob : IJobFor
	{
		public void Execute(int i)
		{
			
		}
	}
	
	[SerializeField, Range(1, 8)] int depth = 4;

	[SerializeField] Mesh mesh;

	[SerializeField] Material material;
	[SerializeField] Material material2;
	
	static readonly int matricesId = Shader.PropertyToID("_Matrices");
	static MaterialPropertyBlock propertyBlock;
	struct FractalPart {
		public Vector3 direction;
		public Quaternion rotation;
		// we don't need transform and game object
		public Vector3 worldPosition;
		public Quaternion worldRotation;
		public float spinAngle;
	}
	
	NativeArray<FractalPart>[] parts;
	NativeArray<Matrix4x4>[] matrices;
	ComputeBuffer[] matricesBuffers;
	
	static Vector3[] directions = {
		Vector3.up, Vector3.right, Vector3.left, Vector3.forward, Vector3.back
	};

	static Quaternion[] rotations = {
		Quaternion.identity,
		Quaternion.Euler(0f, 0f, -90f), Quaternion.Euler(0f, 0f, 90f),
		Quaternion.Euler(90f, 0f, 0f), Quaternion.Euler(-90f, 0f, 0f)
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
		matrices = new NativeArray<Matrix4x4>[depth];
		matricesBuffers = new ComputeBuffer[depth];
		// root object only one
		int stride = 16 * 4;
		int length = 1;
		for (int i = 0; i < parts.Length; i++) {
			parts[i] = new NativeArray<FractalPart>(length, Allocator.Persistent);
			matrices[i] = new NativeArray<Matrix4x4>(length, Allocator.Persistent);
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
		float spinAngleDelta = 22.5f * Time.deltaTime;
		// root part
		FractalPart rootPart = parts[0][0];
		rootPart.spinAngle += spinAngleDelta;
		// rotation should be write to game object
		rootPart.worldRotation  = rootPart.rotation * Quaternion.Euler(0f, rootPart.spinAngle, 0f);
		
		// No matter how we move the object that mounts the script,
		// the position of the fractal generation will not change.
		// We can fix this by incorporating the game object's rotation
		// and position into the root object matrix in Update.
		rootPart.worldRotation = transform.rotation * rootPart.worldRotation;
		rootPart.worldPosition = transform.position;
		
		// rootPart is not a reference by value, to write back.
		parts[0][0] = rootPart;
		float objectScale = transform.localScale.x;
		matrices[0][0] = Matrix4x4.TRS(rootPart.worldPosition, rootPart.worldRotation, objectScale * Vector3.one);
		
		float scale = objectScale;
		for (int li = 1; li < parts.Length; li++) {
			NativeArray<FractalPart> parentParts = parts[li - 1];
			NativeArray<FractalPart> levelParts = parts[li];
			NativeArray<Matrix4x4> levelMatrices = matrices[li];
			scale *= 0.5f;
			for (int fpi = 0; fpi < levelParts.Length; fpi++) {
				FractalPart parent = parentParts[fpi / 5];
				FractalPart part = levelParts[fpi];
				// As everything spins around its local up axis the delta rotation is the rightmost operand.
				part.spinAngle += spinAngleDelta;
				// Transform global quaternion from local quaternion
				// Quaternion worldRotation= transform.parent.rotation * localRotation;
				part.worldRotation = parent.worldRotation * part.rotation * Quaternion.Euler(0f, part.spinAngle, 0f);
				//  we use local position since all li-ci object has the same parent 
				//  the parent's rotation should also affect the direction of its offset
				part.worldPosition = parent.worldPosition 
				                     + parent.worldRotation * (1.5f * scale * part.direction);
				// write part back
				levelParts[fpi] = part;
				levelMatrices[fpi] = Matrix4x4.TRS(part.worldPosition, part.worldRotation, scale * Vector3.one);
			}
		}
		
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