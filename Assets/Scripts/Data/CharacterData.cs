using Cysharp.Threading.Tasks;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
    public float animationDuration;
    public List<ColliderData> colliderDatas;
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

    public bool IsAttacking => _isAttacking;
    public TeamType Team => _team;
    public float CurrentHealth => _currentHealth;
    public bool IsAlive => _isAlive;
    public CharacterData Target => _target;
    protected CharacterData _target;

    private const float MoveInterruptWindow = 0.5f;

    protected float _currentHealth;
    protected float _maxHealth;
    protected float _currentMoveSpeed;
    protected bool _isAlive;
    protected TeamType _team;
    protected int _currentAttack;
    protected int _level;
    protected bool _isAttacking;
    protected int _maxAttack = 3;
    protected bool _canResetCombo;
    private float _moveTimeAccumulator = 0f;

    private Joystick _joystick;
    protected CancellationTokenSource _attackCts;
    protected List<UniTask> _colliderTasks = new();

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
            ResetAttack();
            GameManager.Instance.DelayReturnToPool(this, _team).Forget();
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
        animator.CrossFadeInFixedTime(animTrigger, 0.3f);

        AttackData attackData = attacks[_currentAttack];
        _attackCts = new CancellationTokenSource();
        PerformAttack(attackData, _attackCts.Token).Forget();
    }

    private async UniTask PerformAttack(AttackData attackData, CancellationToken cancellationToken)
    {
        float adjustedAnimDuration = attackData.animationDuration / attackSpeed;
        List<ColliderData> colliders = attackData.colliderDatas;
        List<float> endTimes = new();

        _colliderTasks.Clear();
        foreach (var colData in colliders)
        {
            if (colData.collider == null)
            {
                Debug.LogError($"[{gameObject.name}] ColliderData has null collider!");
                continue;
            }
            UniTask colTask = ActivateColliderWithDelay(colData, cancellationToken);
            _colliderTasks.Add(colTask);
            endTimes.Add(colData.activationDelay);
        }

        float colliderPhaseDuration = endTimes.Max();
        await UniTask.Delay((int)(colliderPhaseDuration * 1000), cancellationToken: cancellationToken);

        _canResetCombo = true;
        _moveTimeAccumulator = 0f;
        float remainingTime = adjustedAnimDuration - colliderPhaseDuration;
        float timeElapsed = 0f;
        bool hasMoved = false;

        while (timeElapsed < remainingTime)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (_joystick.Direction.magnitude > 0.01f)
            {
                hasMoved = true;
                _moveTimeAccumulator += Time.deltaTime;

                if (_moveTimeAccumulator > MoveInterruptWindow)
                {
                    ResetAttack();
                    return;
                }
            }
            else if (hasMoved && _moveTimeAccumulator < MoveInterruptWindow)
            {
                _isAttacking = false;
                _canResetCombo = false;
                animator.speed = 1f;
                return;
            }

            await UniTask.Yield(cancellationToken);
            timeElapsed += Time.deltaTime;
        }

        _isAttacking = false;
        _canResetCombo = false;
        animator.speed = 1f;
    }

    private async UniTask ActivateColliderWithDelay(ColliderData colData, CancellationToken cancellationToken)
    {
        await UniTask.Delay((int)(colData.activationDelay * 1000 / attackSpeed), cancellationToken: cancellationToken);

        if (colData.collider != null)
        {
            colData.collider.enabled = true;
            var bullet = colData.collider.GetComponent<Bullet>();
            if (bullet == null)
            {
                Debug.LogError("Cannot find bullet!");
                return;
            }
            bullet.Init(GetDamage());
        }

        await UniTask.Delay(100, cancellationToken: cancellationToken);

        if (colData.collider != null)
        {
            colData.collider.enabled = false;
        }
    }

    protected void ResetAttack()
    {
        if (_attackCts != null)
        {
            _attackCts.Cancel();
            _attackCts.Dispose();
            _attackCts = null;
        }

        _colliderTasks.Clear();

        _isAttacking = false;
        _canResetCombo = false;
        _currentAttack = -1;
        _moveTimeAccumulator = 0f;
        animator.speed = 1f;

        foreach (var atk in attacks)
        {
            foreach (var colData in atk.colliderDatas)
            {
                if (colData.collider != null)
                    colData.collider.enabled = false;
            }
        }
    }

    public void SetJoystick(Joystick joystick)
    {
        _joystick = joystick;
    }
}