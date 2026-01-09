using UnityEngine;

/// <summary>
/// Gestisce le statistiche del character. (es. <see cref="_walkSpeed"/>)<br/>
/// Per il momento funge piů da Struct; ma nel caso volessimo avere un controllo piů granulare sui parametri del personaggio, si puň estendere la funzionalitŕ di questa classe
/// </summary>
public class CharacterStats : MonoBehaviour
{
    // Campi privati
    [Header("Movement Speed")]
    /// <summary>Velocitŕ di camminata in m/s</summary>
    [SerializeField] private float _walkSpeed = 5f;
    /// <summary>Velocitŕ di corsa in m/s</summary>
    [SerializeField] private float _runSpeed = 7.5f;

    [Header("Jump Settings")]
    /// <summary>L'altezza che viene raggiunta con un salto (indipendente dalla gravitŕ)</summary>
    [SerializeField] private float _jumpHeight = 2f;
    /// <summary>Tempo (in secondi) permesso al giocatore di saltare anche dopo aver perso conttato con il terreno </summary>
    [SerializeField] private float _jumpCoyoteTime = 0.25f;

    [Header("Air Movement")]
    [SerializeField] private float _airAcceleration = 3f;
    [SerializeField] private float _airDeceleration = 3f;

    [Header("Slide Settings")]
    //TODO _slideMoveInfluence e _slideDecelleration sono da definire meglio
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
    /// <summary>Velocitŕ minima per correre su una parete</summary>
    [SerializeField] private float _wallRunMinimumSpeed = 1f;
    /// <summary>Angolazione massima per la visuale rispetto al muro consentita per correre su una parete</summary>
    [SerializeField] private float _wallRunLookAngleLimit = 110f;

    [Header("Dash Settings")]
    /// <summary> </summary>
    [SerializeField] private float _dashSpeed = 17f;
    /// <summary>Durata del dash in secondi</summary>
    [SerializeField] private float _dashDuration = 0.3f;

    [Header("Jetpack Settings")]
    /// <summary>Accelerazione del jetpack (m/s^2)</summary>
    [SerializeField] private float _jetpackAcceleration = 16f;

    [Header("Physics")]
    /// <summary>Accelerazione di gravitŕ (m/s^2)</summary>
    [SerializeField] private float _gravity = 9.81f;

    // Proprietŕ
    public float WalkSpeed => _walkSpeed;
    public float RunSpeed => _runSpeed;
    public float JumpHeight => _jumpHeight;
    public float JumpCoyoteTime => _jumpCoyoteTime;
    public float AirAcceleration => _airAcceleration;
    public float AirDeceleration => _airDeceleration;
    public float SlideStartBoost => _slideStartBoost;
    public float SlideMoveInfluence => _slideMoveInfluence;
    public float SlideDeceleration => _slideDeceleration;
    public float SlideIncreasedDeceleration => _slideIncreasedDeceleration;
    public float SlideIncreasedDecelerationThreshold => _slideIncreasedDecelerationThreshold;
    public float WallRunMinSpeed => _wallRunMinimumSpeed;
    public float WallRunLookAngleLimit => _wallRunLookAngleLimit;
    public float DashSpeed => _dashSpeed;
    public float DashDuration => _dashDuration;
    public float JetpackAcceleration => _jetpackAcceleration;
    public float Gravity => _gravity;
}