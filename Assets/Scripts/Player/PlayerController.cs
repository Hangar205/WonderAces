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
    [Tooltip("Fuerza del salto (5x la altura del personaje ~7.5 unidades)")]
    public float jumpForce = 18f;
    public float groundYawSpeed = 120f;

    // ==================== MODO AVIÓN (Star Fox Style) ====================
    [Header("Modo Avión — Velocidad (Star Fox)")]
    [Tooltip("Velocidad base — siempre avanza a esta velocidad")]
    public float cruiseSpeed = 18f;
    [Tooltip("Velocidad al acelerar con RT")]
    public float boostSpeed = 35f;
    [Tooltip("Velocidad al frenar con LT")]
    public float brakeSpeed = 8f;
    [Tooltip("Velocidad máxima absoluta")]
    public float maxFlightSpeed = 40f;
    [Tooltip("Qué tan rápido cambia entre velocidades")]
    public float speedTransition = 5f;
    [Tooltip("Gravedad suave durante vuelo")]
    public float flightGravity = 5f;

    [Header("Modo Avión — Control (Star Fox)")]
    [Tooltip("Velocidad de pitch (arriba = subir, abajo = bajar)")]
    public float pitchSpeed = 70f;
    [Tooltip("Velocidad de roll (inclinación lateral)")]
    public float rollSpeed = 90f;
    [Tooltip("Fuerza de giro automático por roll")]
    public float yawFromRoll = 40f;
    [Tooltip("Ángulo máximo de bank")]
    public float maxBankAngle = 50f;

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
    public float minFlightAltitude = 5f;

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

        // PITCH: flechas + Left Stick Y (Star Fox: arriba = subir)
        inputPitch = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) inputPitch = 1f;   // Arriba = subir
        if (Input.GetKey(KeyCode.DownArrow)) inputPitch = -1f; // Abajo = bajar
        float stickY = GetAxisSafe("Joy1Axis2");
        if (Mathf.Abs(stickY) > STICK_DEADZONE && Mathf.Abs(stickY) > Mathf.Abs(inputPitch))
            inputPitch = -stickY; // Stick arriba = subir (Star Fox style)

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
            targetSpeed = cruiseSpeed;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
            // Impulso frontal automático — en Star Fox siempre avanzas
            rb.linearVelocity = transform.forward * cruiseSpeed + Vector3.up * jumpForce * 0.6f;
        }
        else if (currentMode == FlightMode.Airplane && isGrounded
                 && currentSpeed < landingSpeed && inputBrake > 0.3f)
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
        // Raycast desde los pies del personaje (offset hacia abajo)
        Vector3 feetPos = transform.position + Vector3.down * 0.5f;
        if (Physics.Raycast(feetPos, Vector3.down, out hit, groundCheckDist))
        {
            groundNormal = hit.normal;
            isGrounded = hit.distance < 0.8f;
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

    // ==================== MODO AVIÓN (Star Fox Style) ====================

    // Velocidad objetivo actual (se interpola suavemente)
    private float targetSpeed;

    /// <summary>
    /// Física estilo Star Fox:
    /// - SIEMPRE avanza hacia adelante a velocidad base
    /// - RT = acelerar, LT = frenar
    /// - El pitch genera fuerza vertical directa (subir/bajar)
    /// - Gravedad suave compensada por sustentación base
    /// </summary>
    private void ApplyAirplanePhysics()
    {
        // Determinar velocidad objetivo según input
        if (inputThrottle > 0.1f)
            targetSpeed = Mathf.Lerp(cruiseSpeed, boostSpeed, inputThrottle);
        else if (inputBrake > 0.1f)
            targetSpeed = Mathf.Lerp(cruiseSpeed, brakeSpeed, inputBrake);
        else
            targetSpeed = cruiseSpeed;

        // Interpolar velocidad horizontal suavemente
        float currentForwardSpeed = Vector3.Dot(rb.linearVelocity, transform.forward);
        float newSpeed = Mathf.Lerp(currentForwardSpeed, targetSpeed, speedTransition * Time.fixedDeltaTime);

        // Velocidad horizontal: siempre hacia donde apunta la nariz (solo XZ)
        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        flatForward.Normalize();
        if (flatForward.sqrMagnitude < 0.01f) flatForward = Vector3.forward;

        Vector3 desiredVelocity = flatForward * newSpeed;

        // Velocidad vertical: generada por el pitch del personaje
        // Cuanto más inclinado hacia arriba, más sube. Hacia abajo, más baja.
        float pitchAngle = transform.eulerAngles.x;
        if (pitchAngle > 180f) pitchAngle -= 360f;
        // pitchAngle negativo = nariz arriba = subir
        float verticalForce = -pitchAngle / 45f * newSpeed * 0.6f;

        // Gravedad suave — compensada parcialmente por sustentación base
        float currentVertical = rb.linearVelocity.y;
        float targetVertical = verticalForce - flightGravity * 0.15f;
        desiredVelocity.y = Mathf.Lerp(currentVertical, targetVertical, 4f * Time.fixedDeltaTime);

        rb.linearVelocity = desiredVelocity;
    }

    /// <summary>
    /// Rotación estilo Star Fox (NO invertido):
    /// - Left Stick X / A,D = Roll (inclinar alas) → giro automático
    /// - Left Stick Y / Flechas = Pitch (arriba = subir, abajo = bajar)
    /// - Yaw automático proporcional al ángulo de roll
    /// - Sin roll input, las alas se nivelan solas
    /// </summary>
    private void ApplyAirplaneRotation()
    {
        // PITCH: arriba = nariz arriba (NO invertido, estilo Star Fox)
        float pitch = -inputPitch * pitchSpeed * Time.fixedDeltaTime;

        // ROLL: inclinar alas
        float roll = inputRoll * rollSpeed * Time.fixedDeltaTime;

        // Auto-nivelación del roll cuando no hay input
        if (Mathf.Abs(inputRoll) < 0.1f)
        {
            float currentRoll = transform.eulerAngles.z;
            if (currentRoll > 180f) currentRoll -= 360f;
            // Fuerza de corrección proporcional al ángulo actual
            roll = -currentRoll * 2f * Time.fixedDeltaTime;
        }

        // YAW automático por roll (giro coordinado estilo Star Fox)
        float rollAngle = transform.eulerAngles.z;
        if (rollAngle > 180f) rollAngle -= 360f;
        float autoYaw = -rollAngle / 90f * yawFromRoll * Time.fixedDeltaTime;

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
