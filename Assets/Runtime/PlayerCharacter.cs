using UnityEngine;
using KinematicCharacterController;

public enum CrouchInput{
    None, Toggle
}

public struct CharacterState{
    public bool Grounded; 
    public Stance Stance;
    public Vector3 Velocity;
    public Vector3 Acceleration;
}   
public enum Stance{
    Stand, Crouch, Slide
}


public struct CharacterInput{
    public Quaternion Rotation;

    public Vector2 Move;

    public bool Jump;

    public bool JumpSustain;

    public CrouchInput Crouch;

}
public class PlayerCharacter : MonoBehaviour, ICharacterController
{

    [SerializeField] KinematicCharacterMotor motor;

    [SerializeField] Transform root;

    [SerializeField] Transform cameraTarget;

    [Space]

    [SerializeField] float walkSpeed = 20f;

    [Space]

    [SerializeField] float crouchSpeed = 7f;

    [SerializeField] float walkResponse = 25f;
    [SerializeField] float crouchResponse = 20f;

    [Space]

    [SerializeField] float airSpeed = 15f;
    [SerializeField] float airAccel = 70f;

    [Space]

    [SerializeField] float jumpSpeed = 20f;

    [SerializeField] float coyoteTime = 0.2f;

    [Space]

    [Range (0f, 1f)]
    [SerializeField] float jumpSustainGravity = 0.4f;
    [SerializeField] float gravity = -90f; 
    [Space]

    [SerializeField] float slideStartSpeed = 25f;
    [SerializeField] float slideEndSpeed = 15f;
    [SerializeField] float slideFriction = 0.8f;
    [SerializeField] float slideSteerAcceleration = 5f;
    [SerializeField] float slideGravity = -90f;

    [Space]

    [SerializeField] float standHeight = 2f;
    [SerializeField] float crouchHeight = 1f;

    [SerializeField] float crouchHeightResponse = 15f;

    [Space]
    [Range(0,1f)]
    [SerializeField] float standCameraTargetHeight = 0.9f;
    [Range(0,1f)]
    [SerializeField] float crouchCameraTargetHeight = 0.7f;


    private CharacterState _state;
    private CharacterState _lastState;
    private CharacterState _tempState;


    Quaternion _requestedRotation;
    
    Vector3 _requestedMovement;

    bool _requestedJump;

    bool _requestedSustainedJump;

    bool _requestedCrouch;

    bool _requestedCrouchInAir;

    float _timeSinceUngrounded;

    float _timeSinceJumpRequest;

    bool _ungroundedDueToJump;

    Collider[] _uncrouchOverlapResults;

    public void Initialize(){
        _state.Stance = Stance.Stand;
        _lastState = _state;
        _uncrouchOverlapResults = new Collider[8];

        motor.CharacterController = this;
    }

     public void UpdateInput(CharacterInput input){
        _requestedRotation = input.Rotation;
        _requestedMovement = new Vector3(input.Move.x, 0f, input.Move.y);
        _requestedMovement = Vector3.ClampMagnitude(_requestedMovement, 1f);
        // Orient pos to direction the cam is facing
        _requestedMovement = input.Rotation * _requestedMovement;

        var wasRequestingJump = _requestedJump;

        _requestedJump = _requestedJump || input.Jump;

        if (_requestedJump && !wasRequestingJump){
            _timeSinceJumpRequest = 0f;
        }
         _requestedSustainedJump = input.JumpSustain;

        var wasRequestingCrouching = _requestedCrouch;

        _requestedCrouch = input.Crouch switch {
            CrouchInput.Toggle => !_requestedCrouch,
            CrouchInput.None => _requestedCrouch,
            _ => _requestedCrouch

        };

        if (_requestedCrouch && !wasRequestingCrouching){
            _requestedCrouchInAir = !_state.Grounded;
        }else if(!_requestedCrouch && wasRequestingCrouching){
            _requestedCrouchInAir = false;
        }
    }

