using Cysharp.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using UnityEngine;

public enum AILevel
{
    Easy,
    Medium,
    Hard
}

public class AIData : CharacterData
{
    public AILevel AILevel => _aiLevel;

    private AILevel _aiLevel;
    private float _separationStrength = 0.5f;
    private float _avoidanceRadius = 0.2f;
    private float _targetUpdateTimer = 0f;
    private const float _targetUpdateInterval = 0.5f; // Cập nhật mục tiêu mỗi 0.5s

    public override void Init(int level, TeamType teamType)
    {
        base.Init(level, teamType);
        _aiLevel = DetermineAILevel();
        _maxAttack = (int)_aiLevel + 1;
    }

    private AILevel DetermineAILevel()
    {
        return AILevel.Hard; // Giữ logic hiện tại, có thể bật lại random sau
        /*
        float[] weights = new float[3]; // [Easy, Medium, Hard]

        if (_level <= 2)
        {
            weights[0] = 0.7f; weights[1] = 0.3f; weights[2] = 0.0f;
        }
        else if (_level <= 5)
        {
            weights[0] = 0.2f; weights[1] = 0.6f; weights[2] = 0.2f;
        }
        else
        {
            weights[0] = 0.1f; weights[1] = 0.3f; weights[2] = 0.6f;
        }

        float totalWeight = weights.Sum();
        float randomValue = Random.Range(0f, totalWeight);
        float sum = 0f;

        for (int i = 0; i < weights.Length; i++)
        {
            sum += weights[i];
            if (randomValue <= sum)
            {
                return (AILevel)i;
            }
        }
        return AILevel.Medium;
        */
    }

    public override void UpdateCharacter()
    {
        if (!_isAlive) return;

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
        if (_isAttacking) return;

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
            UniTask colTask = ActivateColliderWithDelay(colData, cancellationToken);
            _colliderTasks.Add(colTask);
            endTimes.Add(colData.activationDelay);
        }

        float colliderPhaseDuration = endTimes.Any() ? endTimes.Max() : 0f;
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
            float distanceToTarget = Mathf.Sqrt(distance);
            int nearbyEnemies = CountNearbyEnemies();
            float optimalDistance = attackRange * 0.9f;

            if (distanceToTarget >= optimalDistance * 0.7f &&
                distanceToTarget <= optimalDistance &&
                nearbyEnemies <= 2)
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
            float distanceToTarget = Mathf.Sqrt(distance);
            bool allyAttacking = IsAllyAttackingTarget();
            float optimalDistance = attackRange;

            // Giới hạn số AI tấn công cùng mục tiêu
            int attackingAllies = CountAlliesAttackingTarget();
            if (distanceToTarget >= optimalDistance * 0.7f &&
                distanceToTarget <= optimalDistance &&
                allyAttacking &&
                attackingAllies < 5)
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
            return;

        _targetUpdateTimer = 0f;

        float closestDistance = float.MaxValue;
        CharacterData closestTarget = null;
        CharacterData weakestTarget = null;
        float lowestHealth = float.MaxValue;

        List<CharacterData> targetTeam = _team == TeamType.TeamA
            ? GameManager.Instance.TeamB
            : GameManager.Instance.TeamA;

        foreach (var target in targetTeam)
        {
            if (!target.IsAlive) continue;

            var distance = (transform.position - target.transform.position).sqrMagnitude;

            if (_aiLevel == AILevel.Easy)
            {
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestTarget = target;
                }
            }
            else if (_aiLevel == AILevel.Medium && target.CurrentHealth < lowestHealth)
            {
                lowestHealth = target.CurrentHealth;
                weakestTarget = target;
            }
            else if (_aiLevel == AILevel.Hard)
            {
                if (target.CurrentHealth < lowestHealth)
                {
                    lowestHealth = target.CurrentHealth;
                    weakestTarget = target;
                }
                if (target.Target != null && target.Target.Team == _team)
                {
                    if (distance < closestDistance)
                    {
                        closestDistance = distance;
                        closestTarget = target;
                    }
                }
            }
        }

        _target = _aiLevel == AILevel.Hard && closestTarget != null ? closestTarget : weakestTarget;
        if (_aiLevel == AILevel.Easy) _target = closestTarget;
    }

    private void MoveToTarget()
    {
        if (_target == null) return;

        Vector3 direction = Vector3.zero;
        float distanceToTarget = (transform.position - _target.transform.position).magnitude;

        if (_aiLevel == AILevel.Easy)
        {
            direction = (_target.transform.position - transform.position).normalized;
        }
        else if (_aiLevel == AILevel.Medium)
        {
            float optimalDistance = attackRange * 0.9f;
            if (distanceToTarget > optimalDistance)
            {
                direction = (_target.transform.position - transform.position).normalized;
            }
            else if (distanceToTarget < optimalDistance * 0.7f)
            {
                direction = (transform.position - _target.transform.position).normalized;
            }
        }
        else if (_aiLevel == AILevel.Hard)
        {
            Vector3 toTarget = _target.transform.position - transform.position;
            Vector3 flankDirection = Vector3.Cross(toTarget, Vector3.up).normalized;
            flankDirection *= Random.Range(-1f, 1f) > 0 ? 1f : -1f;
            direction = (toTarget.normalized + flankDirection * 0.5f).normalized;
        }

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

    private bool IsAllyAttackingTarget()
    {
        List<CharacterData> team = _team == TeamType.TeamA
            ? GameManager.Instance.TeamA
            : GameManager.Instance.TeamB;

        if (team.Count == 1) return true;
        foreach (var ally in team)
        {
            if (ally == this || !ally.IsAlive) continue;
            if (ally.Target == _target)
            {
                return true;
            }
        }
        return false;
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