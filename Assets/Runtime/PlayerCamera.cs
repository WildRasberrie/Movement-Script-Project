using UnityEngine;

public struct CameraInput{
        public Vector2 Look;
}

public class PlayerCamera : MonoBehaviour
{
    Vector3 _eulerAngles;
    [SerializeField] float sensitivity = 0.1f;

 
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    public void Initialize(Transform target){
        transform.position = target.position;
        transform.eulerAngles = _eulerAngles = target.eulerAngles;
    }

    public void UpdateRotation(CameraInput input){
        _eulerAngles += new Vector3(-input.Look.y, input.Look.x) * sensitivity;
        transform.eulerAngles = _eulerAngles;

    }
    public void UpdatePosition(Transform target){
        transform.position = target.position;
    }
}
