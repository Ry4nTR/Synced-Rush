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
    /// <summary>Accelerazione di gravità (m/s^2)</summary>
    [SerializeField] private float _gravity = 9.81f;

    // Proprietà
    public float WalkSpeed => _walkSpeed;
    public float RunSpeed => _runSpeed;
    public float JumpHeight => _jumpHeight;
    public float JumpCoyoteTime => _jumpCoyoteTime;
    public float Gravity => _gravity;
}
