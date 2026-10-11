using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Talent de rang 1 (toujours debloque) de la colonne "Frappe Enflammee" de
/// l'arbre du Guerrier -- augmente les degats de la frappe, meme principe
/// qu'ImprovedThunderbolt pour le Mage.
/// </summary>
public class ImprovedFrappeEnflammeeTalent : Talent
{
    private int percent = 5;

    public override bool Click()
    {
        if (base.Click())
        {
            Spell frappe = SpellBook.MyInstance.GetSpell("Frappe Enflammee");
            frappe.MyDamage += (frappe.MyDamage / 100) * percent;
            return true;
        }

        return false;
    }

    public override string GetDescription()
    {
        return string.Format($"Frappe renforcee<color=#ffd100>\nAugmente les degats\nde votre Frappe Enflammee\nde {percent}%. </color>");
    }
}
