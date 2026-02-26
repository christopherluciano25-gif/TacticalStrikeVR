using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Archer Tower - Grid integrated, projectile built into same script
/// </summary>
public class Archer : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 60f;
    [SerializeField] private float attackDamage = 20f;
    [SerializeField] private int attackRangeInCells = 3; // GRID RANGE
    [SerializeField] private float attackCooldown = 1.5f;

    [Header("Projectile")]
    [SerializeField] private GameObject arrowPrefab;
    [SerializeField] private Transform arrowSpawnPoint;
    [SerializeField] private float arrowSpeed = 15f;

    [Header("Team")]
    public TeamSide team = TeamSide.Player;

    private TacticalGrid grid;
    private Vector2Int gridPosition;

    private float currentHealth;
    private bool isDead;
    private float lastAttackTime;

    private const float STRONG_MULTIPLIER = 2f;

    private void Start()
    {
        grid = FindObjectOfType<TacticalGrid>();
        currentHealth = maxHealth;

        if (grid == null)
        {
            Debug.LogError("No TacticalGrid found!");
            return;
        }

        // Snap to grid
        gridPosition = grid.WorldToGrid(transform.position);
        transform.position = grid.GridToWorld(gridPosition.x, gridPosition.y);

        // Register in grid
        grid.PlaceUnit(
            gridPosition.x,
            gridPosition.y,
            1,
            1,
            team,
            UnitType.ArcherTower,
            gameObject
        );

        // Create spawn point automatically if missing
        if (arrowSpawnPoint == null)
        {
            GameObject spawn = new GameObject("ArrowSpawn");
            spawn.transform.SetParent(transform);
            spawn.transform.localPosition = Vector3.up * 1.5f;
            arrowSpawnPoint = spawn.transform;
        }

        StartCoroutine(AILoop());
    }

    private IEnumerator AILoop()
    {
        while (!isDead)
        {
            UnitData target = FindTargetInRange();

            if (target != null)
            {
                FaceTarget(target.unitObject.transform.position);

                if (Time.time >= lastAttackTime + attackCooldown)
                {
                    Shoot(target);
                    lastAttackTime = Time.time;
                }
            }

            yield return new WaitForSeconds(0.25f);
        }
    }

    private UnitData FindTargetInRange()
    {
        TeamSide enemyTeam = team == TeamSide.Player ? TeamSide.Bot : TeamSide.Player;
        List<UnitData> enemies = grid.GetAllUnits(enemyTeam);

        UnitData bestTarget = null;
        int bestPriority = -1;
        int closestDistance = int.MaxValue;

        foreach (UnitData enemy in enemies)
        {
            int distance = Mathf.Abs(enemy.gridPosition.x - gridPosition.x) +
                           Mathf.Abs(enemy.gridPosition.y - gridPosition.y);

            if (distance > attackRangeInCells)
                continue;

            int priority = GetPriority(enemy.type);

            if (priority > bestPriority ||
               (priority == bestPriority && distance < closestDistance))
            {
                bestPriority = priority;
                closestDistance = distance;
                bestTarget = enemy;
            }
        }

        return bestTarget;
    }

    private int GetPriority(UnitType type)
    {
        switch (type)
        {
            case UnitType.Knight: return 5;
            case UnitType.ArcherTower: return 3;
            case UnitType.Wall: return 1;
            default: return 1;
        }
    }

    private void Shoot(UnitData target)
    {
        float finalDamage = attackDamage;

        if (target.type == UnitType.Knight)
            finalDamage *= STRONG_MULTIPLIER;

        if (arrowPrefab != null)
        {
            GameObject arrow = Instantiate(
                arrowPrefab,
                arrowSpawnPoint.position,
                Quaternion.identity
            );

            StartCoroutine(MoveArrow(arrow, target.unitObject, finalDamage));
        }
        else
        {
            ApplyDamage(target.unitObject, finalDamage);
        }
    }

    private IEnumerator MoveArrow(GameObject arrow, GameObject target, float damage)
    {
        float lifetime = 5f;
        float timer = 0f;

        while (arrow != null && target != null && timer < lifetime)
        {
            Vector3 dir = (target.transform.position - arrow.transform.position).normalized;
            arrow.transform.position += dir * arrowSpeed * Time.deltaTime;

            if (dir != Vector3.zero)
                arrow.transform.rotation = Quaternion.LookRotation(dir);

            if (Vector3.Distance(arrow.transform.position, target.transform.position) < 0.3f)
            {
                ApplyDamage(target, damage);
                Destroy(arrow);
                yield break;
            }

            timer += Time.deltaTime;
            yield return null;
        }

        if (arrow != null)
            Destroy(arrow);
    }

    private void ApplyDamage(GameObject target, float damage)
    {
        Knight knight = target.GetComponent<Knight>();
        if (knight != null)
        {
            knight.TakeDamage(damage, gameObject);
            return;
        }

        Archer archer = target.GetComponent<Archer>();
        if (archer != null)
        {
            archer.TakeDamage(damage, gameObject);
        }
    }

    public void TakeDamage(float damage, GameObject attacker)
    {
        if (isDead) return;

        currentHealth -= damage;

        if (currentHealth <= 0)
            Die();
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        grid.RemoveUnit(gameObject);
        Destroy(gameObject);
    }

    private void FaceTarget(Vector3 targetPos)
    {
        Vector3 dir = (targetPos - transform.position);
        dir.y = 0;

        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}