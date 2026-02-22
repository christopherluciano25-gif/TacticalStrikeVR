using UnityEngine;

/// <summary>
/// Shared combat utility functions
/// </summary>
public static class CombatUtils
{
    /// <summary>
    /// Get the team of any GameObject
    /// </summary>
    public static TeamSide GetTeam(GameObject obj)
    {
        if (obj == null) return TeamSide.Neutral;

        Knight knight = obj.GetComponent<Knight>();
        if (knight != null) return knight.team;

        Archer archer = obj.GetComponent<Archer>();
        if (archer != null) return archer.team;

        BaseHealth baseHealth = obj.GetComponent<BaseHealth>();
        if (baseHealth != null) return baseHealth.owner;

        WallHealth wallHealth = obj.GetComponent<WallHealth>();
        if (wallHealth != null) return wallHealth.Owner;

        return TeamSide.Neutral;
    }

    /// <summary>
    /// Apply damage to any GameObject that can take damage
    /// </summary>
    public static void ApplyDamage(GameObject target, float damage, GameObject attacker)
    {
        if (target == null) return;

        Knight knight = target.GetComponent<Knight>();
        if (knight != null)
        {
            knight.TakeDamage(damage, attacker);
            return;
        }

        Archer archer = target.GetComponent<Archer>();
        if (archer != null)
        {
            archer.TakeDamage(damage, attacker);
            return;
        }

        BaseHealth baseHealth = target.GetComponent<BaseHealth>();
        if (baseHealth != null)
        {
            baseHealth.TakeDamage(damage);
            return;
        }

        WallHealth wallHealth = target.GetComponent<WallHealth>();
        if (wallHealth != null)
        {
            wallHealth.TakeDamage(damage);
            return;
        }
    }
}