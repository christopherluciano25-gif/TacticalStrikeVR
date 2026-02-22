using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Centralized unit tracking - prevents expensive FindObjectsByType calls
/// All units register/unregister themselves here
/// </summary>
public class UnitManager : MonoBehaviour
{
    public static UnitManager Instance { get; private set; }

    // Unit registries
    public List<Knight> allKnights = new List<Knight>();
    public List<Archer> allArchers = new List<Archer>();
    public List<BaseHealth> allBases = new List<BaseHealth>();
    public List<WallHealth> allWalls = new List<WallHealth>();

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    // Registration methods
    public void RegisterKnight(Knight knight)
    {
        if (!allKnights.Contains(knight))
            allKnights.Add(knight);
    }

    public void UnregisterKnight(Knight knight)
    {
        allKnights.Remove(knight);
    }

    public void RegisterArcher(Archer archer)
    {
        if (!allArchers.Contains(archer))
            allArchers.Add(archer);
    }

    public void UnregisterArcher(Archer archer)
    {
        allArchers.Remove(archer);
    }

    public void RegisterBase(BaseHealth baseHealth)
    {
        if (!allBases.Contains(baseHealth))
            allBases.Add(baseHealth);
    }

    public void UnregisterBase(BaseHealth baseHealth)
    {
        allBases.Remove(baseHealth);
    }

    public void RegisterWall(WallHealth wall)
    {
        if (!allWalls.Contains(wall))
            allWalls.Add(wall);
    }

    public void UnregisterWall(WallHealth wall)
    {
        allWalls.Remove(wall);
    }

    /// <summary>
    /// Get all potential targets for a specific team (enemies only)
    /// </summary>
    public List<GameObject> GetEnemiesForTeam(TeamSide team)
    {
        List<GameObject> enemies = new List<GameObject>();

        // Add enemy knights
        foreach (Knight knight in allKnights)
        {
            if (knight != null && knight.team != team && knight.team != TeamSide.Neutral)
            {
                enemies.Add(knight.gameObject);
            }
        }

        // Add enemy archers
        foreach (Archer archer in allArchers)
        {
            if (archer != null && archer.team != team && archer.team != TeamSide.Neutral)
            {
                enemies.Add(archer.gameObject);
            }
        }

        // Add enemy bases
        foreach (BaseHealth baseHealth in allBases)
        {
            if (baseHealth != null && baseHealth.owner != team && baseHealth.owner != TeamSide.Neutral)
            {
                enemies.Add(baseHealth.gameObject);
            }
        }

        // Add enemy walls
        foreach (WallHealth wall in allWalls)
        {
            if (wall != null && wall.Owner != team && wall.Owner != TeamSide.Neutral)
            {
                enemies.Add(wall.gameObject);
            }
        }

        return enemies;
    }
}