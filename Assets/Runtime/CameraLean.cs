using UnityEngine;

public class CameraLean : MonoBehaviour
{
    [SerializeField] float attackDamping = 0.5f;
    [SerializeField] float decayDamping = 0.3f;
    [SerializeField] float strength = 0.1f;

    Vector3 _dampedAccel;
    Vector3 _dampedAccelVel;

   public void Initialize(){

   }

   public void UpdateLean(float deltaTime, Vector3 accel, Vector3 up){
        var planarAccel = Vector3.ProjectOnPlane(accel,up);
        var damping = planarAccel.magnitude > _dampedAccel.magnitude
            ? attackDamping
            : decayDamping;
        _dampedAccel = Vector3.SmoothDamp(
            current: _dampedAccel,
            target: planarAccel,
            currentVelocity: ref _dampedAccelVel,
            smoothTime: damping,
            maxSpeed: float.PositiveInfinity,
            deltaTime: deltaTime
        );

        var leanAxis = Vector3.Cross(_dampedAccel.normalized, up).normalized;

        transform.localRotation = Quaternion.identity;
        transform.rotation = Quaternion.AngleAxis(
            _dampedAccel.magnitude * strength, 
            leanAxis
        ) * transform.rotation;
        
   }
}
