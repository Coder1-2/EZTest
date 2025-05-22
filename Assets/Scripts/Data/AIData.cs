using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public enum AILevel { Easy, Medium, Hard }

public class AIData : CharacterData
{
    public AILevel AILevel => _aiLevel;

    private AILevel _aiLevel;
    private float _separationStrength = 1f;
    private float _avoidanceRadius = 1f;
    private float _targetUpdateTimer = 0f;
    private const float _targetUpdateInterval = 0.5f; 

    public override void Init(int level, TeamType teamType, AILevel aILevel)
    {
        base.Init(level, teamType, aILevel);
        _aiLevel = aILevel;
        _maxAttack = (int)_aiLevel + 1;
    }

    public override void UpdateCharacter()
    {
        if (!_isAlive || _isStunned) return;

        FindTarget();
        switch (_aiLevel)
        {
            case AILevel.Easy:
                EasyAI();
                break;
            case AILevel.Medium:
                MediumAI();
                break;
            case AILevel.Hard:
                HardAI();
                break;
        }
    }

    public override void Attack()
    {
        if (_isAttacking || _isStunned) return;

        _isAttacking = true;
        animator.SetBool(AnimHash.IsMoving, false);

        _attackCts = new CancellationTokenSource();
        AttackWithRotation(_attackCts.Token).Forget();
    }

