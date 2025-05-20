using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum TeamType
{
    TeamA,
    TeamB
}

[System.Serializable]
public struct ColliderData
{
    public Collider collider;
    public float activationDelay;
}

[System.Serializable]
public struct AttackData
{
    public float animationDuration; // Tổng thời gian animation
    public List<ColliderData> colliderDatas; // Danh sách Collider và thời gian kích hoạt
}

public class CharacterData : MonoBehaviour
{
    [Header("Character Settings")]
    [SerializeField] protected float baseHealth = 100f;
    [SerializeField] protected float baseDamage = 10f;
    [SerializeField] protected float attackRange = 1.5f;
    [SerializeField] protected float attackSpeed = 1f;
    [SerializeField] protected float baseMoveSpeed = 3f;
    [SerializeField] protected float detectRange = 30f;

    [SerializeField]
    protected AttackData[] attacks;

    [Header("Character Level Scaling")]
    [SerializeField] protected float healthScale = 0.2f;
    [SerializeField] protected float dameScale = 0.1f;
    [SerializeField] protected float moveSpeedScale = 0.1f;

    [Header("Attack chain Settings")]
    [SerializeField] protected float attackMultiplier = 0.2f;

    [Header("References")]
    [SerializeField] protected Animator animator;
    [SerializeField] protected Rigidbody rb;
    [SerializeField] protected Collide collide;

    public TeamType Team => _team;
    public float CurrentHealth => _currentHealth;
    public bool IsAlive => _isAlive;
    protected CharacterData _target;
    [HideInInspector] public float NextAttackTime = 0f;

    protected float _currentHealth;
    protected float _maxHealth;
    protected float _currentMoveSpeed;
    protected bool _isAlive;
    protected TeamType _team;
    protected int _currentAttack;
    protected int _level;
    protected bool _isAttacking;
    protected int _maxAttack = 3;
    private const float ComboResetDelay = 0.5f;
    private const float MoveInterruptWindow = 0.2f; // Thời gian cửa sổ di chuyển
    private float _lastAttackTime = 0f;
    private bool _canResetCombo;
    private float _moveTimeAccumulator = 0f; // Theo dõi thời gian di chuyển
    private bool _isMovingDuringComboWindow;

    private Joystick _joystick;
    private Coroutine _attackCoroutine;
    private List<Coroutine> _colliderCoroutines = new();

