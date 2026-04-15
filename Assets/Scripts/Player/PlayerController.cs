using UnityEngine;

/// <summary>
/// Controlador del jugador para WonderAces.
/// Tres estados: SUELO, VUELO AVIÓN, VUELO JOUST (pendiente).
/// Suelo: caminar/correr, saltar para despegar.
/// Avión: alas extendidas, necesita velocidad para sustentación,
///   pitch/roll para maniobrar, pierde altura si va lento (stall).
/// Usa API de Unity 6: linearVelocity, linearDamping.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    // ==================== MODOS DE VUELO ====================
    public enum FlightMode { Ground, Airplane, Joust }

    [Header("Estado")]
    [Tooltip("Modo de vuelo actual")]
    public FlightMode currentMode = FlightMode.Ground;

    // ==================== SUELO ====================
    [Header("Movimiento en Suelo")]
    public float walkSpeed = 8f;
    public float runSpeed = 14f;
    public float gravity = 20f;
    public float jumpForce = 12f;
    public float groundYawSpeed = 120f;

    // ==================== MODO AVIÓN ====================
    [Header("Modo Avión — Velocidad")]
    [Tooltip("Velocidad mínima para mantener sustentación (debajo = stall)")]
    public float stallSpeed = 8f;
    [Tooltip("Velocidad de crucero")]
    public float cruiseSpeed = 20f;
    [Tooltip("Velocidad máxima")]
    public float maxFlightSpeed = 40f;
    [Tooltip("Aceleración del motor")]
    public float engineForce = 15f;
    [Tooltip("Resistencia aerodinámica (frena naturalmente)")]
    public float dragCoefficient = 0.5f;
    [Tooltip("Multiplicador de boost")]
    public float boostMultiplier = 1.8f;

    [Header("Modo Avión — Sustentación")]
    [Tooltip("Fuerza de sustentación a velocidad de crucero")]
    public float liftForce = 22f;
    [Tooltip("Gravedad durante vuelo")]
    public float flightGravity = 12f;
    [Tooltip("Gravedad extra durante stall (caída rápida)")]
    public float stallGravity = 25f;

    [Header("Modo Avión — Control")]
    [Tooltip("Velocidad de pitch (nariz arriba/abajo)")]
    public float pitchSpeed = 60f;
    [Tooltip("Velocidad de roll (inclinación lateral)")]
    public float rollSpeed = 80f;
    [Tooltip("Velocidad de yaw (giro asistido por roll)")]
    public float yawFromRoll = 30f;
    [Tooltip("Ángulo máximo de bank visual")]
    public float maxBankAngle = 45f;

    // ==================== GENERAL ====================
    [Header("Detección de Suelo")]
    public float groundCheckDist = 1.5f;
    public float landingSpeed = 5f;

    [Header("Colisión")]
    public float bounceForce = 8f;
    public float stunDuration = 0.3f;

    [Header("Límites del Terreno")]
    public float terrainSize = 500f;
    public float terrainMargin = 10f;
    public float maxAltitude = 120f;
    public float minFlightAltitude = 3f;

    [Header("Espada")]
    public float swordRange = 3f;
    public int swordDamage = 3;
    public float swordCooldown = 0.5f;

    // Componentes
    private Rigidbody rb;
    private AngelWarrior angel;
    private bool isGrounded = false;
    private Vector3 groundNormal = Vector3.up;
    private float stunTimer = 0f;
    private bool isStunned = false;
    private float swordTimer = 0f;

    // Inputs
    [HideInInspector] public float currentSpeed;
    [HideInInspector] public float inputYaw;
    [HideInInspector] public float inputPitch;
    [HideInInspector] public float inputRoll;
    [HideInInspector] public float inputThrottle;
    [HideInInspector] public float inputBrake;
    [HideInInspector] public float inputMoveH;
    [HideInInspector] public float inputMoveV;

    private const float STICK_DEADZONE = 0.2f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 0.1f; // Bajo — usamos drag aerodinámico manual
        rb.angularDamping = 4f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        angel = GetComponent<AngelWarrior>();
        currentMode = FlightMode.Ground;
    }

    void Update()
    {
        if (isStunned)
        {
            stunTimer -= Time.deltaTime;
            if (stunTimer <= 0f) isStunned = false;
        }
        if (swordTimer > 0f) swordTimer -= Time.deltaTime;

        CheckGround();
        ReadInput();
        HandleModeSwitch();
        HandleSwordInput();
        currentSpeed = rb.linearVelocity.magnitude;

        // Informar al modelo del modo actual
        if (angel != null)
        {
            angel.SetFlightMode(currentMode == FlightMode.Airplane);
        }
    }

    void FixedUpdate()
    {
        if (isStunned) return;

        switch (currentMode)
        {
            case FlightMode.Ground:
                ApplyGroundMovement();
                ApplyGroundRotation();
                ApplyGravity();
                break;
            case FlightMode.Airplane:
                ApplyAirplanePhysics();
                ApplyAirplaneRotation();
                break;
        }
        EnforceTerrainLimits();
    }

    void OnCollisionEnter(Collision collision)
    {
        // No rebotar contra el terreno
        if (collision.gameObject.GetComponent<Terrain>() != null) return;
        if (collision.gameObject.GetComponent<TerrainCollider>() != null) return;

        if (collision.relativeVelocity.magnitude < 2f) return;
        rb.linearVelocity *= 0.3f;
        rb.AddForce(collision.contacts[0].normal * bounceForce * 0.5f, ForceMode.VelocityChange);
        isStunned = true;
        stunTimer = stunDuration;
    }

    // ==================== INPUT ====================

    private void ReadInput()
    {
        // YAW / movimiento horizontal: A/D + Left Stick X
        inputYaw = 0f;
        if (Input.GetKey(KeyCode.A)) inputYaw = -1f;
        if (Input.GetKey(KeyCode.D)) inputYaw = 1f;
        float stickX = GetAxisSafe("Horizontal");
        if (Mathf.Abs(stickX) > STICK_DEADZONE && Mathf.Abs(stickX) > Mathf.Abs(inputYaw))
            inputYaw = stickX;

        // PITCH: flechas + Left Stick Y
        inputPitch = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) inputPitch = -1f;  // Nariz abajo
        if (Input.GetKey(KeyCode.DownArrow)) inputPitch = 1f; // Nariz arriba
        float stickY = GetAxisSafe("Joy1Axis2");
        if (Mathf.Abs(stickY) > STICK_DEADZONE && Mathf.Abs(stickY) > Mathf.Abs(inputPitch))
            inputPitch = stickY; // Stick arriba = nariz abajo (estilo avión)

        // ROLL: Q/E + LB/RB — en modo avión, Left Stick X también es roll
        inputRoll = 0f;
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.JoystickButton4)
            || Input.GetKey(KeyCode.JoystickButton13)) inputRoll = -1f;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.JoystickButton5)
            || Input.GetKey(KeyCode.JoystickButton14)) inputRoll = 1f;
        // En modo avión, el stick horizontal también controla roll
        if (currentMode == FlightMode.Airplane && Mathf.Abs(inputRoll) < 0.1f)
        {
            if (Mathf.Abs(inputYaw) > STICK_DEADZONE) inputRoll = inputYaw;
        }

        // MOVIMIENTO SUELO: W/S + Left Stick Y
        inputMoveV = 0f;
        inputMoveH = inputYaw;
        if (Input.GetKey(KeyCode.W)) inputMoveV = 1f;
        if (Input.GetKey(KeyCode.S)) inputMoveV = -1f;
        float stickMoveV = GetAxisSafe("Vertical");
        if (Mathf.Abs(stickMoveV) > STICK_DEADZONE && Mathf.Abs(stickMoveV) > Mathf.Abs(inputMoveV))
            inputMoveV = stickMoveV;

        // THROTTLE: W (en vuelo) + RT
        inputThrottle = 0f;
        if (currentMode == FlightMode.Airplane && Input.GetKey(KeyCode.W)) inputThrottle = 1f;
        float rt = GetTriggerValue("Joy1Axis6");
        if (rt > inputThrottle) inputThrottle = rt;

        // BRAKE: S (en vuelo) + LT
        inputBrake = 0f;
        if (currentMode == FlightMode.Airplane && Input.GetKey(KeyCode.S)) inputBrake = 1f;
        float lt = GetTriggerValue("Joy1Axis3");
        if (lt > inputBrake) inputBrake = lt;
    }

    /// <summary>
    /// Maneja transición entre modos:
    /// Suelo → Avión: Space/A para saltar y despegar
    /// Avión → Suelo: aterrizar automáticamente cuando está bajo y lento
    /// </summary>
    private void HandleModeSwitch()
    {
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.JoystickButton0)
            || Input.GetKeyDown(KeyCode.JoystickButton16);

        if (currentMode == FlightMode.Ground && jumpPressed && isGrounded)
        {
            // Despegar: saltar y entrar en modo avión
            currentMode = FlightMode.Airplane;
            isGrounded = false;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            // Impulso inicial hacia adelante para tener sustentación
            rb.AddForce(transform.forward * stallSpeed, ForceMode.VelocityChange);
        }
        else if (currentMode == FlightMode.Airplane && isGrounded
                 && currentSpeed < landingSpeed && inputThrottle < 0.1f)
        {
            // Aterrizar: velocidad baja, cerca del suelo, sin acelerador
            currentMode = FlightMode.Ground;
            // Enderezar al personaje
            Vector3 euler = transform.eulerAngles;
            transform.rotation = Quaternion.Euler(0f, euler.y, 0f);
        }
    }

    private void HandleSwordInput()
    {
        bool swordInput = Input.GetKeyDown(KeyCode.Mouse1)
            || Input.GetKeyDown(KeyCode.C)
            || Input.GetKeyDown(KeyCode.JoystickButton1)
            || Input.GetKeyDown(KeyCode.JoystickButton17)
            || Input.GetKeyDown(KeyCode.JoystickButton2)
            || Input.GetKeyDown(KeyCode.JoystickButton18);

        if (swordInput && swordTimer <= 0f && angel != null)
        {
            angel.SwingSword();
            swordTimer = swordCooldown;
        }
    }

    private void CheckGround()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, groundCheckDist))
        {
            groundNormal = hit.normal;
            isGrounded = hit.distance < 0.5f;
        }
        else
        {
            isGrounded = false;
        }
    }

    // ==================== MODO SUELO ====================

    private void ApplyGroundMovement()
    {
        bool isBoosting = Input.GetKey(KeyCode.LeftShift)
            || Input.GetKey(KeyCode.JoystickButton2)
            || Input.GetKey(KeyCode.JoystickButton18);
        float speed = isBoosting ? runSpeed : walkSpeed;

        Vector3 moveDir = transform.forward * inputMoveV + transform.right * inputMoveH * 0.5f;
        moveDir = Vector3.ProjectOnPlane(moveDir, groundNormal).normalized;

        if (moveDir.magnitude > 0.1f)
        {
            Vector3 targetVel = moveDir * speed;
            targetVel.y = rb.linearVelocity.y;
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVel, 10f * Time.fixedDeltaTime);
        }
        else
        {
            Vector3 vel = rb.linearVelocity;
            vel.x = Mathf.Lerp(vel.x, 0f, 8f * Time.fixedDeltaTime);
            vel.z = Mathf.Lerp(vel.z, 0f, 8f * Time.fixedDeltaTime);
            rb.linearVelocity = vel;
        }
    }

    private void ApplyGroundRotation()
    {
        float yaw = inputYaw * groundYawSpeed * Time.fixedDeltaTime;
        transform.Rotate(0f, yaw, 0f, Space.World);

        // Mantener erguido
        Quaternion target = Quaternion.FromToRotation(transform.up, groundNormal) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 10f * Time.fixedDeltaTime);
    }

    private void ApplyGravity()
    {
        if (!isGrounded)
        {
            rb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
        }
        else if (rb.linearVelocity.y < 0f)
        {
            Vector3 vel = rb.linearVelocity;
            vel.y = 0f;
            rb.linearVelocity = vel;
        }
    }

    // ==================== MODO AVIÓN ====================

    /// <summary>
    /// Física de vuelo tipo avión:
    /// - Motor empuja hacia adelante (transform.forward)
    /// - Sustentación depende de la velocidad (transform.up)
    /// - Gravedad tira hacia abajo siempre
    /// - Drag aerodinámico frena proporcionalmente al cuadrado de la velocidad
    /// - Stall: si velocidad < stallSpeed, cae rápidamente
    /// </summary>
    private void ApplyAirplanePhysics()
    {
        bool isBoosting = Input.GetKey(KeyCode.LeftShift)
            || Input.GetKey(KeyCode.JoystickButton2)
            || Input.GetKey(KeyCode.JoystickButton18);
        float maxSpeed = isBoosting ? maxFlightSpeed * boostMultiplier : maxFlightSpeed;

        // --- MOTOR: empuje hacia adelante ---
        if (inputThrottle > 0.05f && currentSpeed < maxSpeed)
        {
            rb.AddForce(transform.forward * inputThrottle * engineForce, ForceMode.Acceleration);
        }

        // --- FRENO AERODINÁMICO ---
        if (inputBrake > 0.05f)
        {
            rb.AddForce(-rb.linearVelocity.normalized * inputBrake * engineForce * 0.8f, ForceMode.Acceleration);
        }

        // --- DRAG: resistencia proporcional a velocidad ---
        if (currentSpeed > 0.5f)
        {
            float drag = dragCoefficient * currentSpeed * 0.1f;
            rb.AddForce(-rb.linearVelocity.normalized * drag, ForceMode.Acceleration);
        }

        // --- SUSTENTACIÓN: fuerza hacia arriba proporcional a velocidad ---
        float speedRatio = Mathf.Clamp01(currentSpeed / cruiseSpeed);
        float lift = liftForce * speedRatio * speedRatio; // Sustentación cuadrática
        rb.AddForce(transform.up * lift, ForceMode.Acceleration);

        // --- GRAVEDAD ---
        bool isStalling = currentSpeed < stallSpeed;
        float grav = isStalling ? stallGravity : flightGravity;
        rb.AddForce(Vector3.down * grav, ForceMode.Acceleration);

        // --- ALINEAR VELOCIDAD con dirección de la nave (el avión va donde apunta) ---
        if (currentSpeed > 2f)
        {
            Vector3 forwardVel = Vector3.Project(rb.linearVelocity, transform.forward);
            Vector3 sideVel = rb.linearVelocity - forwardVel;
            // Reducir velocidad lateral gradualmente (el avión no derrapa mucho)
            rb.linearVelocity = forwardVel + sideVel * 0.95f;
        }
    }

    /// <summary>
    /// Rotación en modo avión:
    /// - Left Stick X / A,D = Roll (inclinar alas)
    /// - Left Stick Y / Flechas = Pitch (nariz arriba/abajo)
    /// - El yaw es automático: se genera por la inclinación del roll (giro coordinado)
    /// </summary>
    private void ApplyAirplaneRotation()
    {
        // Pitch: nariz arriba/abajo
        float pitch = inputPitch * pitchSpeed * Time.fixedDeltaTime;

        // Roll: inclinar alas
        float roll = inputRoll * rollSpeed * Time.fixedDeltaTime;

        // Yaw automático por roll: cuando el avión está inclinado, gira naturalmente
        float currentRollAngle = transform.eulerAngles.z;
        if (currentRollAngle > 180f) currentRollAngle -= 360f;
        float autoYaw = -currentRollAngle / 180f * yawFromRoll * Time.fixedDeltaTime;

        transform.Rotate(pitch, autoYaw, -roll, Space.Self);
    }

    // ==================== LÍMITES ====================

    private void EnforceTerrainLimits()
    {
        Vector3 pos = transform.position;

        pos.x = Mathf.Clamp(pos.x, terrainMargin, terrainSize - terrainMargin);
        pos.z = Mathf.Clamp(pos.z, terrainMargin, terrainSize - terrainMargin);

        if (pos.y > maxAltitude)
        {
            pos.y = maxAltitude;
            if (rb.linearVelocity.y > 0)
            {
                Vector3 vel = rb.linearVelocity; vel.y = 0; rb.linearVelocity = vel;
            }
        }

        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            float terrainHeight = terrain.SampleHeight(pos) + terrain.transform.position.y;
            float minHeight = currentMode == FlightMode.Airplane ? terrainHeight + minFlightAltitude : terrainHeight + 0.5f;
            if (pos.y < minHeight)
            {
                pos.y = minHeight;
                if (rb.linearVelocity.y < 0)
                {
                    Vector3 vel = rb.linearVelocity; vel.y = 0; rb.linearVelocity = vel;
                }
            }
        }

        transform.position = pos;
    }

    // ==================== UTILIDADES ====================

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
