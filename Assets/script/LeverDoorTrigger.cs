using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class LeverDoorTrigger : MonoBehaviour
{
    [Header("Lever")]
    public HingeJoint leverHinge;

    [Header("Door")]
    public Transform doorTransform;
    public float doorOpenAngle = 120f;

    [Header("Trigger Logic")]
    public float openTriggerAngle = -5f;
    public float resetAngle = -2f;

    [Header("Audio")]
    public AudioClip doorOpenClip;
    public AudioClip doorCloseClip;
    public AudioSource leverAudioSource;

    private Quaternion doorClosedRot;
    private Quaternion doorOpenRot;
    private Quaternion leverRestRotation;
    private Vector3 leverRestPosition;
    private Rigidbody leverBody;
    private Rigidbody doorBody;
    private XRGrabInteractable leverGrab;
    private bool keyInSocket;

    private bool doorOpened = false;
    private bool leverLocked = false;

    void Awake()
    {
        // Measure the lever in the door's frame, including while XR Grab unparents it.
        leverRestRotation = Quaternion.Inverse(doorTransform.rotation) * transform.rotation;
        leverRestPosition = doorTransform.InverseTransformPoint(transform.position);
        leverBody = leverHinge.GetComponent<Rigidbody>();
        doorBody = doorTransform.GetComponent<Rigidbody>();
        leverGrab = GetComponent<XRGrabInteractable>();
        doorClosedRot = doorTransform.localRotation;
        doorOpenRot = Quaternion.Euler(
            doorClosedRot.eulerAngles + new Vector3(0, doorOpenAngle, 0)
        );
        SetUnlocked(keyInSocket);
    }

    public float LeverAngle
    {
        get
        {
            Quaternion relative = Quaternion.Inverse(doorTransform.rotation) * transform.rotation;
            Quaternion delta = Quaternion.Inverse(leverRestRotation) * relative;
            Vector3 axis = leverHinge.axis.normalized;
            float twist = Vector3.Dot(new Vector3(delta.x, delta.y, delta.z), axis);
            return Mathf.DeltaAngle(0f, 2f * Mathf.Atan2(twist, delta.w) * Mathf.Rad2Deg);
        }
    }

    // Persistent socket events call this so removal during a grab is also safe.
    public void SetUnlocked(bool unlocked)
    {
        keyInSocket = unlocked;
        if (!leverBody) return;
        ReleaseLever();
        ResetLever();
        leverBody.isKinematic = !unlocked;
        leverGrab.enabled = unlocked;
        leverLocked = false;
        if (leverAudioSource) leverAudioSource.Stop();
    }

    void ReleaseLever()
    {
        if (leverGrab && leverGrab.isSelected && leverGrab.interactionManager)
            leverGrab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)leverGrab);
    }

    void ResetLever()
    {
        transform.SetParent(doorTransform, true);
        leverBody.position = doorTransform.TransformPoint(leverRestPosition);
        leverBody.rotation = doorTransform.rotation * leverRestRotation;
        if (!leverBody.isKinematic)
        {
            leverBody.velocity = Vector3.zero;
            leverBody.angularVelocity = Vector3.zero;
        }
    }

    void ToggleDoor()
    {
        // A 120-degree door swing must not leave the grabbed body at the old
        // world anchor or drag it against the player's stationary hand.
        ReleaseLever();
        doorOpened = !doorOpened;
        doorTransform.localRotation = doorOpened ? doorOpenRot : doorClosedRot;
        if (doorBody)
        {
            doorBody.position = doorTransform.position;
            doorBody.rotation = doorTransform.rotation;
        }
        ResetLever();
        Physics.SyncTransforms();

        AudioSource doorAudio = doorTransform.GetComponent<AudioSource>();
        AudioClip clip = doorOpened ? doorOpenClip : doorCloseClip;
        if (doorAudio && clip)
        {
            doorAudio.clip = clip;
            doorAudio.Play();
        }
    }

    void Update()
    {
        // XR Grab can change isKinematic; only the socket can unlock the door.
        if (!keyInSocket || leverBody.isKinematic)
        {
            if (leverAudioSource != null && leverAudioSource.isPlaying)
                leverAudioSource.Stop();
            return;
        }

        float angle = LeverAngle;

        if (leverAudioSource != null)
        {
            if (Mathf.Abs(angle) > 2f && !leverAudioSource.isPlaying)
                leverAudioSource.Play();
            else if (Mathf.Abs(angle) <= 2f)
                leverAudioSource.Stop();
        }

        if (!leverLocked && leverGrab.isSelected && angle < openTriggerAngle)
        {
            leverLocked = true;
            ToggleDoor();
        }

        if (leverLocked && angle > resetAngle)
        {
            leverLocked = false;
        }
    }
}
