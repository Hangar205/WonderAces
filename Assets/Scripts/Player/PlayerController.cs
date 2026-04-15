using UnityEngine;

/// <summary>
/// Controlador del jugador para WonderAces.
/// Dos modos: CAMINAR en el suelo y VOLAR sobre el terreno.
/// En el suelo: gravedad, caminar con left stick, saltar para volar.
/// En el aire: vuelo libre estilo Star Fox, RT = acelerar.
/// Usa API de Unity 6: linearVelocity, linearDamping.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Movimiento en Suelo")]
    [Tooltip("Velocidad al caminar")]
    public float walkSpeed = 8f;
    [Tooltip("Velocidad al correr (con boost)")]
    public float runSpeed = 14f;
    [Tooltip("Fuerza de gravedad")]
    public float gravity = 20f;
    [Tooltip("Fuerza del salto (inicia vuelo)")]
    public float jumpForce = 12f;
    [Tooltip("Velocidad de rotación al caminar")]
    public float groundYawSpeed = 120f;

    [Header("Vuelo")]
    [Tooltip("Velocidad máxima en vuelo")]
    public float maxFlightSpeed = 30f;
    [Tooltip("Fuerza de aceleración en vuelo")]
    public float thrustForce = 22f;
    [Tooltip("Fuerza de frenado en vuelo")]
    public float brakeForce = 20f;
    [Tooltip("Fuerza de strafe en vuelo")]
    public float strafeForce = 15f;
    [Tooltip("Desaceleración al soltar acelerador")]
    public float flightDamping = 4f;
    [Tooltip("Multiplicador de boost")]
    public float boostMultiplier = 2f;

    [Header("Rotación en Vuelo")]
    public float yawSpeed = 85f;
    public float pitchSpeed = 85f;
    public float rollSpeed = 60f;
    public float maxBankAngle = 30f;
    public float bankSmooth = 5f;

    [Header("Detección de Suelo")]
    [Tooltip("Distancia de raycast para detectar el suelo")]
    public float groundCheckDist = 1.5f;
    [Tooltip("Altura mínima sobre el suelo antes de aterrizar")]
    public float landingHeight = 2f;

    [Header("Colisión")]
    public float bounceForce = 10f;
    public float stunDuration = 0.3f;

    [Header("Límites del Terreno")]
    public float terrainSize = 500f;
    public float terrainMargin = 10f;
    public float maxAltitude = 100f;

    [Header("Espada")]
    public float swordRange = 3f;
    public int swordDamage = 3;
    public float swordCooldown = 0.5f;

    // Estado
    private Rigidbody rb;
    private AngelWarrior angel;
    private bool isGrounded = false;
    private bool isFlying = false;
    private float currentBankAngle = 0f;
    private float stunTimer = 0f;
    private bool isStunned = false;
    private float swordTimer = 0f;
    private Vector3 groundNormal = Vector3.up;
    private float verticalVelocity = 0f;

    // Inputs
    [HideInInspector] public float currentSpeed;
    [HideInInspector] public float inputYaw;
    [HideInInspector] public float inputPitch;
    [HideInInspector] public float inputThrottle;
    [HideInInspector] public float inputBrake;
    [HideInInspector] public float inputStrafeH;
    [HideInInspector] public float inputStrafeV;
    [HideInInspector] public float inputMoveH; // Movimiento horizontal en suelo
    [HideInInspector] public float inputMoveV; // Movimiento vertical en suelo

    private const float STICK_DEADZONE = 0.2f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false; // Gravedad manual para mejor control
        rb.linearDamping = 1f;
        rb.angularDamping = 4f;
        rb.constraints = RigidbodyConstraints.FreezeRotation; // Rotación manual

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

        CheckGround();
        ReadInput();
        HandleJumpInput();
        HandleSwordInput();
        UpdateBankAnimation();
        currentSpeed = rb.linearVelocity.magnitude;
    }

    void FixedUpdate()
    {
        if (isStunned) return;

        if (isFlying)
        {
            ApplyFlightMovement();
            ApplyFlightRotation();
        }
        else
        {
            ApplyGroundMovement();
            ApplyGroundRotation();
            ApplyGravity();
        }
        EnforceTerrainLimits();
    }

    void OnCollisionEnter(Collision collision)
    {
        // No rebotar contra el suelo (terreno)
        if (collision.gameObject.GetComponent<Terrain>() != null) return;
        if (collision.gameObject.GetComponent<TerrainCollider>() != null) return;

        // Rebotar contra otros objetos (árboles, edificios)
        if (collision.relativeVelocity.magnitude < 2f) return;
        Vector3 bounceDir = collision.contacts[0].normal;
        rb.linearVelocity *= 0.3f; // Reducir velocidad en vez de parar completamente
        rb.AddForce(bounceDir * bounceForce * 0.5f, ForceMode.VelocityChange);
        isStunned = true;
        stunTimer = stunDuration;
    }

    /// <summary>
    /// Detecta si el personaje está en el suelo usando raycast.
    /// </summary>
    private void CheckGround()
    {
        RaycastHit hit;
        if (Physics.Raycast(transform.position, Vector3.down, out hit, groundCheckDist))
        {
            groundNormal = hit.normal;
            float heightAboveGround = hit.distance;

            if (!isFlying && heightAboveGround < 0.3f)
            {
                isGrounded = true;
            }
            else if (isFlying && heightAboveGround < landingHeight
                     && rb.linearVelocity.y <= 0f
                     && inputThrottle < 0.05f)
            {
                // Aterrizar si está volando bajo, cayendo, y sin acelerador
                isFlying = false;
                isGrounded = true;
                // Alinear al suelo
                verticalVelocity = 0f;
            }
        }
        else
        {
            isGrounded = false;
            if (!isFlying)
            {
                // Si no hay suelo debajo y no está volando, activar vuelo
                isFlying = true;
            }
        }
    }

    /// <summary>
    /// Detecta salto (Space/A) para iniciar vuelo.
    /// </summary>
    private void HandleJumpInput()
    {
        bool jumpPressed = Input.GetKeyDown(KeyCode.Space)
            || Input.GetKeyDown(KeyCode.JoystickButton0)
            || Input.GetKeyDown(KeyCode.JoystickButton16);

        if (jumpPressed && isGrounded && !isFlying)
        {
            isFlying = true;
            isGrounded = false;
            verticalVelocity = jumpForce;
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);
        }
    }

    private void ReadInput()
    {
        // --- YAW: A/D + Left Stick X ---
        inputYaw = 0f;
        if (Input.GetKey(KeyCode.A)) inputYaw = -1f;
        if (Input.GetKey(KeyCode.D)) inputYaw = 1f;
        float stickX = GetAxisSafe("Horizontal");
        if (Mathf.Abs(stickX) > STICK_DEADZONE && Mathf.Abs(stickX) > Mathf.Abs(inputYaw))
            inputYaw = stickX;

        // --- PITCH (solo vuelo): Flechas + Left Stick Y ---
        inputPitch = 0f;
        if (Input.GetKey(KeyCode.UpArrow)) inputPitch = -1f;
        if (Input.GetKey(KeyCode.DownArrow)) inputPitch = 1f;
        float stickY = GetAxisSafe("Joy1Axis2");
        if (Mathf.Abs(stickY) > STICK_DEADZONE && Mathf.Abs(stickY) > Mathf.Abs(inputPitch))
            inputPitch = -stickY;

        // --- MOVIMIENTO EN SUELO: Left Stick / WASD ---
        inputMoveH = inputYaw; // Reutilizar para dirección
        inputMoveV = 0f;
        if (Input.GetKey(KeyCode.W)) inputMoveV = 1f;
        if (Input.GetKey(KeyCode.S)) inputMoveV = -1f;
        float stickMoveV = GetAxisSafe("Vertical");
        if (Mathf.Abs(stickMoveV) > STICK_DEADZONE && Mathf.Abs(stickMoveV) > Mathf.Abs(inputMoveV))
            inputMoveV = stickMoveV;

        // --- THROTTLE (vuelo): W + RT ---
        inputThrottle = 0f;
        if (isFlying && Input.GetKey(KeyCode.W)) inputThrottle = 1f;
        float rt = GetTriggerValue("Joy1Axis6");
        if (rt > inputThrottle) inputThrottle = rt;

        // --- BRAKE (vuelo): S + LT ---
        inputBrake = 0f;
        if (isFlying && Input.GetKey(KeyCode.S)) inputBrake = 1f;
        float lt = GetTriggerValue("Joy1Axis3");
        if (lt > inputBrake) inputBrake = lt;

        // --- STRAFE (vuelo, Right Stick) ---
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

    // ==================== MODO SUELO ====================

    /// <summary>
    /// Movimiento en el suelo: caminar/correr con left stick.
    /// W/Left Stick adelante = avanzar, A/D = girar.
    /// </summary>
    private void ApplyGroundMovement()
    {
        bool isBoosting = Input.GetKey(KeyCode.LeftShift)
            || Input.GetKey(KeyCode.JoystickButton2)
            || Input.GetKey(KeyCode.JoystickButton18);

        float speed = isBoosting ? runSpeed : walkSpeed;

        // Movimiento relativo a la dirección del personaje
        Vector3 moveDir = transform.forward * inputMoveV + transform.right * inputMoveH * 0.5f;
        moveDir = Vector3.ProjectOnPlane(moveDir, groundNormal).normalized;

        if (moveDir.magnitude > 0.1f)
        {
            Vector3 targetVelocity = moveDir * speed;
            // Mantener velocidad vertical actual (gravedad)
            targetVelocity.y = rb.linearVelocity.y;
            rb.linearVelocity = Vector3.Lerp(rb.linearVelocity, targetVelocity, 10f * Time.fixedDeltaTime);
        }
        else
        {
            // Frenar horizontalmente cuando no hay input
            Vector3 vel = rb.linearVelocity;
            vel.x = Mathf.Lerp(vel.x, 0f, 8f * Time.fixedDeltaTime);
            vel.z = Mathf.Lerp(vel.z, 0f, 8f * Time.fixedDeltaTime);
            rb.linearVelocity = vel;
        }
    }

    /// <summary>
    /// Rotación en suelo: solo yaw (girar izquierda/derecha).
    /// El personaje se mantiene erguido.
    /// </summary>
    private void ApplyGroundRotation()
    {
        float yaw = inputYaw * groundYawSpeed * Time.fixedDeltaTime;
        transform.Rotate(0f, yaw, 0f, Space.World);

        // Mantener erguido (alinear al suelo)
        Quaternion targetRot = Quaternion.FromToRotation(transform.up, groundNormal) * transform.rotation;
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, 10f * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Aplica gravedad manual cuando está en el suelo o cayendo.
    /// </summary>
    private void ApplyGravity()
    {
        if (!isGrounded)
        {
            rb.AddForce(Vector3.down * gravity, ForceMode.Acceleration);
        }
        else
        {
            // En el suelo: cancelar velocidad vertical negativa
            if (rb.linearVelocity.y < 0f)
            {
                Vector3 vel = rb.linearVelocity;
                vel.y = 0f;
                rb.linearVelocity = vel;
            }
        }
    }

    // ==================== MODO VUELO ====================

    /// <summary>
    /// Movimiento en vuelo: acelerador con inercia, strafe, frenado.
    /// </summary>
    private void ApplyFlightMovement()
    {
        bool isBoosting = Input.GetKey(KeyCode.LeftShift)
            || Input.GetKey(KeyCode.JoystickButton2)
            || Input.GetKey(KeyCode.JoystickButton18);
        float speedLimit = isBoosting ? maxFlightSpeed * boostMultiplier : maxFlightSpeed;

        bool anyInput = inputThrottle > 0.05f
            || Mathf.Abs(inputStrafeH) > 0.05f
            || Mathf.Abs(inputStrafeV) > 0.05f;
        rb.linearDamping = anyInput ? 1f : flightDamping;

        // Acelerador
        if (inputThrottle > 0.05f && rb.linearVelocity.magnitude < speedLimit)
            rb.AddForce(transform.forward * inputThrottle * thrustForce, ForceMode.Acceleration);

        // Strafe
        if (Mathf.Abs(inputStrafeH) > 0.05f)
            rb.AddForce(transform.right * inputStrafeH * strafeForce, ForceMode.Acceleration);
        if (Mathf.Abs(inputStrafeV) > 0.05f)
            rb.AddForce(transform.up * inputStrafeV * strafeForce, ForceMode.Acceleration);

        // Freno
        if (inputBrake > 0.05f)
        {
            if (rb.linearVelocity.magnitude > 0.5f)
                rb.AddForce(-rb.linearVelocity.normalized * inputBrake * brakeForce, ForceMode.Acceleration);
            else
                rb.linearVelocity = Vector3.zero;
        }

        // Gravedad suave en vuelo (el personaje cae lentamente si no acelera)
        if (inputThrottle < 0.05f && inputStrafeV < 0.05f)
            rb.AddForce(Vector3.down * gravity * 0.3f, ForceMode.Acceleration);

        // Limpiar velocidad residual
        if (!anyInput && inputBrake < 0.05f && rb.linearVelocity.magnitude < 0.3f)
            rb.linearVelocity = Vector3.zero;
    }

    /// <summary>
    /// Rotación en vuelo: pitch/yaw/roll libre.
    /// </summary>
    private void ApplyFlightRotation()
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

    // ==================== LÍMITES ====================

    /// <summary>
    /// Mantiene al jugador dentro de los límites del terreno.
    /// </summary>
    private void EnforceTerrainLimits()
    {
        Vector3 pos = transform.position;

        // Limitar XZ al terreno
        pos.x = Mathf.Clamp(pos.x, terrainMargin, terrainSize - terrainMargin);
        pos.z = Mathf.Clamp(pos.z, terrainMargin, terrainSize - terrainMargin);

        // Limitar altura máxima
        if (pos.y > maxAltitude)
        {
            pos.y = maxAltitude;
            Vector3 vel = rb.linearVelocity;
            if (vel.y > 0) vel.y = 0;
            rb.linearVelocity = vel;
        }

        // No hundirse bajo el terreno
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
        {
            float terrainHeight = terrain.SampleHeight(pos) + terrain.transform.position.y;
            if (pos.y < terrainHeight + 0.5f)
            {
                pos.y = terrainHeight + 0.5f;
                if (rb.linearVelocity.y < 0)
                {
                    Vector3 vel = rb.linearVelocity;
                    vel.y = 0;
                    rb.linearVelocity = vel;
                }
            }
        }

        transform.position = pos;
    }

    private void UpdateBankAnimation()
    {
        if (!isFlying) { currentBankAngle = Mathf.Lerp(currentBankAngle, 0f, bankSmooth * Time.deltaTime); return; }
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
