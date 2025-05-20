using System.Collections;
using UnityEngine;

public enum TeamType
{
    TeamA,
    TeamB
}
[System.Serializable]
public struct AttackData
{
    public float animationDuration; // Tổng thời gian animation
    public float colliderActivationTime; // Thời điểm kích hoạt collider
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
    protected AttackData[] attacks = {
        new AttackData { animationDuration = 0.5f, colliderActivationTime = 0.25f },
        new AttackData { animationDuration = 1.0f, colliderActivationTime = 0.5f },
        new AttackData { animationDuration = 0.75f, colliderActivationTime = 0.375f } };

    [Header("Character Level Scaling")]
    [SerializeField] protected float healthScale = 0.2f;
    [SerializeField] protected float dameScale = 0.1f;
    [SerializeField] protected float moveSpeedScale = 0.1f;

    [Header("Attack chain Settings")]
    [SerializeField] protected float attackMultiplier = 0.2f;

    [Header("References")]
    [SerializeField] protected Animator animator;
    [SerializeField] protected Collider weaponCollider;

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

    public Joystick joystick;

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
        _maxAttack = 3;
    }
    public void TakeDamage(float damage)
    {
        _currentHealth -= damage;
        if (_currentHealth <= 0)
        {
            _isAlive = false;
            Debug.Log($"{gameObject.name} has been defeated!");
        }
    }
    public float GetDamage()
    {
        float comboBonus = 1 + (_currentAttack * attackMultiplier);
        return baseDamage * (1 + _level * dameScale) * comboBonus;
    }
    public virtual void UpdateCharacter()
    {
        if (!_isAlive) return;

        Vector3 direction = new (joystick.Horizontal, 0, joystick.Vertical);

        if (direction.magnitude > 0.1f)
        {
            // Move
            direction.Normalize();
            transform.forward = direction;
            transform.Translate(direction * _currentMoveSpeed * Time.deltaTime, Space.World);

            // Play run anim nếu có
            animator.SetBool("IsRunning", true);
        }
        else
        {
            // Dừng, tấn công nếu có target trong phạm vi
            animator.SetBool("IsRunning", false);
            if (_target != null && Vector3.Distance(transform.position, _target.transform.position) <= attackRange)
            {
                Attack();
            }
        }
    }
    public virtual void Attack()
    {
        if (!IsAlive || _isAttacking || Time.time < NextAttackTime) return;

        // Tăng combo
        _currentAttack = Mathf.Min(_currentAttack + 1, _maxAttack - 1);
        _isAttacking = true;

        // Lấy dữ liệu đòn tấn công
        int attackIndex = _currentAttack < attacks.Length ? _currentAttack : attacks.Length - 1;
        float animationDuration = attacks[attackIndex].animationDuration / attackSpeed;
        float colliderActivationTime = attacks[attackIndex].colliderActivationTime / attackSpeed;

        // Phát animation
        string animTrigger = $"Attack{_currentAttack}";
        animator.SetTrigger(animTrigger);
        animator.speed = attackSpeed;

        // Bắt đầu coroutine để xử lý sát thương
        StartCoroutine(PerformAttack(animationDuration, colliderActivationTime));

        // Cập nhật thời gian tấn công tiếp theo
        NextAttackTime = Time.time + animationDuration;
    }

    protected virtual IEnumerator PerformAttack(float animationDuration, float colliderActivationTime)
    {
        // Chờ đến thời điểm kích hoạt collider
        yield return new WaitForSeconds(colliderActivationTime);

        // Kích hoạt collider
        if (weaponCollider != null)
            weaponCollider.enabled = true;

        // Tắt collider sau một khoảng ngắn
        float colliderActiveDuration = 0.1f / attackSpeed; // Thời gian collider bật
        yield return new WaitForSeconds(colliderActiveDuration);

        if (weaponCollider != null)
            weaponCollider.enabled = false;

        // Chờ hoàn thành animation
        float remainingTime = animationDuration - colliderActivationTime - colliderActiveDuration;
        if (remainingTime > 0)
            yield return new WaitForSeconds(remainingTime);

        // Reset trạng thái
        _isAttacking = false;
        animator.speed = 1f;

        if (_currentAttack >= _maxAttack - 1)
        {
            _currentAttack = -1;
        }
    }
}

