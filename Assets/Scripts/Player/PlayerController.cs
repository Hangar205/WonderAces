using UnityEngine;

/// <summary>
/// Controlador estilo Star Fox 16-bit para WonderAces.
/// El personaje SIEMPRE vuela — no hay modo suelo ni salto.
/// Siempre avanza, pitch para subir/bajar, roll para girar.
/// Mundo abierto con altura limitada sobre el terreno.
/// Usa API de Unity 6: linearVelocity, linearDamping.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class PlayerController : MonoBehaviour
{
    [Header("Velocidad (Star Fox)")]
    [Tooltip("Velocidad base — siempre avanza")]
    public float cruiseSpeed = 18f;
    [Tooltip("Velocidad al acelerar con RT")]
    public float boostSpeed = 35f;
    [Tooltip("Velocidad al frenar con LT")]
    public float brakeSpeed = 8f;
    [Tooltip("Velocidad máxima absoluta")]
    public float maxFlightSpeed = 40f;
    [Tooltip("Qué tan rápido cambia entre velocidades")]
    public float speedTransition = 5f;

    [Header("Control (Star Fox)")]
    [Tooltip("Velocidad de pitch (arriba = subir)")]
    public float pitchSpeed = 70f;
    [Tooltip("Velocidad de roll")]
    public float rollSpeed = 90f;
    [Tooltip("Fuerza de giro automático por roll")]
    public float yawFromRoll = 40f;
    [Tooltip("Ángulo máximo de bank")]
    public float maxBankAngle = 50f;

    [Header("Física de Vuelo")]
    [Tooltip("Gravedad suave (mantiene al personaje descendiendo si no hace pitch up)")]
    public float flightGravity = 3f;

    [Header("Límites del Terreno")]
    public float terrainSize = 500f;
    public float terrainMargin = 10f;
    [Tooltip("Altura máxima absoluta")]
    public float maxAltitude = 100f;
    [Tooltip("Altura mínima sobre el terreno")]
    public float minAltitude = 5f;
    [Tooltip("Distancia al límite donde empieza a enderezarse")]
    public float levelOutDistance = 15f;
    [Tooltip("Velocidad de auto-enderezamiento")]
    public float levelOutSpeed = 3f;

    [Header("Colisión")]
    public float bounceForce = 8f;
    public float stunDuration = 0.3f;

    [Header("Espada")]
    public float swordRange = 3f;
    public int swordDamage = 3;
    public float swordCooldown = 0.5f;

    // Componentes
    private Rigidbody rb;
    private AngelWarrior angel;
    private float stunTimer = 0f;
    private bool isStunned = false;
    private float swordTimer = 0f;
    private float targetSpeed;

    // Inputs
    [HideInInspector] public float currentSpeed;
    [HideInInspector] public float inputPitch;
    [HideInInspector] public float inputRoll;
    [HideInInspector] public float inputThrottle;
    [HideInInspector] public float inputBrake;

    private const float STICK_DEADZONE = 0.2f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.linearDamping = 0.1f;
        rb.angularDamping = 4f;
        rb.constraints = RigidbodyConstraints.FreezeRotation;

        angel = GetComponent<AngelWarrior>();
        targetSpeed = cruiseSpeed;

        // Siempre en modo vuelo — alas extendidas
        if (angel != null) angel.SetFlightMode(true);

        // Impulso inicial hacia adelante
        rb.linearVelocity = transform.forward * cruiseSpeed;
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
        currentSpeed = rb.linearVelocity.magnitude;
    }

    void FixedUpdate()
    {
        if (isStunned) return;
        ApplyMovement();
        ApplyRotation();
        EnforceTerrainLimits();
    }

    void OnCollisionEnter(Collision collision)
    {
        if (collision.relativeVelocity.magnitude < 2f) return;
        rb.linearVelocity *= 0.3f;
        rb.AddForce(collision.contacts[0].normal * bounceForce, ForceMode.VelocityChange);
        isStunned = true;
        stunTimer = stunDuration;
    }

    // ==================== INPUT ====================

    private void ReadInput()
    {
        // PITCH: Flechas / Left Stick Y — arriba = subir (Star Fox)
        inputPitch = 0f;
        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) inputPitch = 1f;
        if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) inputPitch = -1f;
        float stickY = GetAxisSafe("Vertical");
        if (Mathf.Abs(stickY) > STICK_DEADZONE && Mathf.Abs(stickY) > Mathf.Abs(inputPitch))
            inputPitch = stickY;

        // ROLL: A/D / Left Stick X / LB/RB
        inputRoll = 0f;
        if (Input.GetKey(KeyCode.A)) inputRoll = -1f;
        if (Input.GetKey(KeyCode.D)) inputRoll = 1f;
        float stickX = GetAxisSafe("Horizontal");
        if (Mathf.Abs(stickX) > STICK_DEADZONE && Mathf.Abs(stickX) > Mathf.Abs(inputRoll))
            inputRoll = stickX;
        // LB/RB como alternativa
        if (Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.JoystickButton4)
            || Input.GetKey(KeyCode.JoystickButton13)) inputRoll = -1f;
        if (Input.GetKey(KeyCode.E) || Input.GetKey(KeyCode.JoystickButton5)
            || Input.GetKey(KeyCode.JoystickButton14)) inputRoll = 1f;

        // THROTTLE: RT
        inputThrottle = GetTriggerValue("Joy1Axis6");

        // BRAKE: LT
        inputBrake = GetTriggerValue("Joy1Axis3");
    }

    private void HandleSwordInput()
    {
        bool swordInput = Input.GetKeyDown(KeyCode.Mouse1)
            || Input.GetKeyDown(KeyCode.C)
            || Input.GetKeyDown(KeyCode.JoystickButton2)
            || Input.GetKeyDown(KeyCode.JoystickButton18);

        if (swordInput && swordTimer <= 0f && angel != null)
        {
            angel.SwingSword();
            swordTimer = swordCooldown;
        }
    }

    // ==================== MOVIMIENTO ====================

    /// <summary>
    /// Movimiento Star Fox: siempre avanza, RT acelera, LT frena.
    /// Pitch genera subida/bajada directa.
    /// </summary>
    private void ApplyMovement()
    {
        // Velocidad objetivo
        if (inputThrottle > 0.1f)
            targetSpeed = Mathf.Lerp(cruiseSpeed, boostSpeed, inputThrottle);
        else if (inputBrake > 0.1f)
            targetSpeed = Mathf.Lerp(cruiseSpeed, brakeSpeed, inputBrake);
        else
            targetSpeed = cruiseSpeed;

        // Interpolar velocidad
        float currentForward = Vector3.Dot(rb.linearVelocity, transform.forward);
        float newSpeed = Mathf.Lerp(currentForward, targetSpeed, speedTransition * Time.fixedDeltaTime);

        // Dirección horizontal (XZ) — siempre avanza donde apunta
        Vector3 flatForward = transform.forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.01f) flatForward = Vector3.forward;
        flatForward.Normalize();

        Vector3 velocity = flatForward * newSpeed;

        // Velocidad vertical: generada por pitch
        float pitchAngle = transform.eulerAngles.x;
        if (pitchAngle > 180f) pitchAngle -= 360f;
        // Nariz arriba (pitch negativo) = subir, nariz abajo = bajar
        float verticalSpeed = -pitchAngle / 40f * newSpeed * 0.7f;

        // Gravedad suave
        float currentY = rb.linearVelocity.y;
        float targetY = verticalSpeed - flightGravity * 0.2f;
        velocity.y = Mathf.Lerp(currentY, targetY, 5f * Time.fixedDeltaTime);

        rb.linearVelocity = velocity;
    }

    /// <summary>
    /// Rotación Star Fox: roll para girar, pitch para subir/bajar.
    /// Auto-nivelación agresiva de roll y pitch cuando no hay input.
    /// El personaje NUNCA vuela de cabeza.
    /// </summary>
    private void ApplyRotation()
    {
        float currentPitch = transform.eulerAngles.x;
        if (currentPitch > 180f) currentPitch -= 360f;
        float currentRoll = transform.eulerAngles.z;
        if (currentRoll > 180f) currentRoll -= 360f;

        // PITCH: input del jugador
        float pitch = -inputPitch * pitchSpeed * Time.fixedDeltaTime;

        // Limitar pitch a ±30°
        if ((currentPitch < -30f && pitch < 0f) || (currentPitch > 30f && pitch > 0f))
            pitch = 0f;

        // Auto-nivelación del PITCH cuando no hay input (vuelve a horizontal)
        if (Mathf.Abs(inputPitch) < 0.1f && Mathf.Abs(currentPitch) > 2f)
        {
            pitch = -currentPitch * 4f * Time.fixedDeltaTime;
        }

        // ROLL: input del jugador
        float roll = inputRoll * rollSpeed * Time.fixedDeltaTime;

        // Auto-nivelación del ROLL agresiva (siempre vuelve a nivelado)
        if (Mathf.Abs(inputRoll) < 0.1f)
        {
            roll = -currentRoll * 5f * Time.fixedDeltaTime;
        }

        // Limitar roll máximo a ±50° (nunca de cabeza)
        if ((currentRoll < -50f && roll < 0f) || (currentRoll > 50f && roll > 0f))
            roll = 0f;

        // Corrección de emergencia: si por alguna razón está casi de cabeza, forzar nivelación
        if (Mathf.Abs(currentRoll) > 60f)
        {
            roll = -currentRoll * 8f * Time.fixedDeltaTime;
        }

        // Yaw automático por roll (giro coordinado)
        float autoYaw = -currentRoll / 90f * yawFromRoll * Time.fixedDeltaTime;

        transform.Rotate(pitch, autoYaw, -roll, Space.Self);
    }

    // ==================== LÍMITES ====================

    private void EnforceTerrainLimits()
    {
        Vector3 pos = transform.position;

        // Limitar XZ al terreno
        pos.x = Mathf.Clamp(pos.x, terrainMargin, terrainSize - terrainMargin);
        pos.z = Mathf.Clamp(pos.z, terrainMargin, terrainSize - terrainMargin);

        // Obtener altura del terreno
        float terrainHeight = 0f;
        Terrain terrain = Terrain.activeTerrain;
        if (terrain != null)
            terrainHeight = terrain.SampleHeight(pos) + terrain.transform.position.y;

        float heightAboveGround = pos.y - terrainHeight;
        float distToceiling = maxAltitude - pos.y;

        // --- AUTO-ENDEREZAMIENTO cerca de los límites ---
        float currentPitch = transform.eulerAngles.x;
        if (currentPitch > 180f) currentPitch -= 360f;

        // Cerca del suelo y apuntando hacia abajo → enderezar (pitch hacia 0)
        if (heightAboveGround < levelOutDistance && currentPitch > 5f)
        {
            float urgency = 1f - (heightAboveGround / levelOutDistance);
            float correction = -currentPitch * levelOutSpeed * urgency * Time.fixedDeltaTime;
            transform.Rotate(correction, 0f, 0f, Space.Self);
        }

        // Cerca del techo y apuntando hacia arriba → enderezar (pitch hacia 0)
        if (distToceiling < levelOutDistance && currentPitch < -5f)
        {
            float urgency = 1f - (distToceiling / levelOutDistance);
            float correction = -currentPitch * levelOutSpeed * urgency * Time.fixedDeltaTime;
            transform.Rotate(correction, 0f, 0f, Space.Self);
        }

        // --- CLAMP DURO de posición ---
        if (pos.y > maxAltitude)
        {
            pos.y = maxAltitude;
            if (rb.linearVelocity.y > 0)
            {
                Vector3 vel = rb.linearVelocity; vel.y = 0; rb.linearVelocity = vel;
            }
        }

        if (pos.y < terrainHeight + minAltitude)
        {
            pos.y = terrainHeight + minAltitude;
            if (rb.linearVelocity.y < 0)
            {
                Vector3 vel = rb.linearVelocity; vel.y = 0; rb.linearVelocity = vel;
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
