using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Talent de rang 3 (1 point) de la colonne "Coup de Bouclier" -- debloque
/// le sort "Bouclier Vengeur", meme principe que BlizzardTalent pour le
/// Mage.
/// </summary>
public class BouclierVengeurTalent : Talent
{
    public override bool Click()
    {
        if (base.Click())
        {
            SpellBook.MyInstance.LearnSpell("Bouclier Vengeur");
        }

        return false;
    }

    public override string GetDescription()
    {
        return "Bouclier Vengeur<color=#ffd100>\nFrappe une cible avec votre bouclier\net lui infligent de lourds degats</color>";
    }
}
