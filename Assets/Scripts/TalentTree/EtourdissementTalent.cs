using Assets.Scripts.Debuffs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Talent de rang 2 de la colonne "Coup de Bouclier" -- donne au sort le
/// debuff "Etourdissement" (voir ShieldBashDebuff), meme principe que
/// PermafrostTalent pour le Mage. Remplace l'ancienne assignation
/// inconditionnelle faite dans Player.cs.
/// </summary>
public class EtourdissementTalent : Talent
{
    ShieldBashDebuff debuff;

    private float speedReduction = 20;

    private float reductionIncrease = 10;

    private string nextRank = string.Empty;

    public void Start()
    {
        debuff = new ShieldBashDebuff(icon);
        debuff.MySpeedReduction = speedReduction;
    }

    public override bool Click()
    {
        if (base.Click())
        {
            debuff.MySpeedReduction = speedReduction;

            if (MyCurrentCount < 3)
            {
                speedReduction += reductionIncrease;
                nextRank = $"<color=#ffffff>\n\nNext rank:\n</color><color=#ffd100>Votre Coup de Bouclier ralentit\nla cible de {debuff.MySpeedReduction + reductionIncrease}%</color>\n";
            }
            else
            {
                nextRank = string.Empty;
            }

            SpellBook.MyInstance.GetSpell("Coup de Bouclier").MyDebuff = debuff;
            UIManager.MyInstance.RefreshTooltip(this);
            return true;
        }

        return false;
    }

    public override string GetDescription()
    {
        return $"Etourdissement<color=#ffd100>\nVotre Coup de Bouclier ralentit\nla cible de {debuff.MySpeedReduction}%</color>{nextRank}";
    }
}