    public virtual void Init(int level, TeamType teamType)
    {
        _level = level;
        _team = teamType;
        _isAlive = true;
        _maxHealth = baseHealth * (_level * healthScale + 1);
        _currentHealth = _maxHealth;
        _currentMoveSpeed = baseMoveSpeed * (_level * moveSpeedScale + 1);
        _currentAttack = -1;
        _isAttacking = false;
        _canResetCombo = false;
        _isMovingDuringComboWindow = false;
        _moveTimeAccumulator = 0f;
        _maxAttack = 4;
        collide.Init(this);
        Debug.Log($"[{gameObject.name}] Initialized: Level={_level}, Team={_team}, Health={_currentHealth}, MoveSpeed={_currentMoveSpeed}");
    }

    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        Debug.Log($"[{gameObject.name}] Took {damage} damage. Health={_currentHealth}");
        if (_currentHealth <= 0)
        {
            _isAlive = false;
            Debug.Log($"[{gameObject.name}] Defeated!");
        }
    }

    public float GetDamage()
    {
        float comboBonus = 1 + (_currentAttack * attackMultiplier);
        float damage = baseDamage * (1 + _level * dameScale) * comboBonus;
        Debug.Log($"[{gameObject.name}] GetDamage: Base={baseDamage}, LevelBonus={1 + _level * dameScale}, ComboBonus={comboBonus}, Total={damage}");
        return damage;
    }

    public virtual void UpdateCharacter()
    {
        if (!_isAlive) return;

        if (!_isAttacking && _canResetCombo)
        {
            _lastAttackTime -= Time.deltaTime;
            Debug.Log($"[{gameObject.name}] Combo reset timer: {_lastAttackTime:F3}s");
            if (_lastAttackTime <= 0)
            {
                _canResetCombo = false;
                ResetAttack();
            }
        }

        var movementVector = _joystick.Direction;
        Debug.Log($"[{gameObject.name}] MovementVector: Magnitude={movementVector.magnitude:F3}");

        if (movementVector.magnitude > 0.01f)
        {
            Vector3 direction = new(movementVector.x, 0, movementVector.y);
            rb.velocity = _currentMoveSpeed * direction;
            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 8 * Time.deltaTime);
            animator.SetBool("IsMoving", true);
            animator.speed = _currentMoveSpeed;

            // Theo dõi di chuyển trong cửa sổ combo
            if (_canResetCombo)
            {
                _isMovingDuringComboWindow = true;
                _moveTimeAccumulator += Time.deltaTime;
                Debug.Log($"[{gameObject.name}] Moving during combo window: MoveTime={_moveTimeAccumulator:F3}s");

                // Nếu di chuyển quá 0.2s, reset combo
                if (_moveTimeAccumulator > MoveInterruptWindow)
                {
                    Debug.Log($"[{gameObject.name}] Movement exceeded {MoveInterruptWindow:F3}s. Resetting combo!");
                    ResetAttack();
                    _isMovingDuringComboWindow = false;
                    _moveTimeAccumulator = 0f;
                }
            }
        }
        else
        {
            rb.velocity = Vector3.zero;
            animator.SetBool("IsMoving", false);
            animator.speed = 1;

            // Nếu vừa di chuyển ngắn trong 0.2s và dừng, chuyển sang đòn tiếp theo
            if (_canResetCombo && _isMovingDuringComboWindow && _moveTimeAccumulator > 0 && _moveTimeAccumulator <= MoveInterruptWindow)
            {
                Debug.Log($"[{gameObject.name}] Short movement stopped within {MoveInterruptWindow:F3}s (MoveTime={_moveTimeAccumulator:F3}s). Triggering next attack!");
                ResetAttack();
                Attack();
            }
            else
            {
                Attack();
            }

            _isMovingDuringComboWindow = false;
            _moveTimeAccumulator = 0f;
        }
    }

    public virtual void Attack()
    {
        if (_isAttacking || Time.time < NextAttackTime)
        {
            Debug.Log($"[{gameObject.name}] Cannot attack: IsAttacking={_isAttacking}, NextAttackTime={NextAttackTime:F3}, CurrentTime={Time.time:F3}");
            return;
        }

        if (_currentAttack >= _maxAttack - 1) _currentAttack = -1;
        _currentAttack = Mathf.Min(_currentAttack + 1, _maxAttack - 1);
        _isAttacking = true;

        int attackIndex = _currentAttack < attacks.Length ? _currentAttack : attacks.Length - 1;
        AttackData attackData = attacks[attackIndex];
        float adjustedAnimDuration = attackData.animationDuration / attackSpeed;

        animator.speed = attackSpeed;
        string animTrigger = $"Attack{_currentAttack}";
        animator.SetTrigger(animTrigger);
        Debug.Log($"[{gameObject.name}] Attack triggered: Index={_currentAttack}, Trigger={animTrigger}, AnimDuration={adjustedAnimDuration:F3}s, AttackSpeed={attackSpeed}");

        _attackCoroutine = StartCoroutine(PerformAttack(attackData));

        NextAttackTime = Time.time + adjustedAnimDuration;
        Debug.Log($"[{gameObject.name}] NextAttackTime set to {NextAttackTime:F3}");
    }

    protected virtual IEnumerator PerformAttack(AttackData attackData)
    {
        float adjustedAnimDuration = attackData.animationDuration / attackSpeed;
        Debug.Log($"[{gameObject.name}] PerformAttack started: AnimDuration={adjustedAnimDuration:F3}s");

        // Khởi tạo danh sách Collider
        List<ColliderData> colliders = attackData.colliderDatas;
        List<float> endTimes = new();

        _colliderCoroutines.Clear();
        foreach (var colData in colliders)
        {
            if (colData.collider == null)
            {
                Debug.LogWarning($"[{gameObject.name}] ColliderData has null collider!");
                continue;
            }
            Coroutine colRoutine = StartCoroutine(ActivateColliderWithDelay(colData));
            _colliderCoroutines.Add(colRoutine);

            float totalTime = (colData.activationDelay + 0.1f) / attackSpeed; // 0.1f là thời gian Collider bật
            endTimes.Add(totalTime);
        }

        // Chờ đến khi tất cả Collider đã tắt
        float colliderPhaseDuration = endTimes.Any() ? endTimes.Max() : 0f;
        Debug.Log($"[{gameObject.name}] Waiting for Collider phase: {colliderPhaseDuration:F3}s");
        yield return new WaitForSeconds(colliderPhaseDuration);

        // Mở cửa sổ combo để theo dõi di chuyển
        _lastAttackTime = ComboResetDelay;
        _canResetCombo = true;
        _isMovingDuringComboWindow = false;
        _moveTimeAccumulator = 0f;
        Debug.Log($"[{gameObject.name}] Combo window opened: MoveInterruptWindow={MoveInterruptWindow:F3}s, CanResetCombo={_canResetCombo}");

        // Nếu không di chuyển, chờ hết animation
        float remainingTime = adjustedAnimDuration - colliderPhaseDuration;
        if (remainingTime > 0)
        {
            Debug.Log($"[{gameObject.name}] Waiting for remaining animation: {remainingTime:F3}s");
            yield return new WaitForSeconds(remainingTime);
        }

        // Reset trạng thái nếu không bị ngắt bởi di chuyển
        if (_isAttacking)
        {
            _isAttacking = false;
            _canResetCombo = false;
            animator.speed = 1f;
            Debug.Log($"[{gameObject.name}] Attack completed: IsAttacking={_isAttacking}, CanResetCombo={_canResetCombo}");
        }
    }

    private void ResetAttack()
    {
        // Stop attack coroutine
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
            Debug.Log($"[{gameObject.name}] Attack coroutine stopped");
        }

        // Stop all collider coroutines
        foreach (var c in _colliderCoroutines)
        {
            if (c != null)
                StopCoroutine(c);
        }
        _colliderCoroutines.Clear();
        Debug.Log($"[{gameObject.name}] All collider coroutines stopped");

        // Reset trạng thái
        _isAttacking = false;
        _canResetCombo = false;
        _currentAttack = -1;
        _isMovingDuringComboWindow = false;
        _moveTimeAccumulator = 0f;
        animator.speed = 1;

        foreach (var atk in attacks)
        {
            foreach (var colData in atk.colliderDatas)
            {
                if (colData.collider != null)
                    colData.collider.enabled = false;
            }
        }
        Debug.Log($"[{gameObject.name}] Attack reset: CurrentAttack={_currentAttack}, IsAttacking={_isAttacking}");
    }

    protected IEnumerator ActivateColliderWithDelay(ColliderData colData)
    {
        Debug.Log($"[{gameObject.name}] Activating collider: Delay={colData.activationDelay / attackSpeed:F3}s");
        yield return new WaitForSeconds(colData.activationDelay / attackSpeed);

        if (colData.collider != null)
        {
            colData.collider.enabled = true;
            var bullet = colData.collider.GetComponent<Bullet>();
            if (bullet != null)
                bullet.Init(GetDamage());
            Debug.Log($"[{gameObject.name}] Collider enabled: Damage={GetDamage()}");
        }

        yield return new WaitForSeconds(0.1f / attackSpeed); // Collider tắt sau 0.1s

        if (colData.collider != null)
        {
            colData.collider.enabled = false;
            Debug.Log($"[{gameObject.name}] Collider disabled");
        }
    }

    public void SetJoystick(Joystick joystick)
    {
        _joystick = joystick;
        Debug.Log($"[{gameObject.name}] Joystick set");
    }
}