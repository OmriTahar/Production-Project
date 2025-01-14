using System.Collections;
using UnityEngine.AI;
using UnityEngine;

public class RangedEnemyAI : BaseEnemyAI
{

    private ProjectilePool _rangedProjectilePool;

    [Header("Ranged Attack Settings")]
    [SerializeField] float _ShootForce;
    [SerializeField] float _arcShootForce;
    [Tooltip("Player layer must be included even though he is not a throw path obstacle!")]
    [SerializeField] LayerMask _throwPathObstacleLayers;

    [Header("Ranged Attack References")]
    [SerializeField] Transform _rangedShootPoint;
    [SerializeField] Transform _throwPathChecker;

    [Header("Fleeing Settings")]
    [SerializeField] float _startFleeFromPlayer_Range;
    [SerializeField] float _fleeingDuration;
    [SerializeField] float _fleeDistance = 10f;

    [Header("Enemy Attack Status")]
    [SerializeField] bool _isPlayerInAttackRange;
    [SerializeField] bool _isThrowPathBlocked;
    [SerializeField] bool _isAlreadyAttacked;

    [Header("Enemy Flee Status")]
    [SerializeField] bool _isPlayerTooClose;
    [SerializeField] bool _canFlee;
    [SerializeField] bool _isFleeing;
    [SerializeField] bool _hasFleedOnce;
    [SerializeField] float _fleeCooldown;
    [SerializeField] bool _finishedThrowing = true;

    private Vector3 _directionToPlayer;
    private WaitForSeconds _fleeDurationCoroutine;


    protected override void Awake()
    {
        base.Awake();
        _rangedProjectilePool = GetComponent<ProjectilePool>();
        _fleeDurationCoroutine = new WaitForSeconds(_fleeingDuration);
    }

    protected override void PlayerDetaction()
    {
        if (IsEnemyActivated)
        {
            if (!_isFleeing)
            {
                _isPlayerInAttackRange = Physics.CheckSphere(transform.position, _unitAttackRange, _playerLayer);
                _isPlayerTooClose = Physics.CheckSphere(transform.position, _startFleeFromPlayer_Range, _playerLayer);

                if (_isPlayerTooClose)
                    _canFlee = CanEnemyFlee();
            }
        }
    }

    protected override void EnemyStateMachine()
    {
        base.EnemyStateMachine();

        if (IsEnemyActivated && _finishedThrowing)
        {

            if (IsCreatingAttackPath)
            {
                if (HasReachedDestination())
                {
                    IsCreatingAttackPath = false;
                }
            }
            else
            {
                if (!_isFleeing)
                {
                    if (!_isPlayerInAttackRange)
                    {
                        ChasePlayer();
                    }
                }

                if (_isPlayerTooClose && _canFlee && !_hasFleedOnce)
                    StartCoroutine(Flee());

                if ((_isPlayerInAttackRange && (!_isPlayerTooClose || _hasFleedOnce)))
                {
                    transform.LookAt(_playerTransform);
                    CheckThrowPath();

                    if (!_isThrowPathBlocked)
                        RangeAttack();
                    else
                        CreateClearAttackPath();
                }
            }
        }
    }

    protected override void ChasePlayer()
    {
        if (_playerTransform != null)
        {
            _agent.SetDestination(_playerTransform.position);
            _myCurrentState = State.chasing;

            if (!_isPlayerTooClose && _isPlayerInAttackRange)
                return;
        }
    }

    private void CheckThrowPath()
    {
        Ray ray = new Ray(_throwPathChecker.position, _throwPathChecker.forward);
        RaycastHit hit;
        Physics.Raycast(ray, out hit, _unitAttackRange, _throwPathObstacleLayers);

        if (hit.collider != null && hit.collider.tag != "Player")
        {
            _isThrowPathBlocked = true;
            return;
        }
        else if (hit.collider != null && hit.collider.tag == "Player")
        {
            _isThrowPathBlocked = false;
            return;
        }
    }

    private void RangeAttack()
    {
        if (!_isThrowPathBlocked)
        {
            _myCurrentState = State.attacking;

            _agent.SetDestination(transform.position); // Make sure enemy doesn't move while attacking
            transform.LookAt(_playerTransform);

            if (!_isAlreadyAttacked && !_isThrowPathBlocked)
            {
                _isAlreadyAttacked = true;
                _finishedThrowing = false;
                _animator.SetTrigger("Throw");
            }
        }
    }

    public void Throw() // Called from animation event
    {
        GameObject carrot = _rangedProjectilePool.GetProjectileFromPool();
        carrot.transform.position = _rangedShootPoint.position;
        carrot.transform.rotation = Quaternion.identity;

        Rigidbody rb = carrot.GetComponent<Rigidbody>();
        rb.AddForce((_playerTransform.position - carrot.transform.position).normalized * _ShootForce, ForceMode.Impulse);
        rb.AddForce(Vector3.up * _arcShootForce, ForceMode.Impulse);

        Vector3 rotateCarrotTo = new Vector3(transform.position.x, carrot.transform.position.y, transform.position.z);
        carrot.transform.LookAt(rotateCarrotTo);
        FMODUnity.RuntimeManager.PlayOneShot("event:/Sound/Bunny/Carrot Shoot 2");

        Invoke(nameof(ResetAttack), _timeBetweenAttacks);
    }

    public void FinishedThrowing() // Called from animation event. Fix fleeing before finishing throw animation
    {
        _finishedThrowing = true;
    }

    private bool CanEnemyFlee()
    {
        _directionToPlayer = (transform.position - _playerTransform.position).normalized * _fleeDistance;
        Vector3 newFleePosition = transform.position + _directionToPlayer;

        if (_agent.CalculatePath(newFleePosition, new NavMeshPath()))
        {
            return true;
        }
        else
            return false;
    }

    private IEnumerator Flee()
    {
        _myCurrentState = State.fleeing;
        _isFleeing = true;

        _directionToPlayer = (transform.position - _playerTransform.position).normalized * _fleeDistance;
        Vector3 newFleePosition = transform.position + _directionToPlayer;
        _agent.SetDestination(newFleePosition);

        yield return _fleeDurationCoroutine;
        _hasFleedOnce = true;
        _isFleeing = false;
        Invoke("ResetFlee", _fleeCooldown);
    }

    private void ResetAttack()
    {
        _isAlreadyAttacked = false;
    }

    private void ResetFlee()
    {
        _hasFleedOnce = false;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _startFleeFromPlayer_Range);
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, _unitAttackRange);

        Gizmos.color = Color.yellow;
        Vector3 direction = transform.TransformDirection(Vector3.forward) * _unitAttackRange;
        Gizmos.DrawRay(transform.position, direction);
    }

}
