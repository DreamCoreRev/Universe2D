using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public class CharacterPanel : MonoBehaviour, IPointerClickHandler {

    private static CharacterPanel instance;

    [SerializeField]
    private CanvasGroup canvasGroup;

    [SerializeField]
    private CharButton head, shoulders, chest, hands, legs, feet, main, off;

    public CharButton MySlectedButton { get; set; }

    public static CharacterPanel MyInstance
    {
        get
        {
            if (instance == null)
            {
                instance = GameObject.FindObjectOfType<CharacterPanel>();
            }

            return instance;
        }
    }

    public void OpenClose()
    {
        if (canvasGroup.alpha <= 0)
        {
            canvasGroup.blocksRaycasts = true;
            canvasGroup.alpha = 1;
        }
        else
        {
            canvasGroup.blocksRaycasts = false;
            canvasGroup.alpha = 0;
        }
    }

    public void EquipArmor(Armor armor)
    {
        switch (armor.MyArmorType)
        {
            case ArmorType.Head:
                head.EquipArmor(armor);
                break;
            case ArmorType.Shoulders:
                shoulders.EquipArmor(armor);
                break;
            case ArmorType.Chest:
                chest.EquipArmor(armor);
                break;
            case ArmorType.Hands:
                hands.EquipArmor(armor);
                break;
            case ArmorType.Legs:
                legs.EquipArmor(armor);
                break;
            case ArmorType.Feet:
                feet.EquipArmor(armor);
                break;
            case ArmorType.MainHand:
                main.EquipArmor(armor);
                break;
            case ArmorType.Offhand:
                off.EquipArmor(armor);
                break;
        }
    }

    // Tape n'importe où sur le fond du panneau Personnage (pas forcément sur
    // le bon petit emplacement d'équipement) pour équiper l'objet qu'on a en
    // main -- EquipArmor() sait déjà retrouver le bon emplacement tout seul
    // via armor.MyArmorType, donc pas besoin de viser précisément au tactile.
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left && HandScript.MyInstance.MyMoveable is Armor)
        {
            (HandScript.MyInstance.MyMoveable as Armor).Equip();
        }
    }
}
