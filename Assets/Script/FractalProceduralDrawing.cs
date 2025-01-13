using System;
using UnityEngine;

public class FractalProceduralDrawing : MonoBehaviour
{

	[SerializeField, Range(1, 8)] int depth = 4;

	[SerializeField] Mesh mesh;

	[SerializeField] Material material;
	
	struct FractalPart {
		public Vector3 direction;
		public Quaternion rotation;
		// we don't need transform and game object
		public Vector3 worldPosition;
		public Quaternion worldRotation;
		public float spinAngle;
	}
	
	FractalPart[][] parts;
	Matrix4x4[][] matrices;
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
		parts = new FractalPart[depth][];
		matrices = new Matrix4x4[depth][];
		matricesBuffers = new ComputeBuffer[depth];
		// root object only one
		int stride = 16 * 4;
		int length = 1;
		for (int i = 0; i < parts.Length; i++) {
			parts[i] = new FractalPart[length];
			matrices[i] = new Matrix4x4[length];
			matricesBuffers[i] = new ComputeBuffer(length, stride);
			// each part has 5 children, so *= 5
			length *= 5;
		}
		
		parts[0][0] = CreatePart(0);
		// li means level in fractal
		for (int li = 1; li < parts.Length; li++)
		{
			FractalPart[] levelParts = parts[li];
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
	}
	
	void OnDisable () {
		for (int i = 0; i < matricesBuffers.Length; i++) {
			matricesBuffers[i].Release();
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
		// rootPart is not a reference by value, to write back.
		parts[0][0] = rootPart;
		matrices[0][0] = Matrix4x4.TRS(rootPart.worldPosition, rootPart.worldRotation, Vector3.one);
		float scale = 1;
		for (int li = 1; li < parts.Length; li++) {
			FractalPart[] parentParts = parts[li - 1];
			FractalPart[] levelParts = parts[li];
			Matrix4x4[] levelMatrices = matrices[li];
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

		for (int i = 0; i < matricesBuffers.Length; i++)
		{
			matricesBuffers[i].SetData(matrices[i]);
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