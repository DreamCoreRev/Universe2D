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

        // gearSocket (ci-dessus) est cable en dur dans la scene sur l'ancien
        // personnage solo -- desormais desactive (voir Player.prefab) --
        // donc n'a plus d'effet visuel reel. Le vrai socket vit sur le
        // Player effectivement actif (reseau ou solo) : on l'applique la
        // en plus, et on previent les autres joueurs via le reseau.
        int socketIndex = Player.SocketIndexForArmorType((int)armoryType);
        if (socketIndex >= 0 && MyEquippedArmor.MyAnimationClips != null)
        {
            GearSocket liveSocket = Player.MyInstance.GetGearSocket(socketIndex);
            if (liveSocket != null)
            {
                liveSocket.Equip(MyEquippedArmor.MyAnimationClips);
            }
        }
        Player.MyInstance.SyncEquippedArmor(socketIndex, MyEquippedArmor);

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

        // Meme remarque que dans EquipArmor() : gearSocket ci-dessus ne
        // pointe plus vers un personnage actif, donc on applique en plus
        // sur le vrai socket et on previent les autres joueurs.
        int socketIndex = Player.SocketIndexForArmorType((int)armoryType);
        if (socketIndex >= 0)
        {
            GearSocket liveSocket = Player.MyInstance.GetGearSocket(socketIndex);
            if (liveSocket != null)
            {
                liveSocket.Dequip();
            }
        }
        Player.MyInstance.SyncEquippedArmor(socketIndex, null);

        equippedArmor.MyCharButton = null;
        equippedArmor = null;
    }
}
