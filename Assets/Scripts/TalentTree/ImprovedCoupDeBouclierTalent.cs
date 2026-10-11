using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Talent de rang 1 (toujours debloque) de la colonne "Coup de Bouclier" --
/// augmente la portee du sort, meme principe qu'ImprovedFrostbolt pour le
/// Mage.
/// </summary>
public class ImprovedCoupDeBouclierTalent : Talent
{
    public override bool Click()
    {
        if (base.Click())
        {
            SpellBook.MyInstance.GetSpell("Coup de Bouclier").MyRange += 1;
            return true;
        }

        return false;
    }

    public override string GetDescription()
    {
        return "Bouclier renforce<color=#ffd100>\nAugmente la portee\nde votre Coup de Bouclier\nde 1. </color>";
    }
}
