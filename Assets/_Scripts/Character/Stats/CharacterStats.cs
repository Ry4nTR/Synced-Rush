using UnityEngine;

/// <summary>
/// Gestisce le statistiche del character. (es. <see cref="_walkSpeed"/>)<br/>
/// Per il momento funge piů da Struct; ma nel caso volessimo avere un controllo piů granulare sui parametri del personaggio, si puň estendere la funzionalitŕ di questa classe
/// </summary>
public class CharacterStats : MonoBehaviour
{
    // Campi privati
    [Header("Movement Speed")]
    /// <summary>Velocitŕ target di camminata in m/s</summary>
    [SerializeField] private float _walkSpeed = 5f;
    /// <summary>Velocitŕ target di corsa in m/s</summary>
    [SerializeField] private float _runSpeed = 7.5f;
    /// <summary>Accelerazione/Decelerazione camminata/corsa (m/s^2)</summary>
    [SerializeField] private float _groundAccelDecel = 10f;
    /// <summary>Decelerazione camminata/corsa nel caso viene superata la velocità di target (m/s^2)</summary>
    [SerializeField] private float _groundOverspeedDeceleration = 7.5f;

    [Header("Jump Settings")]
    /// <summary>L'altezza che viene raggiunta con un salto (indipendente dalla gravitŕ)</summary>
    [SerializeField] private float _jumpHeight = 2f;
    /// <summary>Tempo (in secondi) permesso al giocatore di saltare anche dopo aver perso conttato con il terreno </summary>
    [SerializeField] private float _jumpCoyoteTime = 0.25f;

    [Header("Air Movement")]
    /// <summary>Velocità desiderata in aria (m/s)</summary>
    [SerializeField] private float _airTargetSpeed = 3f;
    [SerializeField] private float _airAcceleration = 5f;
    [SerializeField] private float _airDeceleration = 3f;
    /// <summary>Decelerazione aumentata nel caso viene superata la target speed (m/s^2)</summary>
    [SerializeField] private float _airOverspeedDeceleration = 6f;

    [Header("Slide Settings")]
    //TODO _slideMoveInfluence e _slideDeceleration sono da definire meglio
    /// <summary> </summary>
    [SerializeField] private float _slideStartBoost = 7.5f;
    /// <summary> </summary>
    [SerializeField] private float _slideMoveInfluence = 4f;
    /// <summary> </summary>
    [SerializeField] private float _slideDeceleration = 10f;
    /// <summary> </summary>
    [SerializeField] private float _slideIncreasedDeceleration = 20f;
    /// <summary> </summary>
    [SerializeField] private float _slideIncreasedDecelerationThreshold = 70f;

    [Header("Wall Run Settings")]
    /// <summary>Decelerazione WallRun (m/s^2)</summary>
    [SerializeField] private float _wallRunTargetSpeed = 50f;
    /// <summary>Decelerazione WallRun (m/s^2)</summary>
    [SerializeField] private float _wallRunDeceleration = 5f;
    /// <summary>Frenata manuale del WallRun (m/s^2) NOTA: viene comunque applicata la <see cref="_wallRunDeceleration"/> durante la frenata</summary>
    [SerializeField] private float _wallRunBrake = 10f;
    /// <summary>Velocitŕ minima per correre su una parete</summary>
    [SerializeField] private float _wallRunMinimumSpeed = 3f;
    /// <summary>Angolazione massima per la visuale rispetto al muro consentita per correre su una parete</summary>
    [SerializeField] private float _wallRunLookAngleLimit = 110f;
    /// <summary>Durata della spinta iniziale del wall run in secondi</summary>
    [SerializeField] private float _wallRunInitialBoostDuration = 1f;
    /// <summary>Forza di accelerazione della spinta iniziale del wall run (m/s^2)</summary>
    [SerializeField] private float _wallRunInitialBoostAcceleration = 10f;


