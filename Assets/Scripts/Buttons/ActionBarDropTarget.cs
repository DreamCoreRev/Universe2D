using UnityEngine;
using UnityEngine.EventSystems;

// Permet de taper n'importe où sur la barre d'action (pas uniquement sur un
// slot précis) pour y placer le sort/objet qu'on a actuellement "en main".
// Plus pratique au tactile que de viser un petit bouton exact : on prend le
// sort dans le grimoire (SpellButton), puis on tape n'importe où sur la
// barre, et il se range dans le premier emplacement libre.
public class ActionBarDropTarget : MonoBehaviour, IPointerClickHandler
{
    [SerializeField]
    private ActionButton[] actionButtons;

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left)
        {
            return;
        }

        if (HandScript.MyInstance.MyMoveable == null || !(HandScript.MyInstance.MyMoveable is IUseable))
        {
            return;
        }

        // Cherche le premier emplacement libre de la barre d'action
        foreach (ActionButton button in actionButtons)
        {
            if (button.MyUseable == null)
            {
                button.SetUseable(HandScript.MyInstance.MyMoveable as IUseable);
                return;
            }
        }
    }
}
