using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.TextCore.Text;

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
    private float _separationStrength = 3f;
    private float _avoidanceRadius = 1.5f;
    public override void Init(int level, TeamType teamType)
    {
        base.Init(level, teamType);
        _aiLevel = DetermineAILevel();
        _maxAttack = (int)_aiLevel + 1;
    }
    private AILevel DetermineAILevel()
    {
        float[] weights = new float[3]; // [Easy, Medium, Hard]

        if (_level <= 2) // Level 1-3: Ưu tiên Easy
        {
            weights[0] = 0.7f; // 70% Easy
            weights[1] = 0.3f; // 30% Medium
            weights[2] = 0.0f; // 0% Hard
        }
        else if (_level <= 5) // Level 4-6: Ưu tiên Medium
        {
            weights[0] = 0.2f; // 20% Easy
            weights[1] = 0.6f; // 60% Medium
            weights[2] = 0.2f; // 20% Hard
        }
        else // Level 7-9: Ưu tiên Hard
        {
            weights[0] = 0.1f; // 10% Easy
            weights[1] = 0.3f; // 30% Medium
            weights[2] = 0.6f; // 60% Hard
        }

        float totalWeight = weights[0] + weights[1] + weights[2];
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
    // ===========================
    // EASY AI Logic
    // ===========================
    private void EasyAI()
    {
        if (_target != null)
        {
            if (Vector3.Distance(transform.position, _target.transform.position) <= attackRange)
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

    // ===========================
    // MEDIUM AI Logic
    // ===========================
    private void MediumAI()
    {
        if(_target != null)
        {
            if (_currentHealth < _maxHealth * 0.5f)
            {
                Vector3 evadeDirection = (transform.position - _target.transform.position).normalized;
                transform.position += evadeDirection * _currentMoveSpeed * Time.deltaTime * 1.5f;
            }
            else
            {
                var distance = (transform.position - _target.transform.position).sqrMagnitude;
                if (distance <= attackRange * attackRange)
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
    }

    // ===========================
    // HARD AI Logic
    // ===========================
    private void HardAI()
    {
        if (_target != null)
        {
            if (_currentHealth < _target.CurrentHealth)
            {
                Vector3 evadeDirection = (transform.position - _target.transform.position).normalized;
                transform.position += _currentMoveSpeed * 2f * Time.deltaTime * evadeDirection;
            }
            else
            {
                var distance = (transform.position - _target.transform.position).sqrMagnitude;
                if (distance <= attackRange * attackRange)
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
    }
    private void FindTarget()
    {
        float closestDistance = float.MaxValue;
        CharacterData closestTarget = null;

        List<CharacterData> targetTeam = _team == TeamType.TeamA
            ? GameManager.Instance.TeamB
            : GameManager.Instance.TeamA;

        var count = targetTeam.Count;
        for (var i = 0; i < count; i ++)
        {
            var target = targetTeam[i];
            if (!target.IsAlive) continue;

            var distance = (transform.position - _target.transform.position).sqrMagnitude;
            if (distance < closestDistance && distance <= detectRange * detectRange)
            {
                if (_aiLevel == AILevel.Easy)
                {
                    _target = target;
                    return;
                }
                closestDistance = distance;
                closestTarget = target;
            }
        }

        _target = closestTarget;
    }
    private void MoveToTarget()
    {
        // Tính hướng đến mục tiêu
        Vector3 direction = (_target.transform.position - transform.position).normalized;

        // Thêm lực né tránh
        Vector3 avoidance = GetAvoidanceDirection();
        direction += avoidance;
        direction = direction.normalized;

        // Xoay nhân vật về hướng di chuyển
        if (direction.magnitude > 0.1f)
        {
            var targetRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10 * Time.deltaTime);
        }

        // Di chuyển
        rb.velocity = _currentMoveSpeed * direction;

        // Cập nhật animation
        animator.SetBool("IsRunning", direction.magnitude > 0.1f);
    }
    private Vector3 GetAvoidanceDirection()
    {
        Vector3 avoidance = Vector3.zero;

        List<CharacterData> team = _team == TeamType.TeamA
            ? GameManager.Instance.TeamA
            : GameManager.Instance.TeamB;

        var count = team.Count;
        for (var i = 0; i < count; i++)
        {
            var other = team[i];

            if (other == this) continue;

            float distance = (transform.position - _target.transform.position).sqrMagnitude;

            if (distance < _avoidanceRadius * _avoidanceRadius)
            {
                Vector3 away = transform.position - other.transform.position;
                avoidance += away.normalized / distance;
            }
        }

        return avoidance * _separationStrength;
    }
}
