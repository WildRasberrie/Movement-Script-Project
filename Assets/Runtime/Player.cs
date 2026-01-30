using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;


public class Player : MonoBehaviour
{
    [SerializeField] PlayerCharacter playerCharacter;
    [SerializeField] PlayerCamera playerCamera;

    [SerializeField] CameraSpring cameraSpring;
    [SerializeField] CameraLean cameraLean;
    [Space]
    [SerializeField] Volume volume;
    [SerializeField] StanceVignette stanceVignette;

    PlayerInputActions _inputActions;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        playerCharacter.Initialize();
        playerCamera.Initialize(playerCharacter.GetCameraTarget());

        _inputActions = new PlayerInputActions();
        _inputActions.Enable();

        playerCharacter.Initialize();
        playerCamera.Initialize(playerCharacter.GetCameraTarget());
        
        // lock cursor to center
        Cursor.lockState = CursorLockMode.Locked;
        cameraSpring.Initialize();
        cameraLean.Initialize();
        stanceVignette.Initialize(volume.profile);
    }

    void OnDestroy(){
        _inputActions.Dispose();

    }
    // Update is called once per frame
    void Update(){
        var input = _inputActions.Gameplay; 

        //get cam input and update its rot 
        var cameraInput = new CameraInput{Look = input.Look.ReadValue<Vector2>()};
        playerCamera.UpdateRotation(cameraInput);

        //grab character input & update it 
        var characterInput = new CharacterInput{
            Rotation = playerCamera.transform.rotation, 
            Move = input.Move.ReadValue<Vector2>(),
            Jump = input.Jump.WasPressedThisFrame(),
            JumpSustain = input.Jump.IsPressed(),
            Crouch = input.Crouch.WasPressedThisFrame()
                ? CrouchInput.Toggle
                : CrouchInput.None
            };

        playerCharacter.UpdateInput(characterInput);
        playerCharacter.UpdateBody(Time.deltaTime);

        #if UNITY_EDITOR
        if(Keyboard.current.tKey.wasPressedThisFrame){
            var ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);

            if (Physics.Raycast(ray, out var hit)){
                Teleport(hit.point);
            }
        }
        #endif
    }

    void LateUpdate(){
        var cameraTarget = playerCharacter.GetCameraTarget();
        var state = playerCharacter.GetState();
        playerCamera.UpdatePosition(cameraTarget);
        cameraSpring.UpdateSpring(Time.deltaTime, cameraTarget.up);
        cameraLean.UpdateLean(Time.deltaTime, state.Acceleration, cameraTarget.up);
        stanceVignette.UpdateVignette(Time.deltaTime, state.Stance);
    }

    public void Teleport(Vector3 position){
            playerCharacter.SetPosition(position);

    }
}
