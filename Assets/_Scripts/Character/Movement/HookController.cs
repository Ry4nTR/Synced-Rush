using UnityEngine;

public class HookController : MonoBehaviour
{
    [SerializeField] private LayerMask _layerMask;

    private float _speed = 40f;
    private float _maxDistance = 50f;
    private float _currentDistance = 0f;

    private Vector3 _startPosition;
    private Vector3 _direction;

    public bool IsHooked { get; private set; } = false;
    public bool IsShooting { get; private set; } = false;

    private void Awake()
    {
        gameObject.SetActive(false);
    }

    // La direzione deve essere normalizzata!
    public void Shoot(Vector3 startPosition, Vector3 direction, float speed, float maxDistance)
    {
        gameObject.SetActive(true);

        _speed = speed;
        _maxDistance = maxDistance;
        _currentDistance = 0f;
        IsHooked = false;
        IsShooting = true;
        

        _startPosition = startPosition;
        transform.position = startPosition;

        direction.Normalize();
        _direction = direction;
        transform.forward = direction;
    }

    void Update()
    {
        if (IsHooked || !IsShooting) return;

        float distanceThisFrame = _speed * Time.deltaTime;
        float newDistance = _currentDistance + distanceThisFrame;

        //if (newDistance > _maxDistance)
        //{
        //    distanceThisFrame = newDistance - _maxDistance;
        //    _currentDistance = _maxDistance;
        //}
        //else
        //    _currentDistance = newDistance;

        if (_currentDistance + distanceThisFrame > _maxDistance)
        {
            distanceThisFrame = _maxDistance - _currentDistance;
        }

        if (Physics.Raycast(transform.position, _direction, out RaycastHit hit, distanceThisFrame, _layerMask))
        {
            transform.position = hit.point;
            IsHooked = true;
            OnHookHit(hit.collider);
        }
        else
        {
            //transform.Translate(_direction * distanceThisFrame);
            transform.position += _direction * distanceThisFrame;
        }

        if (Vector3.Distance(_startPosition, transform.position) > _maxDistance)
        {
            IsShooting = false;
            gameObject.SetActive(false);
        }
    }

    private void OnHookHit(Collider other)
    {
        IsHooked = true;
        IsShooting = false;
    }
}


