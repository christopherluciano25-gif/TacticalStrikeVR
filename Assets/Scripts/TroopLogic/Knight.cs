using UnityEngine;
using System.Collections;

public class Knight : MonoBehaviour
{
    [Header("Stats")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float moveSpeed = 3f;
    [SerializeField] private float attackDamage = 15f;
    [SerializeField] private float attackRange = 1.2f;
    [SerializeField] private float attackCooldown = 1f;
    [SerializeField] private float baseDamage = 20f;

    [Header("Team")]
    public TeamSide team = TeamSide.Player;

    [Header("References")]
    [SerializeField] private TacticalGrid grid;

    private float currentHealth;
    private bool isDead = false;
    private float lastAttackTime;

    private Vector2Int currentGridPos;
    private Vector2Int targetGridPos;

    private Transform currentTarget;

    private void Start()
    {
        currentHealth = maxHealth;

        if (grid == null)
            grid = FindFirstObjectByType<TacticalGrid>();

        currentGridPos = grid.WorldToGrid(transform.position);
        targetGridPos = currentGridPos;

        StartCoroutine(AILoop());
    }

    private IEnumerator AILoop()
    {
        while (!isDead)
        {
            FindBestTarget();
            HandleCombatOrMovement();
            yield return new WaitForSeconds(0.1f);
        }
    }

    private void Update()
    {
        if (isDead) return;

        MoveSmoothlyToTargetCell();
    }

    // =========================================================
    // TARGETING
    // =========================================================

    private void FindBestTarget()
    {
        float closestDist = Mathf.Infinity;
        Transform best = null;

        Knight[] knights = FindObjectsByType<Knight>(FindObjectsSortMode.None);

        foreach (var k in knights)
        {
            if (k == this || k.team == team) continue;

            float dist = Vector3.Distance(transform.position, k.transform.position);
            if (dist < closestDist)
            {
                closestDist = dist;
                best = k.transform;
            }
        }

        currentTarget = best;
    }

    // =========================================================
    // AI DECISION
    // =========================================================

    private void HandleCombatOrMovement()
    {
        if (currentTarget != null)
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);

            if (dist <= attackRange)
            {
                TryAttack();
                return;
            }
        }

        DecideNextGridCell();
    }

    private void DecideNextGridCell()
    {
        Vector2Int next;

        if (currentTarget != null)
        {
            Vector2Int enemyGrid = grid.WorldToGrid(currentTarget.position);

            Vector2Int dir = new Vector2Int(
                Mathf.Clamp(enemyGrid.x - currentGridPos.x, -1, 1),
                Mathf.Clamp(enemyGrid.y - currentGridPos.y, -1, 1)
            );

            next = currentGridPos + dir;
        }
        else
        {
            // Move toward enemy base row
            int direction = team == TeamSide.Player ? 1 : -1;
            next = new Vector2Int(currentGridPos.x, currentGridPos.y + direction);
        }

        if (grid.IsValidGridPosition(next))
        {
            targetGridPos = next;
        }
        else
        {
            DamageEnemyBase();
            Die();
        }
    }

    // =========================================================
    // MOVEMENT
    // =========================================================

    private void MoveSmoothlyToTargetCell()
    {
        Vector3 worldTarget = grid.GridToWorld(targetGridPos.x, targetGridPos.y);

        transform.position = Vector3.MoveTowards(
            transform.position,
            worldTarget,
            moveSpeed * Time.deltaTime
        );

        Vector3 lookDir = (worldTarget - transform.position);
        lookDir.y = 0;
        if (lookDir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(lookDir);

        if (Vector3.Distance(transform.position, worldTarget) < 0.05f)
        {
            currentGridPos = targetGridPos;
        }
    }

    // =========================================================
    // COMBAT
    // =========================================================

    private void TryAttack()
    {
        if (Time.time < lastAttackTime + attackCooldown)
            return;

        lastAttackTime = Time.time;

        Knight enemy = currentTarget.GetComponent<Knight>();
        if (enemy != null)
            enemy.TakeDamage(attackDamage, gameObject);
    }

    public void TakeDamage(float damage, GameObject attacker)
    {
        if (isDead) return;

        currentHealth -= damage;

        if (currentHealth <= 0)
            Die();
    }

    // =========================================================
    // BASE DAMAGE
    // =========================================================

    private void DamageEnemyBase()
    {
        BaseHealth[] bases = FindObjectsByType<BaseHealth>(FindObjectsSortMode.None);

        foreach (BaseHealth b in bases)
        {
            if (b.owner != team)
            {
                b.TakeDamage(baseDamage);
                Debug.Log($"{team} Knight hit enemy base for {baseDamage}");
            }
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (grid != null)
            grid.RemoveUnit(gameObject);

        Destroy(gameObject);
    }
}