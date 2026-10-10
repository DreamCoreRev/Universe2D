using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static UnityEngine.ParticleSystem;

public class RainOfFireSpell : AOESpell
{
 
    public override void Execute()
    {
        tickElapsed += Time.deltaTime;

        if (tickElapsed >= 1)
        {
            if (!VisualOnly)
            {
                Character source = Source != null ? Source : Player.MyInstance;

                for (int i = 0; i < enemies.Count; i++)
                {
                    CombatNetworking.DealDamage(enemies[i], damage / duration, source);
                }
            }

            tickElapsed = 0;
        }
    }


}