    [Header("Dash Settings")]
    /// <summary> </summary>
    [SerializeField] private float _dashSpeed = 17f;
    /// <summary>Durata del dash in secondi</summary>
    [SerializeField] private float _dashDuration = 0.3f;
    /// <summary>carica massima del dash</summary>
    [SerializeField] private float _dashkMaxCharge = 100;
    /// <summary>Consumo carica dash all'uso</summary>
    [SerializeField] private float _dashDrain = 50;
    /// <summary>Guadagno di carica del jetpack al secondo</summary>
    [SerializeField] private float _dashRecharge = 16.6f;

    [Header("Jetpack Settings")]
    /// <summary>Accelerazione del jetpack (m/s^2)</summary>
    [SerializeField] private float _jetpackAcceleration = 16f;
    /// <summary>Carburante massimo del jetpack</summary>
    [SerializeField] private float _jetpackMaxCharge = 100f;
    /// <summary>Consumo carburante del jetpack al secondo</summary>
    [SerializeField] private float _jetpackDrain = 20f;
    /// <summary>Guadagno di carburante del jetpack al secondo</summary>
    [SerializeField] private float _jetpackRecharge = 16.6f;

    [Header("Grappling Hook Settings")]
    /// <summary>Distanza massima raggiungibile dall'uncino in metri</summary>
    [SerializeField] private float _hookMaxDistance = 50f;
    /// <summary>Velocità (fissa) dell'uncino (m/s)</summary>
    [SerializeField] private float _hookSpeed = 40f;
    /// <summary>Forza di attrazione dell'uncino al giocatore (m/s^2)</summary>
    [SerializeField] private float _hookPull = 30f;
    /// <summary>Distanza minima consentita fra l'uncino e il giocatore in metri</summary>
    [SerializeField] private float _hookMinDistance = 2.5f;

    [Header("Physics")]
    /// <summary>Accelerazione di gravitŕ (m/s^2)</summary>
    [SerializeField] private float _gravity = 9.81f;

    // Proprietŕ
    public float WalkSpeed => _walkSpeed;
    public float RunSpeed => _runSpeed;
    public float GroundAccelDecel => _groundAccelDecel;
    public float GroundOverspeedDeceleration => _groundOverspeedDeceleration;

    public float JumpHeight => _jumpHeight;
    public float JumpCoyoteTime => _jumpCoyoteTime;

    public float AirTargetSpeed => _airTargetSpeed;
    public float AirAcceleration => _airAcceleration;
    public float AirDeceleration => _airDeceleration;
    public float AirOverspeedDeceleration => _airOverspeedDeceleration;

    public float SlideStartBoost => _slideStartBoost;
    public float SlideMoveInfluence => _slideMoveInfluence;
    public float SlideDeceleration => _slideDeceleration;
    public float SlideIncreasedDeceleration => _slideIncreasedDeceleration;
    public float SlideIncreasedDecelerationThreshold => _slideIncreasedDecelerationThreshold;

    public float WallRunTargetSpeed => _wallRunTargetSpeed;
    public float WallRunDeceleration => _wallRunDeceleration;
    public float WallRunBrake => _wallRunBrake;
    public float WallRunMinSpeed => _wallRunMinimumSpeed;
    public float WallRunLookAngleLimit => _wallRunLookAngleLimit;
    public float WallRunInitialBoostDuration => _wallRunInitialBoostDuration;
    public float WallRunInitialBoostAcceleration => _wallRunInitialBoostAcceleration;

    public float DashSpeed => _dashSpeed;
    public float DashDuration => _dashDuration;
    public float DashMaxCharge => _dashkMaxCharge;
    public float DashDrain => _dashDrain;
    public float DashRecharge => _dashRecharge;

    public float JetpackAcceleration => _jetpackAcceleration;
    public float JetpackMaxCharge => _jetpackMaxCharge;
    public float JetpackDrain => _jetpackDrain;
    public float JetpackRecharge => _jetpackRecharge;

    public float HookMaxDistance => _hookMaxDistance;
    public float HookSpeed => _hookSpeed;
    public float HookPull => _hookPull;
    public float HookMinDistance => _hookMinDistance;

    public float Gravity => _gravity;
}