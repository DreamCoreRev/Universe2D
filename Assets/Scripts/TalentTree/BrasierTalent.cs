using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Talent de rang 3 (1 point, deverrouille apres Brulure au rang max) de la
/// colonne "Frappe Enflammee" -- debloque le sort "Brasier", meme principe
/// que RainOfFire pour le Mage.
/// </summary>
public class BrasierTalent : Talent
{
    public override bool Click()
    {
        if (base.Click())
        {
            SpellBook.MyInstance.LearnSpell("Brasier");
        }

        return false;
    }

    public override string GetDescription()
    {
        return "Brasier<color=#ffd100>\nFrappe une cible au corps a corps\navec une violente explosion de flammes</color>";
    }
}
