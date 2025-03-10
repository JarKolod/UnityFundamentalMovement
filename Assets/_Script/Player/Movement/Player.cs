using Unity.VisualScripting;
using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private PlayerCharacter playerCharacter;
    [SerializeField] private PlayerCamera playerCamera;

    private PlayerInputActions inputActions;

    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;

        inputActions = new PlayerInputActions();
        inputActions.Enable();

        playerCharacter.Initialize();
        playerCamera.Initialize(playerCharacter.GetCameraTarget());
    }

    private void OnDestroy()
    {
        inputActions.Dispose();
    }

    private void Update()
    {
        var input = inputActions.Gameplay;
        float deltaTime = Time.deltaTime;

        CameraInput cameraInput = new CameraInput { Look = input.Look.ReadValue<Vector2>() };

        playerCamera.UpdateRotation(cameraInput);

        CharacterInput characterInput = new CharacterInput
        {
            Rotation = playerCamera.transform.rotation,
            Move = input.Move.ReadValue<Vector2>(),
            Jump = input.Jump.WasPressedThisFrame(),
            JumpSustain = input.Jump.IsPressed(),
            Crouch = input.Crouch.WasPressedThisFrame() ? CrouchInput.Toggle : CrouchInput.None
        };

        playerCharacter.UpdateInput(characterInput);
        playerCharacter.UpdateBody(deltaTime);

        playerCamera.UpdatePosition(playerCharacter.GetCameraTarget());
    }
}
