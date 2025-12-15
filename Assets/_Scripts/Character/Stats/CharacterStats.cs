using UnityEngine;

/// <summary>
/// Gestisce le statistiche del character. (es. <see cref="_walkSpeed"/>)<br/>
/// Per il momento funge più da Struct; ma nel caso volessimo avere un controllo più granulare sui parametri del personaggio, si può estendere la funzionalità di questa classe
/// </summary>
public class CharacterStats : MonoBehaviour
{
    // Campi privati
    /// <summary>Velocità di camminata in m/s</summary>
    [SerializeField] private float _walkSpeed = 3.5f;
    /// <summary>Velocità di corsa in m/s</summary>
    [SerializeField] private float _runSpeed = 5.25f;
    /// <summary>L'altezza che viene raggiunta con un salto (indipendente dalla gravità)</summary>
    [SerializeField] private float _jumpHeight = 2f;
    /// <summary>Tempo (in secondi) permesso al giocatore di saltare anche dopo aver perso conttato con il terreno </summary>
    [SerializeField] private float _jumpCoyoteTime = 0.25f;
    //TODO _slideMoveInfluence e _slideDecelleration sono da definire meglio
    /// <summary> </summary>
    [SerializeField] private float _slideStartBoost = 7.5f;
    /// <summary> </summary>
    [SerializeField] private float _slideMoveInfluence = 4f;
    /// <summary> </summary>
    [SerializeField] private float _slideDecelleration = 5f;
    /// <summary> </summary>
    [SerializeField] private float _slideIncreasedDecelleration = 10f;
    /// <summary> </summary>
    [SerializeField] private float _slideIncreasedDecellerationThreshold = 5f;
    /// <summary>Velocità minima per correre su una parete</summary>
    [SerializeField] private float _wallRunMinimumSpeed = 1f;
    /// <summary>Angolazione massima per la visuale rispetto al muro consentita per correre su una parete</summary>
    [SerializeField] private float _wallRunLookAngleLimit = 100f;
    /// <summary>Accelerazione di gravità (m/s^2)</summary>
    [SerializeField] private float _gravity = 9.81f;

    // Proprietà
    public float WalkSpeed => _walkSpeed;
    public float RunSpeed => _runSpeed;
    public float JumpHeight => _jumpHeight;
    public float JumpCoyoteTime => _jumpCoyoteTime;
    public float SlideStartBoost => _slideStartBoost;
    public float SlideMoveInfluence => _slideMoveInfluence;
    public float SlideDeceleration => _slideDecelleration;
    public float SlideIncreasedDeceleration => _slideIncreasedDecelleration;
    public float SlideIncreasedDecelerationThreshold => _slideIncreasedDecellerationThreshold;
    public float WallRunMinSpeed => _wallRunMinimumSpeed;
    public float WallRunLookAngleLimit => _wallRunLookAngleLimit;
    public float Gravity => _gravity;
}
