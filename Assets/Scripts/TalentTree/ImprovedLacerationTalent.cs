using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Talent de rang 1 (toujours debloque) de la colonne "Laceration" --
/// reduit le temps d'incantation du sort, meme principe qu'ImprovedFireball
/// pour le Mage.
/// </summary>
public class ImprovedLacerationTalent : Talent
{
    public override bool Click()
    {
        if (base.Click())
        {
            SpellBook.MyInstance.GetSpell("Laceration").MyCastTime -= 0.05f;
            return true;
        }

        return false;
    }

    public override string GetDescription()
    {
        return "Lame affutee<color=#ffd100>\nReduit le temps d'incantation\nde votre Laceration\nde 0.05 sec. </color>";
    }
}
