using System;
using UnityEngine;

public struct CameraInput
{
    public Vector2 Look;
}

public class PlayerCamera : MonoBehaviour
{
    private Vector3 eulerAngles;

    [SerializeField] private float sensitivity = 0.02f;

    internal void Initialize(Transform target)
    {
        transform.position = target.position;

        transform.eulerAngles = eulerAngles = target.eulerAngles;
    }

    public void UpdateRotation(CameraInput input)
    {
        eulerAngles += new Vector3(-input.Look.y, input.Look.x) * sensitivity;
        transform.eulerAngles = eulerAngles;
    }

    public void UpdatePosition(Transform target)
    {
        transform.position = target.position;
    }
}
