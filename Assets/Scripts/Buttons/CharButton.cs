using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class CharButton : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler {

    [SerializeField]
    private ArmorType armoryType;

    private Armor equippedArmor;

    [SerializeField]
    private Image icon;

    [SerializeField]
    private GearSocket gearSocket;

    [SerializeField]
    private Image visual;

    public Armor MyEquippedArmor
    {
        get
        {
            return equippedArmor;
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (HandScript.MyInstance.MyMoveable is Armor)
            {
                Armor tmp = (Armor)HandScript.MyInstance.MyMoveable;

                if (tmp.MyArmorType == armoryType)
                {
                    EquipArmor(tmp);
                }

                UIManager.MyInstance.RefreshTooltip(tmp);
            }
            else if(HandScript.MyInstance.MyMoveable == null && MyEquippedArmor != null)
            {
              
                HandScript.MyInstance.TakeMoveable(MyEquippedArmor);
                CharacterPanel.MyInstance.MySlectedButton = this;
                icon.color = Color.grey;
            }
        }
    }

    public void EquipArmor(Armor armor)
    {
        armor.Remove();

        if (visual != null)
        {
            visual.gameObject.SetActive(true);
            visual.sprite = armor.Visual;
        }

        if (MyEquippedArmor != null)
        {
            Player.MyInstance.DequipGear(MyEquippedArmor);

            if (MyEquippedArmor != armor)
            {
                armor.MySlot.AddItem(MyEquippedArmor);
            }
       
            UIManager.MyInstance.RefreshTooltip(MyEquippedArmor);
        }
        else
        {
            UIManager.MyInstance.HideTooltip();
        }

        icon.enabled = true;
        icon.sprite = armor.MyIcon;
        icon.color = Color.white;
        this.equippedArmor = armor; //A reference to the equipped armor
        this.MyEquippedArmor.MyCharButton = this;

        if (HandScript.MyInstance.MyMoveable == (armor as IMoveable))
        {
            HandScript.MyInstance.Drop();
        }

        if (gearSocket != null && MyEquippedArmor.MyAnimationClips != null)
        {
            gearSocket.Equip(MyEquippedArmor.MyAnimationClips);
        }

        Player.MyInstance.EquipGear(armor);
    
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (MyEquippedArmor != null)
        {
            UIManager.MyInstance.ShowTooltip(new Vector2(0, 0),transform.position, MyEquippedArmor);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UIManager.MyInstance.HideTooltip();
    }

    public void DequipArmor()
    {
        icon.color = Color.white;
        icon.enabled = false;

        if (visual != null)
        {
            visual.gameObject.SetActive(false);
        }

       
        if (gearSocket != null && MyEquippedArmor.MyAnimationClips != null)
        {
            Player.MyInstance.DequipGear(MyEquippedArmor);
            gearSocket.Dequip();
        }
        else if (MyEquippedArmor != null)
        {
            Player.MyInstance.DequipGear(MyEquippedArmor);
        }

        equippedArmor.MyCharButton = null;
        equippedArmor = null;
    }
}
