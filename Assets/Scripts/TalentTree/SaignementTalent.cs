using Assets.Scripts.Debuffs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Talent de rang 2 de la colonne "Laceration" -- donne au sort le debuff
/// "Saignement" (voir BleedDebuff), meme principe qu'IgniteTalent pour le
/// Mage. Remplace l'ancienne assignation inconditionnelle faite dans
/// Player.cs.
/// </summary>
public class SaignementTalent : Talent
{
    BleedDebuff debuff;

    private float tickDamage = 8;

    private float damageIncrease = 4;

    private string nextRank = string.Empty;

    public void Start()
    {
        debuff = new BleedDebuff(icon);
        debuff.MyTickDamage = tickDamage;
    }

    public override bool Click()
    {
        if (base.Click())
        {
            debuff.MyTickDamage = tickDamage;

            if (MyCurrentCount < 3)
            {
                tickDamage += damageIncrease;
                nextRank = $"<color=#ffffff>\n\nNext rank:\n</color><color=#ffd100>Votre Laceration fait saigner\nla cible pour {tickDamage * debuff.MyDuration} degats sur {debuff.MyDuration} secondes</color>\n";
            }
            else
            {
                nextRank = string.Empty;
            }

            SpellBook.MyInstance.GetSpell("Laceration").MyDebuff = debuff;
            UIManager.MyInstance.RefreshTooltip(this);
            return true;
        }

        return false;
    }

    public override string GetDescription()
    {
        return $"Saignement<color=#ffd100>\nVotre Laceration fait saigner\nla cible pour {debuff.MyTickDamage * debuff.MyDuration} degats sur {debuff.MyDuration} secondes</color>{nextRank}";
    }
}