    public void UpdateBody(float deltaTime){
        var currentHeight = motor.Capsule.height;
        var normalizedHeight = currentHeight / standHeight;

        var cameraTargetHeight = currentHeight * (
            _state.Stance is Stance.Stand
            ? standCameraTargetHeight
            : crouchCameraTargetHeight
        );

        var rootTargetScale = new Vector3(1f, normalizedHeight, 1f);

        cameraTarget.localPosition = Vector3.Lerp(
            a: cameraTarget.localPosition,
            b: new Vector3(0f, cameraTargetHeight, 0f),
            t: 1f - Mathf.Exp(-crouchHeightResponse * deltaTime)
        );

        root.localScale = Vector3.Lerp(
            a: root.localScale,
            b: rootTargetScale,
            t: 1f - Mathf.Exp(-crouchHeightResponse * deltaTime)
        );
    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime){

        var forward = Vector3.ProjectOnPlane (
            _requestedRotation * Vector3.forward, 
            motor.CharacterUp
        );
        if (forward != Vector3.zero){
            currentRotation = Quaternion.LookRotation(forward, motor.CharacterUp);
        }
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime){
        if (motor.GroundingStatus.IsStableOnGround){
            _timeSinceUngrounded = 0f;
            _ungroundedDueToJump = false;

            _state.Acceleration = Vector3.zero;
              var groundedMovement = motor.GetDirectionTangentToSurface(
                    direction: _requestedMovement, 
                    surfaceNormal : motor.GroundingStatus.GroundNormal
                ) * _requestedMovement.magnitude;
            //Start slide
            {
                var moving = groundedMovement.sqrMagnitude > 0f;
                var crouching = _state.Stance is Stance.Crouch;
                var wasStanding = _lastState.Stance is Stance.Stand;
                var wasInAir = !_lastState.Grounded;

                if (moving && crouching && (wasStanding || wasInAir)){
                    _state.Stance = Stance.Slide;
                    if (wasInAir){
                       currentVelocity = Vector3.ProjectOnPlane(
                        vector: _lastState.Velocity,
                        planeNormal: motor.GroundingStatus.GroundNormal
                       ); 
                    }

                    var effectiveSlideStartSpeed = slideStartSpeed;

                    if (!_lastState.Grounded && !_requestedCrouchInAir){
                            effectiveSlideStartSpeed = 0f;
                            _requestedCrouchInAir = false;
                    }

                    var slideSpeed = Mathf.Max(effectiveSlideStartSpeed, currentVelocity.magnitude);
                    currentVelocity = motor.GetDirectionTangentToSurface(
                        direction: currentVelocity,
                        surfaceNormal: motor.GroundingStatus.GroundNormal
                    ) * slideSpeed;

                    
                }
            }
            //Move
            if(_state.Stance is Stance.Stand or Stance.Crouch){
                var speed = _state.Stance is Stance.Stand 
                        ? walkSpeed
                        : crouchSpeed;

                var response = _state.Stance is Stance.Stand 
                        ? walkResponse
                        : crouchResponse;


                
                var targetVelocity = groundedMovement * speed ;

                var moveVelocity = Vector3.Lerp(
                    a: currentVelocity,
                    b: targetVelocity,
                    t: 1f- Mathf.Exp(-response *deltaTime)
                );
                _state.Acceleration = (moveVelocity - currentVelocity);
                currentVelocity = moveVelocity;
            }else {
                //friction
                currentVelocity -= currentVelocity * (slideFriction *deltaTime);
               
               {
                    var force = Vector3.ProjectOnPlane(
                        vector: -motor.CharacterUp,
                        planeNormal: motor.GroundingStatus.GroundNormal
                    )* slideGravity; 
                    currentVelocity -= force * deltaTime;
               }
               
               //steer
                {
                    //target Vel based on input 
                    var currentSpeed = currentVelocity.magnitude;
                    var targetVelocity = groundedMovement * currentSpeed;
                    var steerVelocity = currentVelocity;
                    var steerForce = (targetVelocity - steerVelocity) * slideSteerAcceleration * deltaTime;
                    
                    steerVelocity += steerForce;
                    steerVelocity = Vector3.ClampMagnitude(steerVelocity, currentSpeed);

                    _state.Acceleration = (steerVelocity - currentVelocity)/ deltaTime;
                    currentVelocity = steerVelocity;
                }
                //stop
                if(currentVelocity.magnitude < slideEndSpeed){
                    _state.Stance = Stance.Crouch;
                }
            }

        }else{
            _timeSinceUngrounded += deltaTime;

            //Move
            if (_requestedMovement.sqrMagnitude > 0f){
                var planarMovement = Vector3.ProjectOnPlane(
                vector: _requestedMovement,
                planeNormal: motor.CharacterUp
                ) * _requestedMovement.magnitude;

                //current vel on movement plane
                var currentPlanarVelocity = Vector3.ProjectOnPlane(
                    vector: currentVelocity,
                    planeNormal: motor.CharacterUp
                );

                //calc movement force 
                var movementForce = planarMovement * airAccel * deltaTime;
                if (currentPlanarVelocity.magnitude <airSpeed){
                    //add target vel
                    var targetPlanarVelocity = currentPlanarVelocity + movementForce;

                    targetPlanarVelocity = Vector3.ClampMagnitude(targetPlanarVelocity, airSpeed);

                    movementForce = targetPlanarVelocity - currentPlanarVelocity;
                } else if (Vector3.Dot(currentPlanarVelocity, movementForce)> 0){
                    var constrainedMovementForce = Vector3.ProjectOnPlane(
                        vector: movementForce,
                        planeNormal: currentPlanarVelocity.normalized
                    );
                    movementForce = constrainedMovementForce;

                }
                if (motor.GroundingStatus.FoundAnyGround){
                    if (Vector3.Dot(movementForce, currentVelocity + movementForce) > 0f){
                        var obstructionNormal = Vector3.Cross (
                            motor.CharacterUp,
                            Vector3.Cross(
                                motor.CharacterUp,
                                motor.GroundingStatus.GroundNormal
                            )
                        ).normalized;

                        movementForce = Vector3.ProjectOnPlane(movementForce, obstructionNormal);
                    }
                }

                currentVelocity += movementForce;
            }
            //Gravity
            var effectiveGravity = gravity;
            var verticalSpeed = Vector3.Dot(currentVelocity,motor.CharacterUp);

            if (_requestedSustainedJump && verticalSpeed > 0f){
                effectiveGravity = jumpSustainGravity;
            }
            currentVelocity += motor.CharacterUp * effectiveGravity * deltaTime;
        }

        if (_requestedJump){
            var grounded = motor.GroundingStatus.IsStableOnGround;

            var canCoyoteJump  = _timeSinceUngrounded < coyoteTime && !_ungroundedDueToJump;
        
            if (grounded || canCoyoteJump){
                _requestedJump = false;
                _requestedCrouchInAir = false; 
                _requestedCrouch = false;

                motor.ForceUnground(time: 0f);
                _ungroundedDueToJump = true;

                var currentVerticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
                var targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);
                currentVelocity += motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);
            }else{
                _timeSinceJumpRequest += deltaTime;
                var canJumpLater = _timeSinceJumpRequest < coyoteTime;
                _requestedJump = canJumpLater;
            }
        }
    }

    public void BeforeCharacterUpdate(float deltaTime){
        _tempState = _state;
        //crouch
        if (_requestedCrouch && _state.Stance is Stance.Stand){
            _state.Stance = Stance.Crouch;

            motor.SetCapsuleDimensions(
                radius: motor.Capsule.radius,
                height: crouchHeight,
                yOffset: crouchHeight * 0.5f
            );
        }
    }
    public void PostGroundingUpdate(float deltaTime){
        if (!motor.GroundingStatus.IsStableOnGround && _state.Stance is Stance.Slide){
            _state.Stance = Stance.Crouch;
        }
    }

    public void AfterCharacterUpdate(float deltaTime){
        //uncrouch
        if (!_requestedCrouch && _state.Stance is not Stance.Stand){

            motor.SetCapsuleDimensions(
                radius: motor.Capsule.radius,
                height: standHeight,
                yOffset: standHeight * 0.5f
            );

            var pos= motor.TransientPosition;
            var rot = motor.TransientRotation;
            var mask = motor.CollidableLayers;
        
            if (motor.CharacterOverlap(pos,rot, _uncrouchOverlapResults, mask, QueryTriggerInteraction.Ignore) > 0){
                _requestedCrouch = true; 

                motor.SetCapsuleDimensions(
                radius: motor.Capsule.radius,
                height: crouchHeight,
                yOffset: crouchHeight * 0.5f
                );
            } else {
                _state.Stance = Stance.Stand;
            }
        }

        _state.Grounded = motor.GroundingStatus.IsStableOnGround;
        _state.Velocity = motor.Velocity;

        _lastState = _tempState;
    }
  

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport){}
    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport){}
    public bool IsColliderValidForCollisions(Collider coll) => true;
        
    public void OnDiscreteCollisionDetected(Collider hitCollider){}
    public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport){}

    public Transform GetCameraTarget() => cameraTarget;

    public CharacterState GetState() => _state;
    public CharacterState GetLastState() => _lastState;

    public void SetPosition(Vector3 position, bool killVelocity = true){

        motor.SetPosition(position);
        if(killVelocity){
            motor.BaseVelocity = Vector3.zero;
        }
    }
   
}
