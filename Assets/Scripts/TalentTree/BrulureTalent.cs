using Assets.Scripts.Debuffs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Talent de rang 2 de la colonne "Frappe Enflammee" -- donne au sort le
/// debuff "Brulure" (voir BurnDebuff), meme principe qu'IgniteTalent pour
/// le Mage (icone/degats du debuff pris sur le talent lui-meme, pas sur le
/// sort).
/// </summary>
public class BrulureTalent : Talent
{
    BurnDebuff debuff;

    private float tickDamage = 5;

    private float damageIncrease = 2;

    private string nextRank = string.Empty;

    public void Start()
    {
        debuff = new BurnDebuff(icon);
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
                nextRank = $"<color=#ffffff>\n\nNext rank:\n</color><color=#ffd100>Votre Frappe Enflammee applique\nune brulure qui infligera\n{tickDamage * debuff.MyDuration} degats sur {debuff.MyDuration} secondes</color>\n";
            }
            else
            {
                nextRank = string.Empty;
            }

            SpellBook.MyInstance.GetSpell("Frappe Enflammee").MyDebuff = debuff;
            UIManager.MyInstance.RefreshTooltip(this);
            return true;
        }

        return false;
    }

    public override string GetDescription()
    {
        return $"Brulure<color=#ffd100>\nVotre Frappe Enflammee applique\nune brulure qui infligera\n{debuff.MyTickDamage * debuff.MyDuration} degats sur {debuff.MyDuration} secondes</color>{nextRank}";
    }
}