    private async UniTask AttackWithRotation(CancellationToken cancellationToken)
    {
        if (_target != null && _target.IsAlive)
        {
            float rotationTime = 0f;
            float maxRotationTime = 0.2f;
            Vector3 directionToTarget = (_target.transform.position - transform.position).normalized;
            directionToTarget.y = 0;

            if (directionToTarget != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(directionToTarget, Vector3.up);
                while (rotationTime < maxRotationTime)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 15 * Time.deltaTime);
                    rotationTime += Time.deltaTime;
                    await UniTask.Yield(cancellationToken);
                }
                transform.rotation = targetRotation;
            }
        }

        if (_currentAttack >= _maxAttack - 1) _currentAttack = -1;
        _currentAttack = Mathf.Min(_currentAttack + 1, _maxAttack - 1);

        animator.speed = attackSpeed;
        string animTrigger = $"Attack{_currentAttack}";
        animator.CrossFadeInFixedTime(animTrigger, 0.3f);

        AttackData attackData = attacks[_currentAttack];
        await PerformAttack(attackData, cancellationToken);
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
            UniTask colTask = ActivateColliderWithDelay(attackData, colData, cancellationToken);
            _colliderTasks.Add(colTask);
            endTimes.Add(colData.activationDelay);
        }

        float colliderPhaseDuration = endTimes.Max();
        await UniTask.Delay((int)(colliderPhaseDuration * 1000), cancellationToken: cancellationToken);

        float remainingTime = adjustedAnimDuration - colliderPhaseDuration;

        if (_aiLevel == AILevel.Hard)
        {
            float timeElapsed = 0f;
            while (timeElapsed < remainingTime)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (_target == null || !_target.IsAlive)
                {
                    await UniTask.Delay(200, cancellationToken: cancellationToken);
                    _isAttacking = false;
                    animator.speed = 1f;
                    return;
                }

                float distance = (transform.position - _target.transform.position).sqrMagnitude;
                if (distance > attackRange * attackRange)
                {
                    await UniTask.Delay(200, cancellationToken: cancellationToken);
                    _isAttacking = false;
                    animator.speed = 1f;
                    return;
                }

                await UniTask.Delay(200, cancellationToken: cancellationToken);
                timeElapsed += 0.2f;
            }
        }
        else if (remainingTime > 0)
        {
            await UniTask.Delay((int)(remainingTime * 1000), cancellationToken: cancellationToken);
        }

        _isAttacking = false;
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
                Debug.LogError("Cannot find bullet!");
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

    private void EasyAI()
    {
        if (_target != null)
        {
            if (_isAttacking) return;

            if ((transform.position - _target.transform.position).sqrMagnitude <= attackRange * attackRange)
            {
                rb.velocity = Vector3.zero;
                Attack();
            }
            else
            {
                MoveToTarget();
            }
        }
    }

    private void MediumAI()
    {
        if (_target != null)
        {
            if (_isAttacking) return;

            var distance = (transform.position - _target.transform.position).sqrMagnitude;
            int nearbyEnemies = CountNearbyEnemies();
            float optimalDistance = attackRange * attackRange;

            if (distance <= optimalDistance && nearbyEnemies <= 2)
            {
                rb.velocity = Vector3.zero;
                Attack();
            }
            else
            {
                _currentAttack = -1;
                MoveToTarget();
            }
        }
    }

    private void HardAI()
    {
        if (_target != null)
        {
            if (_isAttacking) return;

            var distance = (transform.position - _target.transform.position).sqrMagnitude;
            float optimalDistance = attackRange * attackRange;

            // Giới hạn số AI tấn công cùng mục tiêu
            int attackingAllies = CountAlliesAttackingTarget();
            if (distance <= optimalDistance && attackingAllies < 5)
            {
                rb.velocity = Vector3.zero;
                Attack();
            }
            else
            {
                MoveToTarget();
            }
        }
    }

    private void FindTarget()
    {
        _targetUpdateTimer += Time.deltaTime;
        if (_targetUpdateTimer < _targetUpdateInterval && _target != null && _target.IsAlive)
        {
            Debug.Log($"[{gameObject.name}] Target still valid: {_target.gameObject.name}, skipping update");
            return;
        }

        _targetUpdateTimer = 0f;

        float closestDistance = float.MaxValue;
        CharacterData closestTarget = null;
        CharacterData weakestTarget = null;
        float lowestHealth = float.MaxValue;
        CharacterData lowHealthTarget = null; // For Medium: Target with health < _currentHealth

        List<CharacterData> targetTeam = _team == TeamType.TeamA
            ? GameManager.Instance.TeamB
            : GameManager.Instance.TeamA;

        foreach (var target in targetTeam)
        {
            if (!target.IsAlive) continue;

            float distance = (transform.position - target.transform.position).sqrMagnitude;

            // Easy: Chọn mục tiêu gần nhất
            if (_aiLevel == AILevel.Easy)
            {
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = target;
                }
            }
            // Medium: Ưu tiên mục tiêu có máu thấp hơn _currentHealth, nếu không thì gần nhất
            else if (_aiLevel == AILevel.Medium)
            {
                if (target.CurrentHealth < _currentHealth)
                {
                    if (target.CurrentHealth < lowestHealth)
                    {
                        lowestHealth = target.CurrentHealth;
                        lowHealthTarget = target;
                    }
                }
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = target;
                }
            }
            // Hard: Ưu tiên mục tiêu nhắm vào đồng đội (gần nhất), nếu không thì máu thấp nhất
            else if (_aiLevel == AILevel.Hard)
            {
                if (target.Target != null && target.Target.Team == _team)
                {
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTarget = target;
                    }
                }
                if (target.CurrentHealth < lowestHealth)
                {
                    lowestHealth = target.CurrentHealth;
                    weakestTarget = target;
                }
            }
        }

        if (_aiLevel == AILevel.Easy)
        {
            _target = closestTarget;
        }
        else if (_aiLevel == AILevel.Medium)
        {
            _target = lowHealthTarget != null ? lowHealthTarget : closestTarget;
        }
        else if (_aiLevel == AILevel.Hard)
        {
            _target = closestTarget != null ? closestTarget : weakestTarget;
        }
    }

    private void MoveToTarget()
    {
        if (_target == null) return;

        var direction = (_target.transform.position - transform.position).normalized;

        Vector3 avoidance = GetAvoidanceDirection();
        direction += avoidance;
        direction = direction.normalized;

        rb.velocity = _currentMoveSpeed * direction;

        if (rb.velocity.magnitude > 0.1f)
        {
            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 8 * Time.deltaTime);
        }

        animator.SetBool(AnimHash.IsMoving, true);
    }

    private Vector3 GetAvoidanceDirection()
    {
        Vector3 avoidance = Vector3.zero;
        Collider[] nearbyAllies = Physics.OverlapSphere(transform.position, _avoidanceRadius, LayerMask.GetMask("Character"));
        foreach (var ally in nearbyAllies)
        {
            if (ally.gameObject == gameObject) continue;
            float distance = (transform.position - ally.transform.position).sqrMagnitude;
            if (distance < _avoidanceRadius * _avoidanceRadius)
            {
                Vector3 away = transform.position - ally.transform.position;
                avoidance += away.normalized / distance;
            }
        }
        return avoidance * _separationStrength;
    }

    private int CountNearbyEnemies()
    {
        int count = 0;
        List<CharacterData> targetTeam = _team == TeamType.TeamA
            ? GameManager.Instance.TeamB
            : GameManager.Instance.TeamA;

        foreach (var target in targetTeam)
        {
            if (!target.IsAlive) continue;
            if ((transform.position - target.transform.position).sqrMagnitude <= attackRange * attackRange * 4)
            {
                count++;
            }
        }
        return count;
    }

    private int CountAlliesAttackingTarget()
    {
        int count = 0;
        List<CharacterData> team = _team == TeamType.TeamA
            ? GameManager.Instance.TeamA
            : GameManager.Instance.TeamB;
        foreach (var ally in team)
        {
            if (ally == this || !ally.IsAlive) continue;
            if (ally.Target == _target && ally.IsAttacking) count++;
        }
        return count;
    }
}