using UnityEngine;

public class TestBaseDamage : MonoBehaviour
{
    void Update()
    {
        // Press 1 to damage player base
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            DamageBase(TeamSide.Player, 100f);
        }
        
        // Press 2 to damage bot base
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            DamageBase(TeamSide.Bot, 100f);
        }
        
        // Press 3 to destroy player base (defeat)
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            DestroyBase(TeamSide.Player);
        }
        
        // Press 4 to destroy bot base (victory)
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            DestroyBase(TeamSide.Bot);
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
    
    void DestroyBase(TeamSide owner)
    {
        BaseHealth[] bases = FindObjectsByType<BaseHealth>(FindObjectsSortMode.None);
        
        foreach (BaseHealth baseHealth in bases)
        {
            if (baseHealth.owner == owner)
            {
                baseHealth.TakeDamage(9999f);
                Debug.Log($"Destroyed {owner} base!");
                break;
            }
        }
    }
}