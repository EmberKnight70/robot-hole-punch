using System;
using UnityEngine;
using UnityStandardAssets.Characters.FirstPerson;
using UnityStandardAssets.Utility;
using Random = UnityEngine.Random;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{

    #region Movement Settings

    [Header("Movement Settings")]

    [SerializeField]
    private float m_WalkSpeed;
    [SerializeField]
    private float m_RunSpeed;
    [SerializeField]
    [Range(0f, 1f)]
    private float m_RunstepLenghten;
    [SerializeField]
    private float m_JumpSpeed;
    [SerializeField]
    private float m_StickToGroundForce;
    [SerializeField]
    private float m_GravityMultiplier;

    public bool CanRun { get; set; }
    public bool IsMoving { get; private set; }
    public bool UseGravity { get; set; } = true;
    private bool m_Jump;
    private Vector2 m_Input;
    private Vector3 m_MoveDir = Vector3.zero;
    private CharacterController m_CharacterController;
    public CharacterController CharacterController { get { return m_CharacterController; } }
    private CollisionFlags m_CollisionFlags;
    private bool m_PreviouslyGrounded;
    private Vector3 m_OriginalCameraPosition;
    private bool m_Jumping;

    #endregion

    #region Rotation Settings

    [Header("Rotation Settings")]

    [SerializeField]
    private MouseLook m_MouseLook;
    [SerializeField]
    private bool m_UseHeadBob;
    [SerializeField]
    private CurveControlledBob m_HeadBob = new CurveControlledBob();
    [SerializeField]
    private LerpControlledBob m_JumpBob = new LerpControlledBob();
    [SerializeField]
    private float m_StepInterval;

    private float m_StepCycle;
    private float m_NextStep;

    #endregion

    #region Audio Settings

    [Header("Audio Settings")]

    [SerializeField] private AudioClip[] m_FootstepSounds;    // an array of footstep sounds that will be randomly selected from.
    [SerializeField] private AudioClip[] m_JumpSounds;           // the sound played when character leaves the ground.
    [SerializeField] private AudioClip m_LandSound;           // the sound played when character touches back on ground.

    public bool UseSound { get; set; } = true;

    #endregion

    #region References

    private Camera Camera { get { return PlayerCenterControl.Instance.Camera; } }
    private new AudioSource audio { get { return PlayerCenterControl.Instance.Audio; } }
    private InputSystem input { get { return PlayerCenterControl.Instance.input; } }

    #endregion



    // Use this for initialization
    private void Start()
    {
        m_CharacterController = GetComponent<CharacterController>();
        m_OriginalCameraPosition = Camera.transform.localPosition;
        m_HeadBob.Setup(Camera, m_StepInterval);
        m_StepCycle = 0f;
        m_NextStep = m_StepCycle / 2f;
        m_Jumping = false;
        m_MouseLook.Init(transform, Camera.transform);
    }


    // Update is called once per frame
    private void Update()
    {
        RotateView();
        // the jump state needs to read here to make sure it is not missed
        if (!m_Jump)
        {
            m_Jump = input.Jump;
        }

        if (!m_PreviouslyGrounded && m_CharacterController.isGrounded)
        {
            StartCoroutine(m_JumpBob.DoBobCycle());
            PlayLandingSound();
            m_MoveDir.y = 0f;
            m_Jumping = false;
        }
        if (!m_CharacterController.isGrounded && !m_Jumping && m_PreviouslyGrounded)
        {
            m_MoveDir.y = 0f;
        }

        m_PreviouslyGrounded = m_CharacterController.isGrounded;
    }


    private void PlayLandingSound()
    {
        if (!UseSound) return;

        audio.clip = m_LandSound;
        audio.PlayOneShot(m_LandSound);
        m_NextStep = m_StepCycle + .5f;
    }


    private void FixedUpdate()
    {
        GetInput(out float speed);
        // always move along the camera forward as it is the direction that it being aimed at
        Vector3 desiredMove = transform.forward * m_Input.y + transform.right * m_Input.x;

        // get a normal for the surface that is being touched to move along it
        Physics.SphereCast(transform.position, m_CharacterController.radius, Vector3.down, out RaycastHit hitInfo,
                           m_CharacterController.height / 2f, Physics.AllLayers, QueryTriggerInteraction.Ignore);
        desiredMove = Vector3.ProjectOnPlane(desiredMove, hitInfo.normal).normalized;

        m_MoveDir.x = desiredMove.x * speed;
        m_MoveDir.z = desiredMove.z * speed;


        if (m_CharacterController.isGrounded)
        {
            m_MoveDir.y = -m_StickToGroundForce;

            if (m_Jump)
            {
                m_MoveDir.y = m_JumpSpeed;
                PlayJumpSound();
                m_Jump = false;
                m_Jumping = true;
            }
        }
        else
        {
            m_MoveDir += Physics.gravity * m_GravityMultiplier * Time.fixedDeltaTime;
        }

        if (!UseGravity)
        {
            m_MoveDir.y = 0;
        }

        m_CollisionFlags = m_CharacterController.Move(m_MoveDir * Time.fixedDeltaTime);

        ProgressStepCycle(speed);
        UpdateCameraPosition(speed);

        m_MouseLook.UpdateCursorLock();
    }


    private void PlayJumpSound()
    {
        if (!UseSound) return;


        // pick & play a random footstep sound from the array,
        // excluding sound at index 0
        int n = Random.Range(1, m_JumpSounds.Length);
        audio.clip = m_JumpSounds[n];
        audio.PlayOneShot(m_JumpSounds[n]);
        // move picked sound to index 0 so it's not picked next time
        m_JumpSounds[n] = m_JumpSounds[0];
        m_JumpSounds[0] = audio.clip;
    }


    private void ProgressStepCycle(float speed)
    {
        if (m_CharacterController.velocity.sqrMagnitude > 0 && (m_Input.x != 0 || m_Input.y != 0))
        {
            m_StepCycle += (m_CharacterController.velocity.magnitude + (speed * (IsMoving ? 1f : m_RunstepLenghten))) *
                         Time.fixedDeltaTime;
        }

        if (!(m_StepCycle > m_NextStep))
        {
            return;
        }

        m_NextStep = m_StepCycle + m_StepInterval;

        PlayFootStepAudio();
    }


    private void PlayFootStepAudio()
    {
        if (!m_CharacterController.isGrounded || !UseSound)
        {
            return;
        }
        // pick & play a random footstep sound from the array,
        // excluding sound at index 0
        int n = Random.Range(1, m_FootstepSounds.Length);
        audio.clip = m_FootstepSounds[n];
        audio.PlayOneShot(audio.clip);
        // move picked sound to index 0 so it's not picked next time
        m_FootstepSounds[n] = m_FootstepSounds[0];
        m_FootstepSounds[0] = audio.clip;
    }


    private void UpdateCameraPosition(float speed)
    {
        Vector3 newCameraPosition;
        if (!m_UseHeadBob)
        {
            return;
        }
        if (m_CharacterController.velocity.magnitude > 0 && m_CharacterController.isGrounded)
        {
            Camera.transform.localPosition =
                m_HeadBob.DoHeadBob(m_CharacterController.velocity.magnitude +
                                  (speed * (IsMoving ? 1f : m_RunstepLenghten)));
            newCameraPosition = Camera.transform.localPosition;
            newCameraPosition.y = Camera.transform.localPosition.y - m_JumpBob.Offset();
        }
        else
        {
            newCameraPosition = Camera.transform.localPosition;
            newCameraPosition.y = m_OriginalCameraPosition.y - m_JumpBob.Offset();
        }
        Camera.transform.localPosition = newCameraPosition;
    }


    private void GetInput(out float speed)
    {
        m_Input = new Vector2(input.Horizontal, input.Vertical);

        // normalize input if it exceeds 1 in combined length:
        if (m_Input.sqrMagnitude > 1)
            m_Input.Normalize();

        IsMoving = m_Input.sqrMagnitude != 0;

        if (CanRun)
            speed = !input.Run ? m_WalkSpeed : m_RunSpeed;
        else
            speed = m_WalkSpeed;
    }


    private void RotateView()
    {
        m_MouseLook.LookRotation(transform, Camera.transform);
    }


    private void OnControllerColliderHit(ControllerColliderHit hit)
    {
        Rigidbody body = hit.collider.attachedRigidbody;
        //dont move the rigidbody if the character is on top of it
        if (m_CollisionFlags == CollisionFlags.Below)
        {
            return;
        }
        if (body == null || body.isKinematic)
        {
            return;
        }

        body.AddForceAtPosition(m_CharacterController.velocity * 0.1f, hit.point, ForceMode.Impulse);

    }
}