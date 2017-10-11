using UnityEngine;

public class AVR_CameraFollow : MonoBehaviour
{
	// The position that that camera will be following.
	public Transform target;
	// The speed with which the camera will be following.           
	public float smoothing = 5f;

	// The initial offset from the target.
	Vector3 offset;

    public Vector3 cameraPosition = new Vector3(0.5f, 1.0f, 0.5f);

	void Start()
	{
		// Calculate the initial offset.
        offset = transform.position - target.position;
	}

	void Update()
	{
		// Create a postion the camera is aiming for based on 
		// the offset from the target.
		Vector3 targetCamPos = target.position + offset + cameraPosition;

		// Smoothly interpolate between the camera's current 
		// position and it's target position.
		transform.position = Vector3.Lerp(transform.position, targetCamPos, smoothing * Time.deltaTime);
	}
}