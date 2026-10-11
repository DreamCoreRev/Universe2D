using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Talent de rang 3 (1 point) de la colonne "Laceration" -- debloque le
/// sort "Execution", meme principe que ChainLightningTalents pour le Mage.
/// </summary>
public class ExecutionTalent : Talent
{
    public override bool Click()
    {
        if (base.Click())
        {
            SpellBook.MyInstance.LearnSpell("Execution");
        }

        return false;
    }

    public override string GetDescription()
    {
        return "Execution<color=#ffd100>\nFrappe une cible au corps a corps\navec une lame tranchante</color>";
    }
}
