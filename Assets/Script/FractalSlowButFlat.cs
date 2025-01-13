using System;
using UnityEngine;

public class FractalSlowButFlat : MonoBehaviour
{

	[SerializeField, Range(1, 8)] int depth = 4;

	[SerializeField] Mesh mesh;

	[SerializeField] Material material;
	
	struct FractalPart {
		// This rotation and position are relative to the logical parent object.
		public Vector3 direction;
		public Quaternion rotation;
		// This transform is relative to unique root object
		// Thus, transform's localRotation and localPosition is logically 'world position'
		public Transform transform;
	}
	
	FractalPart[][] parts;
	
	static Vector3[] directions = {
		Vector3.up, Vector3.right, Vector3.left, Vector3.forward, Vector3.back
	};

	static Quaternion[] rotations = {
		Quaternion.identity,
		Quaternion.Euler(0f, 0f, -90f), Quaternion.Euler(0f, 0f, 90f),
		Quaternion.Euler(90f, 0f, 0f), Quaternion.Euler(-90f, 0f, 0f)
	};
	
	FractalPart  CreatePart (int levelIndex, int childIndex, float scale) {
		var go = new GameObject("Fractal Part L" + levelIndex + " C" + childIndex);
		go.transform.SetParent(transform, false);
		go.transform.localScale = scale * Vector3.one;
		go.AddComponent<MeshFilter>().mesh = mesh;
		go.AddComponent<MeshRenderer>().material = material;
		// FractalPart‘s directions and rotations is local.
		return new FractalPart {
			direction = directions[childIndex],
			rotation = rotations[childIndex],
			transform = go.transform
		};
	}

	private void Awake()
	{
		parts = new FractalPart[depth][];
		// root object only one
		int length = 1;
		for (int i = 0; i < parts.Length; i++) {
			parts[i] = new FractalPart[length];
			// each part has 5 children, so *= 5
			length *= 5;
		}
		
		float scale = 1f;
		parts[0][0] = CreatePart(0, 0, scale);
		// li代表层级
		for (int li = 1; li < parts.Length; li++)
		{
			scale *= 0.5f;
			FractalPart[] levelParts = parts[li];
			// levelParts.Length represents how many objects there are in each level.
			// fpi代表一个父物体的所有子物体（一共五个儿子）
			for (int fpi = 0; fpi < levelParts.Length; fpi += 5) {
				for (int ci = 0; ci < 5; ci++) {
					// ci遍历一个父物体的所有子物体
					// In outer loop we use fpi += 5, so here should be fpi + ci
					levelParts[fpi + ci] = CreatePart(li, ci, scale);
				}
			}
		}
	}
	
	void Update () {
		// rotation speed
		Quaternion deltaRotation = Quaternion.Euler(0f, 22.5f * Time.deltaTime, 0f);
		// rotate the root
		FractalPart rootPart = parts[0][0];
		rootPart.rotation *= deltaRotation;
		// rotation should be write to game object
		rootPart.transform.localRotation = rootPart.rotation;
		// rootPart is not a reference by value, to write back.
		parts[0][0] = rootPart;
		
		for (int li = 1; li < parts.Length; li++) {
			FractalPart[] parentParts = parts[li - 1];
			FractalPart[] levelParts = parts[li];
			for (int fpi = 0; fpi < levelParts.Length; fpi++) {
				Transform parentTransform = parentParts[fpi / 5].transform;
				FractalPart part = levelParts[fpi];
				// As everything spins around its local up axis the delta rotation is the rightmost operand.
				part.rotation *= deltaRotation;
				// part.rotation apply first, then parentTransform.localRotation
				// we should calculate localRotation first
				var parentRotationLocal = parentTransform.localRotation;
				part.transform.localRotation = parentRotationLocal * part.rotation;
				//  we use local position since all li-ci object has the same parent 
				//  the parent's rotation should also affect the direction of its offset
				part.transform.localPosition = parentTransform.localPosition 
				                               + parentRotationLocal * (1.5f * part.transform.localScale.x * part.direction);
				// write part back
				levelParts[fpi] = part;
			}
		}
	}
}