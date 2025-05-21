using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Unity.VisualScripting;
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
public static class AnimHash
{
    public static readonly int IsMoving = Animator.StringToHash("IsMoving");
    public static readonly int Die = Animator.StringToHash("Die");
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

    private const float MoveInterruptWindow = 0.5f; // Thời gian cửa sổ di chuyển;

    protected float _currentHealth;
    protected float _maxHealth;
    protected float _currentMoveSpeed;
    protected bool _isAlive;
    protected TeamType _team;
    protected int _currentAttack;
    protected int _level;
    protected bool _isAttacking;
    protected int _maxAttack = 3;
    private bool _canResetCombo;
    private float _moveTimeAccumulator = 0f; // Theo dõi thời gian di chuyển

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
        _moveTimeAccumulator = 0f;
        _maxAttack = attacks.Length;
        collide.Init(this);
    }

    public void TakeDamage(float damage)
    {
        if (!_isAlive) return;
        _currentHealth -= damage;
        AudioManager.Instance.PlaySoundEffect(AudioName.Hit);
        if (_currentHealth <= 0)
        {
            _isAlive = false;
            animator.SetTrigger(AnimHash.Die);
            AudioManager.Instance.PlaySoundEffect(AudioName.Die);
            StartCoroutine(GameManager.Instance.DelayReturnToPool(this, _team));
        }
    }

    public float GetDamage()
    {
        float comboBonus = 1 + (_currentAttack * attackMultiplier);
        float damage = baseDamage * (1 + _level * dameScale) * comboBonus;
        return damage;
    }
    public virtual void UpdateCharacter()
    {
        if (!_isAlive) return;

        var movementVector = _joystick.Direction;
        if (movementVector.magnitude > 0.01f)
        {
            Vector3 direction = new(movementVector.x, 0, movementVector.y);
            rb.velocity = _currentMoveSpeed * direction;

            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 8 * Time.deltaTime);

            animator.speed = _currentMoveSpeed;
            animator.SetBool(AnimHash.IsMoving, true);
        }
        else
        {
            rb.velocity = Vector3.zero;

            animator.speed = 1;
            animator.SetBool(AnimHash.IsMoving, false);

            Attack();
        }
    }
    public virtual void Attack()
    {
        if (_isAttacking) return;

        _isAttacking = true;
        _canResetCombo = false;

        if (_currentAttack >= _maxAttack - 1) _currentAttack = -1;
        _currentAttack = Mathf.Min(_currentAttack + 1, _maxAttack - 1);

        animator.speed = attackSpeed;
        string animTrigger = $"Attack{_currentAttack}";
        //animator.SetTrigger(animTrigger);
        animator.CrossFadeInFixedTime(animTrigger, 0.3f);

        AttackData attackData = attacks[_currentAttack];
        _attackCoroutine = StartCoroutine(PerformAttack(attackData));
    }
    protected virtual IEnumerator PerformAttack(AttackData attackData)
    {
        float adjustedAnimDuration = attackData.animationDuration / attackSpeed;

        // Khởi tạo danh sách Collider
        List<ColliderData> colliders = attackData.colliderDatas;
        List<float> endTimes = new();

        _colliderCoroutines.Clear();
        foreach (var colData in colliders)
        {
            if (colData.collider == null)
            {
                Debug.LogError($"[{gameObject.name}] ColliderData has null collider!");
                continue;
            }
            Coroutine colRoutine = StartCoroutine(ActivateColliderWithDelay(colData));
            _colliderCoroutines.Add(colRoutine);

            float totalTime = colData.activationDelay;
            endTimes.Add(totalTime);
        }

        // Chờ đến khi tất cả Collider đã enable
        float colliderPhaseDuration = endTimes.Any() ? endTimes.Max() : 0f;
        yield return new WaitForSeconds(colliderPhaseDuration);

        // Mở cửa sổ combo để theo dõi di chuyển
        _canResetCombo = true;
        _moveTimeAccumulator = 0f;

        // Theo dõi di chuyển trong vòng 0.5s
        float remainingTime = adjustedAnimDuration - colliderPhaseDuration;
        float timeElapsed = 0f;
        bool hasMoved = false;

        while (timeElapsed < remainingTime)
        {
            if (_joystick.Direction.magnitude > 0.01f)
            {
                hasMoved = true;
                _moveTimeAccumulator += Time.deltaTime;

                // Nếu di chuyển quá 0.5s, reset combo
                if (_moveTimeAccumulator > MoveInterruptWindow)
                {
                    ResetAttack();
                    yield break;
                }
            }
            else if (hasMoved && _moveTimeAccumulator < MoveInterruptWindow)
            {
                // Nếu đã di chuyển nhưng dưới 0.5s và dừng lại, cho phép attack tiếp theo
                _isAttacking = false;
                _canResetCombo = false;
                animator.speed = 1f;
                yield break; // Thoát để có thể gọi Attack() ngay lập tức
            }

            timeElapsed += Time.deltaTime;
            yield return null;
        }

        // Nếu không di chuyển hoặc không bị ngắt, reset trạng thái bình thường
        if (_isAttacking)
        {
            _isAttacking = false;
            _canResetCombo = false;
            animator.speed = 1f;
        }
    }
    private void ResetAttack()
    {
        // Stop attack coroutine
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        // Stop all collider coroutines
        foreach (var c in _colliderCoroutines)
        {
            if (c != null)
                StopCoroutine(c);
        }
        _colliderCoroutines.Clear();

        // Reset trạng thái
        _isAttacking = false;
        _canResetCombo = false;
        _currentAttack = -1;
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
    }

    protected IEnumerator ActivateColliderWithDelay(ColliderData colData)
    {
        yield return new WaitForSeconds(colData.activationDelay / attackSpeed);

        if (colData.collider != null)
        {
            colData.collider.enabled = true;
            var bullet = colData.collider.GetComponent<Bullet>();
            if (bullet == null)
            {
                Debug.LogError("Cannot find bullet!");
                yield break;
            }
            bullet.Init(GetDamage());
        }

        yield return new WaitForSeconds(0.1f); // Collider tắt sau 0.1s

        if (colData.collider != null)
        {
            colData.collider.enabled = false;
        }
    }

    public void SetJoystick(Joystick joystick)
    {
        _joystick = joystick;
    }
}