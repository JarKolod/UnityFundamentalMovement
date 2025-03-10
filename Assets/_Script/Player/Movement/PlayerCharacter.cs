using UnityEngine;
using KinematicCharacterController;

public enum CrouchInput
{
    None,
    Toggle
}

public enum Stance
{
    Stand,
    Crouch
}

public struct CharacterInput
{
    public Quaternion Rotation;
    public Vector2 Move;
    public bool Jump;
    public bool JumpSustain;
    public CrouchInput Crouch;
}

public class PlayerCharacter : MonoBehaviour, ICharacterController
{
    [SerializeField] private KinematicCharacterMotor motor;
    [SerializeField] private Transform rootCamera;
    [SerializeField] private Transform cameraTarget;
    [Space]
    [SerializeField] private float walkSpeed = 20f;
    [SerializeField] private float crouchSpeed = 10f;
    [SerializeField] private float walkResponse = 25f;
    [SerializeField] private float crouchResponse = 20f;
    [Space]
    [SerializeField] private float airSpeed = 15f;
    [SerializeField] private float airAcceleration = 70f;
    [Space]
    [SerializeField] private float jumpSpeed = 20f;
    [Range(0f, 1f)]
    [SerializeField] private float jumpSustainGravityMultiplier = 0.4f;
    [SerializeField] private float gravityForce = 90f;
    [Space]
    [SerializeField] private float standHeight = 2f;
    [SerializeField] private float crouchHeight = 1f;
    [SerializeField] private float crouchHeightResponse = 15f;
    [Range(0f, 1.0f)]
    [SerializeField] private float standCameraTarget = 0.9f;
    [Range(0f, 1.0f)]
    [SerializeField] private float crouchCameraTarget = 0.7f;

    private Stance stance;

    private Quaternion requestedRotation;
    private Vector3 requestedMovement;
    private bool requestedJump;
    private bool requestedSustainedJump;
    private bool requestedCrouch;

    private Collider[] uncrouchOverlapResult;

    public void Initialize()
    {
        stance = Stance.Stand;
        uncrouchOverlapResult = new Collider[2];

        motor.CharacterController = this;
    }

    public void UpdateInput(CharacterInput input)
    {
        requestedRotation = input.Rotation;
        requestedMovement = new Vector3(input.Move.x, 0f, input.Move.y);
        requestedMovement = Vector3.ClampMagnitude(requestedMovement, 1f);
        requestedMovement = input.Rotation * requestedMovement;
        requestedJump = requestedJump || input.Jump;
        requestedSustainedJump = input.JumpSustain;
        requestedCrouch = input.Crouch switch
        {
            CrouchInput.Toggle => !requestedCrouch,
            CrouchInput.None => requestedCrouch,
            _ => requestedCrouch
        };
    }

    public void UpdateBody(float deltaTime)
    {
        float currentHeight = motor.Capsule.height;
        float normalizeHeight = currentHeight / standHeight;

        float cameraTargetHeight = currentHeight *
        (
            stance is Stance.Stand ? standCameraTarget : crouchCameraTarget
        );
        Vector3 rootTargetScale = new Vector3(1f, normalizeHeight, 1f);

        cameraTarget.localPosition = Vector3.Lerp
            (
                cameraTarget.localPosition,
                new Vector3(0f, cameraTargetHeight, 0f),
                1f - Mathf.Exp(-crouchHeightResponse * deltaTime)
            );
        rootCamera.localScale = Vector3.Lerp
            (
                rootCamera.localScale,
                rootTargetScale,
                1f - Mathf.Exp(-crouchHeightResponse * deltaTime)
            );
    }

