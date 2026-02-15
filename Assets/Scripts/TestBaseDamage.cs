using UnityEngine;

public class TestBaseDamage : MonoBehaviour
{
    void Update()
    {
        // Press 1 to damage player base (test defeat)
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            DamageBase(TeamSide.Player, 100f);
        }
        
        // Press 2 to damage bot base (test victory)
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            DamageBase(TeamSide.Bot, 100f);
        }
        
        // Press 3 to destroy player base instantly (game over)
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            DamageBase(TeamSide.Player, 9999f);
        }
        
        // Press 4 to destroy bot base instantly (victory)
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            DamageBase(TeamSide.Bot, 9999f);
        }
    }
    
    void DamageBase(TeamSide owner, float damage)
    {
        BaseHealth[] bases = FindObjectsByType<BaseHealth>(FindObjectsSortMode.None);
        
        foreach (BaseHealth baseHealth in bases)
        {
            if (baseHealth.owner == owner)
            {
                baseHealth.TakeDamage(damage);
                Debug.Log($"Damaged {owner} base! HP: {baseHealth.CurrentHealth}/{baseHealth.MaxHealth}");
                break;
            }
        }
    }
}