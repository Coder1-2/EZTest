using Cysharp.Threading.Tasks;
using Microlight.MicroBar;
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

public enum HitType
{
    Hit0,
    Hit1,
    Hit2,
    Hit3
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
    public HitType hitType;
    public float animationDuration;
    public List<ColliderData> colliderDatas;
}

public static class AnimHash
{
    public static readonly int IsMoving = Animator.StringToHash("IsMoving");
    public static readonly int Victory = Animator.StringToHash("Victory");
    public static readonly int Die = Animator.StringToHash("Die");
    public static readonly int Hit0 = Animator.StringToHash("Hit0");
    public static readonly int Hit1 = Animator.StringToHash("Hit1");
    public static readonly int Hit2 = Animator.StringToHash("Hit2");
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
    [SerializeField] protected MicroBar healthBar;

    public bool IsAttacking => _isAttacking;
    public TeamType Team => _team;
    public float CurrentHealth => _currentHealth;
    public bool IsAlive => _isAlive;
    public CharacterData Target => _target;
    protected CharacterData _target;

    private const float MoveInterruptWindow = 0.5f;
    private const float StunDuration = 0.5f;

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
    protected bool _isStunned;
    private bool _isStop;

    private Joystick _joystick;
    protected CancellationTokenSource _attackCts;
    protected List<UniTask> _colliderTasks = new();

    public virtual void Init(int level, TeamType teamType, AILevel aILevel)
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
        _isStunned = false;
        _isStop = false;
        _maxAttack = attacks.Length;

        collide.Init(this);
        ResetAttack();

        if(healthBar != null)
        {
            healthBar.gameObject.SetActive(true);
            healthBar.Initialize(_maxHealth);
        }

        GameManager.Instance.OnGameOver += OnGameOver;
    }
    private void OnGameOver(TeamType teamType)
    {
        healthBar.gameObject.SetActive(false);
        animator.SetBool(AnimHash.IsMoving, false);
        rb.velocity = Vector3.zero;
        ResetAttack();
        if (_team == teamType)
        {
            animator.SetTrigger(AnimHash.Victory);
        }
        else
        {
            animator.SetTrigger(AnimHash.Die);
        }
    }
    public void KnockBack(Vector3 direction, float force = 1.5f)
    {
        rb.AddForce(direction * force, ForceMode.VelocityChange);
    }
    public void TakeDamage(float damage, HitType hitType)
    {
        if (!_isAlive) return;

        _currentHealth -= damage;

        if (healthBar != null)
        {
            healthBar.UpdateBar(_currentHealth);
        }

        GameManager.Instance.CreateText(transform.position + new Vector3(0, 1, 0), Mathf.RoundToInt(damage).ToString());

        if (_currentHealth <= 0)
        {
            _isAlive = false;
            animator.SetTrigger(AnimHash.Die);
            ResetAttack();
            healthBar.gameObject.SetActive(false);
            GameManager.Instance.OnGameOver -= OnGameOver;
            GameManager.Instance.DelayReturnToPool(this, _team).Forget();
        }
        else
        {
            animator.SetBool(AnimHash.IsMoving, false);

            // Trigger animation theo HitType
            switch (hitType)
            {
                case HitType.Hit0:
                case HitType.Hit3:
                    animator.SetTrigger(AnimHash.Hit0);
                    AudioManager.Instance.PlaySoundEffect(AudioName.Hit1);
                    break;
                case HitType.Hit1:
                    animator.SetTrigger(AnimHash.Hit1);
                    AudioManager.Instance.PlaySoundEffect(AudioName.Hit3);
                    break;
                case HitType.Hit2:
                    animator.SetTrigger(AnimHash.Hit2);
                    AudioManager.Instance.PlaySoundEffect(AudioName.Hit2);
                    break;
            }

            // Kiểm tra xác suất stun
            float stunChance = hitType switch
            {
                HitType.Hit0 => 0.05f,
                HitType.Hit1 => 0.1f,
                HitType.Hit2 => 0.2f,
                HitType.Hit3 => 0.3f,
                _ => 0f
            };
            bool isStunned = Random.Range(0f, 1f) < stunChance;
            if (isStunned)
            {
                _isStunned = true;
                ResetAttack();
                rb.velocity = Vector3.zero; 
                ApplyStun().Forget();
            }
        }
    }
    public void UpdateHealthBarView()
    {
        if (healthBar == null) return;
        healthBar.transform.rotation = Quaternion.identity;
    }

    private async UniTask ApplyStun()
    {
        await UniTask.Delay((int)(StunDuration * 1000), cancellationToken: this.GetCancellationTokenOnDestroy());
        _isStunned = false;
    }

    public float GetDamage()
    {
        float comboBonus = 1 + (_currentAttack * attackMultiplier);
        float damage = baseDamage * (1 + _level * dameScale) * comboBonus;
        return damage;
    }

    public virtual void UpdateCharacter()
    {
        if (!_isAlive || _isStunned) return;

        var movementVector = _joystick.Direction;
        if (movementVector.magnitude > 0.01f)
        {
            _isStop = false;

            Vector3 direction = new(movementVector.x, 0, movementVector.y);
            rb.velocity = _currentMoveSpeed * direction;

            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 8 * Time.deltaTime);

            animator.speed = _currentMoveSpeed;
            animator.SetBool(AnimHash.IsMoving, true);

            if (_canResetCombo)
            {
                _moveTimeAccumulator += Time.deltaTime;

                if (_moveTimeAccumulator > MoveInterruptWindow)
                {
                    ResetAttack();
                }
            }
        }
        else
        {
            if (!_isStop)
            {
                rb.velocity = Vector3.zero;
                _isStop = true;
            }


            animator.speed = 1;
            animator.SetBool(AnimHash.IsMoving, false);

            Attack();
        }
    }

    public virtual void Attack()
    {
        if (_isAttacking || _isStunned)
        {
            return;
        }

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
                continue;
            }
            UniTask colTask = ActivateColliderWithDelay(attackData, colData, cancellationToken);
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

    private async UniTask ActivateColliderWithDelay(AttackData attackData, ColliderData colData, CancellationToken cancellationToken)
    {
        await UniTask.Delay((int)(colData.activationDelay * 1000 / attackSpeed), cancellationToken: cancellationToken);

        if (colData.collider != null)
        {
            colData.collider.enabled = true;
            var bullet = colData.collider.GetComponent<Bullet>();
            if (bullet == null)
            {
                return;
            }
            bullet.Init(GetDamage(), attackData.hitType);
        }

        await UniTask.Delay((int)(0.5f * 1000 / attackSpeed), cancellationToken: cancellationToken);

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