    public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
    {
        // move
        if (motor.GroundingStatus.IsStableOnGround)
        {
            Vector3 groundedMovement = motor.GetDirectionTangentToSurface(direction: requestedMovement, surfaceNormal: motor.GroundingStatus.GroundNormal);
            groundedMovement *= requestedMovement.magnitude;

            float speed = stance is Stance.Stand ? walkSpeed : crouchSpeed;
            float response = stance is Stance.Stand ? walkResponse : crouchResponse;


            Vector3 targetVelocity = groundedMovement * speed;
            currentVelocity = Vector3.Lerp
                (
                    currentVelocity,
                    targetVelocity,
                    1f - Mathf.Exp(-response * deltaTime)
                );
        }
        else
        {
            //in air
            if (requestedMovement.sqrMagnitude > 0f)
            {
                Vector3 planarMovement = Vector3.ProjectOnPlane
                    (
                        vector: requestedMovement,
                        planeNormal: motor.CharacterUp
                    ) * requestedMovement.magnitude;

                var currentPlanerVelocity = Vector3.ProjectOnPlane
                    (
                        vector: currentVelocity,
                        planeNormal: motor.CharacterUp
                    );

                Vector3 movementForce = planarMovement * airAcceleration * deltaTime;

                if (currentPlanerVelocity.magnitude < airSpeed)
                {
                    Vector3 targetPlanerVeloctiy = currentPlanerVelocity + movementForce;

                    targetPlanerVeloctiy = Vector3.ClampMagnitude(targetPlanerVeloctiy, airSpeed);

                    movementForce = targetPlanerVeloctiy - currentPlanerVelocity;
                }
                // preserve in air velocity/speed when jumping
                else if(Vector3.Dot(currentPlanerVelocity, movementForce) > 0f)
                {
                    Vector3 constainedMovementForce = Vector3.ProjectOnPlane
                        (
                            vector: movementForce,
                            planeNormal: currentPlanerVelocity.normalized
                        );
                    movementForce = constainedMovementForce;
                }

                currentVelocity += movementForce;
            }

            float effectiveGravity = gravityForce;
            float verticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
            if (requestedSustainedJump && verticalSpeed > 0f)
            {
                effectiveGravity *= jumpSustainGravityMultiplier;
            }
            currentVelocity += motor.CharacterUp * -effectiveGravity * deltaTime;
        }

        if (requestedJump)
        {
            requestedJump = false;
            motor.ForceUnground(time: 0.1f);

            // minimum vertical speed to the jump speed
            float currentVerticalSpeed = Vector3.Dot(currentVelocity, motor.CharacterUp);
            float targetVerticalSpeed = Mathf.Max(currentVerticalSpeed, jumpSpeed);

            // add diffrence in current and target vertical speed to character velocity
            currentVelocity += motor.CharacterUp * (targetVerticalSpeed - currentVerticalSpeed);
        }

    }

    public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
    {
        Vector3 forward = Vector3.ProjectOnPlane(requestedRotation * Vector3.forward, motor.CharacterUp);

        if (forward != Vector3.zero)
        {
            currentRotation = Quaternion.LookRotation(forward, motor.CharacterUp);
        }
    }

    public void BeforeCharacterUpdate(float deltaTime)
    {
        // Crouch
        print("req: " + requestedCrouch + " stance: " + stance);
        if (requestedCrouch && stance is Stance.Stand)
        {
            stance = Stance.Crouch;
            motor.SetCapsuleDimensions
            (
                radius: motor.Capsule.radius,
                height: crouchHeight,
                yOffset: crouchHeight * 0.5f
            );
        }
    }

    public void AfterCharacterUpdate(float deltaTime)
    {
        // Uncrouch
        if (!requestedCrouch && stance is not Stance.Stand)
        {
            motor.SetCapsuleDimensions
            (
                radius: motor.Capsule.radius,
                height: standHeight,
                yOffset: standHeight * 0.5f
            );

            if (motor.CharacterOverlap(
                motor.TransientPosition,
                motor.TransientRotation,
                uncrouchOverlapResult,
                motor.CollidableLayers,
                QueryTriggerInteraction.Ignore) > 0)
            {
                requestedCrouch = true;
                motor.SetCapsuleDimensions
                (
                    radius: motor.Capsule.radius,
                    height: crouchHeight,
                    yOffset: crouchHeight * 0.5f
                );
            }
            else
            {
                stance = Stance.Stand;
            }

        }
    }

    public bool IsColliderValidForCollisions(Collider coll)
    {
        return true;
    }

    public void OnDiscreteCollisionDetected(Collider hitCollider)
    {

    }

    public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {

    }

    public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint, ref HitStabilityReport hitStabilityReport)
    {

    }

    public void PostGroundingUpdate(float deltaTime)
    {

    }

    public void ProcessHitStabilityReport(
        Collider hitCollider,
        Vector3 hitNormal,
        Vector3 hitPoint,
        Vector3 atCharacterPosition,
        Quaternion atCharacterRotation,
        ref HitStabilityReport hitStabilityReport)
    {

    }

    public Transform GetCameraTarget()
    {
        return cameraTarget;
    }
}
