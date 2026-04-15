using UnityEngine;

/// <summary>
/// Controlador del jugador para WonderAces.
/// Vuelo libre estilo Star Fox all-range mode.
/// RT = acelerar, Left Stick = dirección, A = disparar, B/X = espada.
/// Misma base de controles que OrbitaVertigo adaptada a fantasía.
/// Usa API de Unity 6: linearVelocity, linearDamping.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Velocidad")]
    public float maxSpeed = 30f;
    public float thrustForce = 22f;
    public float brakeForce = 20f;
    public float strafeForce = 15f;
    public float inertiaDecay = 6f;
    public float boostMultiplier = 2f;

    [Header("Rotación")]
    public float yawSpeed = 85f;
    public float pitchSpeed = 85f;
    public float rollSpeed = 60f;

    [Header("Bank Animation")]
    public float maxBankAngle = 30f;
    public float bankSmooth = 5f;

    [Header("Colisión")]
    public float bounceForce = 10f;
    public float stunDuration = 0.3f;

    [Header("Arena")]
    public float arenaRadius = 120f;
    public float wallPushForce = 12f;

    [Header("Espada")]
    [Tooltip("Radio de daño del ataque con espada")]
    public float swordRange = 3f;
    [Tooltip("Daño del ataque con espada")]
    public int swordDamage = 3;
    [Tooltip("Cooldown entre ataques de espada")]
    public float swordCooldown = 0.5f;

    // Componentes
    private Rigidbody rb;
    private AngelWarrior angel;
    private float currentBankAngle = 0f;
    private float stunTimer = 0f;
    private bool isStunned = false;
    private float swordTimer = 0f;

    // Inputs
    [HideInInspector] public float currentSpeed;
    [HideInInspector] public float inputYaw;
    [HideInInspector] public float inputPitch;
    [HideInInspector] public float inputThrottle;
    [HideInInspector] public float inputBrake;
    [HideInInspector] public float inputStrafeH;
    [HideInInspector] public float inputStrafeV;

    private const float STICK_DEADZONE = 0.2f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = inertiaDecay;
        rb.angularDamping = 4f;

        // Buscar el modelo del ángel
        angel = GetComponent<AngelWarrior>();
    }

    void Update()
    {
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f) isStunned = false;
        }

        if (swordTimer > 0f) swordTimer -= Time.deltaTime;

        ReadInput();
        HandleSwordInput();
        UpdateBankAnimation();
        currentSpeed = rb.linearVelocity.magnitude;
    }

    void FixedUpdate()
    {
        if (!isStunned)
        {
            ApplyThrottle();
            ApplyRotation();
        }
        EnforceSphereLimit();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude < 2f) return;
        Vector3 bounceDir = collision.contacts[0].normal;
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(bounceDir * bounceForce, ForceMode.VelocityChange);
        isStunned = true;
        stunTimer = stunDuration;
    }

    private void ReadInput()
    {
        // YAW
        inputYaw = 0f;
        if (Input.GetKey(KeyCode.A)) inputYaw = -1f;
        if (Input.GetKey(KeyCode.D)) inputYaw = 1f;
        float stickX = GetAxisSafe("Horizontal");
        if (Mathf.Abs(stickX) > STICK_DEADZONE && Mathf.Abs(stickX) > Mathf.Abs(inputYaw))
            inputYaw = stickX;

        // PITCH
        inputPitch = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) inputPitch = -1f;
        if (Input.GetKey(KeyCode.DownArrow)) inputPitch = 1f;
        float stickY = GetAxisSafe("Joy1Axis2");
        if (Mathf.Abs(stickY) > STICK_DEADZONE && Mathf.Abs(stickY) > Mathf.Abs(inputPitch))
            inputPitch = -stickY;

        // THROTTLE
        inputThrottle = 0f;
        if (Input.GetKey(KeyCode.W)) inputThrottle = 1f;
        float rt = GetTriggerValue("Joy1Axis6");
        if (rt > inputThrottle) inputThrottle = rt;

        // BRAKE
        inputBrake = 0f;
        if (Input.GetKey(KeyCode.S)) inputBrake = 1f;
        float lt = GetTriggerValue("Joy1Axis3");
        if (lt > inputBrake) inputBrake = lt;

        // STRAFE (Right Stick)
        inputStrafeH = 0f;
        inputStrafeV = 0f;
        float rsX = GetAxisSafe("Joy1Axis4");
        float rsY = GetAxisSafe("Joy1Axis5");
        if (Mathf.Abs(rsX) > STICK_DEADZONE) inputStrafeH = rsX;
        if (Mathf.Abs(rsY) > STICK_DEADZONE) inputStrafeV = -rsY;
        if (Input.GetKey(KeyCode.Z)) inputStrafeH = -1f;
        if (Input.GetKey(KeyCode.X)) inputStrafeH = 1f;
        if (Input.GetKey(KeyCode.R)) inputStrafeV = 1f;
        if (Input.GetKey(KeyCode.F)) inputStrafeV = -1f;
    }

    /// <summary>
    /// Detecta input de espada y activa el swing.
    /// Xbox B o X, teclado E o Click derecho.
    /// </summary>
    private void HandleSwordInput()
    {
        bool swordInput = Input.GetKeyDown(KeyCode.Mouse1) // Click derecho
            || Input.GetKeyDown(KeyCode.C)
            || Input.GetKeyDown(KeyCode.JoystickButton1)   // B
            || Input.GetKeyDown(KeyCode.JoystickButton17)  // B alt
            || Input.GetKeyDown(KeyCode.JoystickButton2)   // X
            || Input.GetKeyDown(KeyCode.JoystickButton18); // X alt

        if (swordInput && swordTimer <= 0f && angel != null)
        {
            angel.SwingSword();
            swordTimer = swordCooldown;
            // TODO: detectar enemigos en rango y aplicar daño
        }
    }

    private void ApplyThrottle()
    {
        float speedLimit = Input.GetKey(KeyCode.LeftShift) ? maxSpeed * boostMultiplier : maxSpeed;

        bool anyInput = inputThrottle > 0.05f
            || Mathf.Abs(inputStrafeH) > 0.05f
            || Mathf.Abs(inputStrafeV) > 0.05f;
        rb.linearDamping = anyInput ? 1f : inertiaDecay;

        if (inputThrottle > 0.05f && rb.linearVelocity.magnitude < speedLimit)
        {
            rb.AddForce(transform.forward * inputThrottle * thrustForce, ForceMode.Acceleration);
        }

        if (Mathf.Abs(inputStrafeH) > 0.05f)
            rb.AddForce(transform.right * inputStrafeH * strafeForce, ForceMode.Acceleration);
        if (Mathf.Abs(inputStrafeV) > 0.05f)
            rb.AddForce(transform.up * inputStrafeV * strafeForce, ForceMode.Acceleration);

        if (inputBrake > 0.05f)
        {
            if (rb.linearVelocity.magnitude > 0.5f)
                rb.AddForce(-rb.linearVelocity.normalized * inputBrake * brakeForce, ForceMode.Acceleration);
            else
                rb.linearVelocity = Vector3.zero;
        }

        if (!anyInput && inputBrake < 0.05f && rb.linearVelocity.magnitude < 0.3f)
            rb.linearVelocity = Vector3.zero;
    }

    private void ApplyRotation()
    {
        float yaw = inputYaw * yawSpeed * Time.fixedDeltaTime;
        float pitch = inputPitch * pitchSpeed * Time.fixedDeltaTime;

        float roll = 0f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.JoystickButton4)
            || Input.GetKey(KeyCode.JoystickButton13)) roll = 1f;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.JoystickButton5)
            || Input.GetKey(KeyCode.JoystickButton14)) roll = -1f;
        roll *= rollSpeed * Time.fixedDeltaTime;

        transform.Rotate(pitch, yaw, roll, Space.Self);
    }

    /// <summary>
    /// Límite esférico: el jugador no puede salir de la arena esférica.
    /// </summary>
    private void EnforceSphereLimit()
    {
        float dist = transform.position.magnitude;
        float limit = arenaRadius - 5f;

        if (dist > limit)
        {
            Vector3 pushDir = -transform.position.normalized;
            float excess = dist - limit;
            rb.AddForce(pushDir * excess * wallPushForce, ForceMode.Acceleration);

            // Clamp duro
            if (dist > arenaRadius)
            {
                transform.position = transform.position.normalized * arenaRadius;
            }
        }
    }

    private void UpdateBankAnimation()
    {
        // El bank se aplica al modelo visual del ángel si existe
        currentBankAngle = Mathf.Lerp(currentBankAngle, -inputYaw * maxBankAngle, bankSmooth * Time.deltaTime);
    }

    private float GetTriggerValue(string axisName)
    {
        float raw = GetAxisSafe(axisName);
        if (raw < -0.5f) return 0f;
        float value = Mathf.Abs(raw);
        if (value < 0.25f) return 0f;
        return Mathf.Clamp01(value);
    }

    private float GetAxisSafe(string axisName)
    {
        try { return Input.GetAxis(axisName); }
        catch { return 0f; }
    }
}
