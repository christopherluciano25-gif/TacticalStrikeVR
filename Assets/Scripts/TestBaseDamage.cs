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
```

6. **Save** and close code editor
7. Return to Unity (wait for compile)

8. **Hierarchy** → Right-click → **Create Empty**
9. Rename to: `TestManager`
10. **Add Component** → Search for `TestBaseDamage`
11. Click to add it

✅ **Test Script Ready!**

---

## **STEP 14: Test in Unity Editor**

1. Make sure you have **PlayerBase** and **BotBase** in your scene with **BaseHealth** scripts
   - If not, create them now (see Step 6 from earlier)

2. **Press Play** button (top-center)

3. **Test Victory:**
   - Press keyboard **4** key
   - Bot base should be destroyed
   - After 2 seconds → **Green VICTORY screen appears!** ✅

4. **Press Play** to stop

5. **Test Defeat:**
   - Press **Play** again
   - Press keyboard **3** key
   - Player base destroyed
   - After 2 seconds → **Red DEFEAT screen appears!** ✅

6. **Test Restart Button:**
   - When screen appears, try clicking the RESTART button
   - Scene should reload

**Check Console for messages:**
```
[BaseHealth] Bot base took 9999 damage! HP: 0/500
[BaseHealth] Bot BASE DESTROYED! Game Over!
[GameEndManager] Base destroyed: Bot
[GameEndManager] PLAYER VICTORY!