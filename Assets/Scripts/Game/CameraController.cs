using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraController : MonoBehaviour {

	public Transform pivot;

	[Header ("Rotation")]
	public float rotSpeed = 6;
	public float rotSmoothing = 10;

	[Header ("Zoom")]
	public float zoomSpeed = 6;
	public float zoomSmoothing = 10;

	Vector2 rotInput;

	float targetZoomDst;
	float currentZoomDst;

	void Start () 
	{
		rotInput = (Vector2) transform.eulerAngles;
		targetZoomDst = (transform.position - pivot.position).magnitude;
		currentZoomDst = targetZoomDst;
	}

	void LateUpdate () 
	{
		if (Input.GetMouseButton(0)) 
		{
			Vector2 mouseInput = new Vector2 (Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            rotInput += new Vector2(-mouseInput.y, mouseInput.x) * rotSpeed;
		}

		HandleZoomInput();
        UpdateRotation();
		UpdateZoom();
	}

	void UpdateRotation () 
	{
		Quaternion targetRot = Quaternion.Euler (rotInput.x, rotInput.y, 0);
		Quaternion rotation = Quaternion.Slerp (transform.rotation, targetRot, Time.deltaTime * rotSmoothing);
		Vector3 position = rotation * Vector3.forward * -(pivot.position - transform.position).magnitude + pivot.position;

		transform.rotation = rotation;
		transform.position = position;
	}

	void HandleZoomInput() 
	{
		targetZoomDst -= Input.mouseScrollDelta.y * zoomSpeed;
	}

	void UpdateZoom () 
	{
		currentZoomDst = Mathf.Lerp(currentZoomDst, targetZoomDst, Time.deltaTime * zoomSmoothing);
		Vector3 dirToPivot = (pivot.transform.position - transform.position).normalized;
		transform.position = pivot.transform.position - dirToPivot * currentZoomDst;
	}
}