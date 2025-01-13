using UnityEngine;

public class FractalSlow : MonoBehaviour
{

	[SerializeField, Range(1, 8)] int depth = 4;

	FractalSlow CreateChild (Vector3 direction, Quaternion rotation) {
		// Fractal‘s Child using mesh of it's gameobject
		FractalSlow child = Instantiate(this);
		child.depth = depth - 1;
		child.transform.localPosition = 0.75f * direction;
		// rotation is to adjust the position of child
		child.transform.localRotation = rotation;
		child.transform.localScale = 0.5f * Vector3.one;
		return child;
	}
	void Start () {
		if (depth <= 1) {
			return;
		}

		FractalSlow childA = CreateChild(Vector3.up, Quaternion.identity);
		FractalSlow childB = CreateChild(Vector3.right, Quaternion.Euler(0f, 0f, -90f));
		FractalSlow childC = CreateChild(Vector3.left, Quaternion.Euler(0f, 0f, 90f));
		FractalSlow childD = CreateChild(Vector3.forward, Quaternion.Euler(90f, 0f, 0f));
		FractalSlow childE = CreateChild(Vector3.back, Quaternion.Euler(-90f, 0f, 0f));
		// Establish the parent-child relationship only after both children have been created.
		// Otherwise child now gets cloned as well, because Instantiate duplicates
		// the entire game object hierarchy that was passed to it.
		childA.transform.SetParent(transform, false);
		childB.transform.SetParent(transform, false);
		childC.transform.SetParent(transform, false);
		childD.transform.SetParent(transform, false);
		childE.transform.SetParent(transform, false);
	}
	
	void Update () {
		// for a sphere rotate itself has no effect
		// but this will also rotate its child object
		transform.Rotate(0f, 22.5f * Time.deltaTime, 0f);
	}

}