using UnityEngine;

/// <summary>
/// Gestisce le statistiche del character. (es. <see cref="_walkSpeed"/>)<br/>
/// Per il momento funge più da Struct; ma nel caso volessimo avere un controllo più granulare sui parametri del personaggio, si può estendere la funzionalità di questa classe
/// </summary>
public class CharacterStats : MonoBehaviour
{
    // Campi privati
    /// <summary>Velocità di camminata in m/s</summary>
    [SerializeField] private float _walkSpeed = 5f;
    /// <summary>Velocità di corsa in m/s</summary>
    [SerializeField] private float _runSpeed = 7.5f;

    /// <summary>L'altezza che viene raggiunta con un salto (indipendente dalla gravità)</summary>
    [SerializeField] private float _jumpHeight = 2f;
    /// <summary>Tempo (in secondi) permesso al giocatore di saltare anche dopo aver perso conttato con il terreno </summary>
    [SerializeField] private float _jumpCoyoteTime = 0.25f;

    [SerializeField] private float _airAcceleration = 3f;
    [SerializeField] private float _airDeceleration = 3f;

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

    /// <summary>Velocità minima per correre su una parete</summary>
    [SerializeField] private float _wallRunMinimumSpeed = 1f;
    /// <summary>Angolazione massima per la visuale rispetto al muro consentita per correre su una parete</summary>
    [SerializeField] private float _wallRunLookAngleLimit = 110f;

    /// <summary>Accelerazione di gravità (m/s^2)</summary>
    [SerializeField] private float _gravity = 9.81f;

    // Proprietà
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
    public float Gravity => _gravity;
}
