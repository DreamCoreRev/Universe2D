using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class HandScript : MonoBehaviour
{
    /// <summary>
    /// Singleton instance of the handscript
    /// </summary>
    private static HandScript instance;

    public static HandScript MyInstance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<HandScript>();
            }

            return instance;
        }
    }

    /// <summary>
    /// The current moveable
    /// </summary>
    public IMoveable MyMoveable { get; set; }

    /// <summary>
    /// The icon of the item, that we acre moving around atm.
    /// </summary>
    private Image icon;

    /// <summary>
    /// An offset to move the icon away from the mouse
    /// </summary>
    [SerializeField]
    private Vector3 offset;

    // Use this for initialization
    void Start ()
    {
        //Creates a reference to the image on the hand
        icon = GetComponent<Image>();	
	}
	
	// Update is called once per frame
	void Update ()
    {
        //Makes sure that the icon follows the hand
        icon.transform.position = Input.mousePosition+offset;
	}

    // LateUpdate s'exécute après l'Update() de l'EventSystem, donc
    // IsPointerOverGameObject() reflète bien l'état du frame en cours --
    // en le testant depuis Update() (comme avant), sur tactile il arrivait
    // qu'il renvoie encore "pas sur une UI" au moment même où on tape sur un
    // bouton valide (ex: la barre d'action), ce qui supprimait l'objet en
    // main au lieu de le placer. On ne teste aussi qu'au moment précis où le
    // doigt touche l'écran (GetMouseButtonDown), plutôt qu'à chaque frame
    // tant qu'il reste posé.
    void LateUpdate()
    {
        if (Input.GetMouseButtonDown(0) && !EventSystem.current.IsPointerOverGameObject() && MyInstance.MyMoveable != null)
        {
            DeleteItem();
        }
    }

    /// <summary>
    /// Take a moveable in the hand, so that we can move it around
    /// </summary>
    /// <param name="moveable">The moveable to pick up</param>
    public void TakeMoveable(IMoveable moveable)
    {
        this.MyMoveable = moveable;
        icon.sprite = moveable.MyIcon;
        icon.enabled = true;
    }

    public IMoveable Put()
    {
        IMoveable tmp = MyMoveable;
        MyMoveable = null;
        icon.enabled = false;
        return tmp;
    }

    public void Drop()
    {
        MyMoveable = null;
        icon.enabled = false;
        InventoryScript.MyInstance.FromSlot = null;
    }

    /// <summary>
    /// Deletes an item from the inventory
    /// </summary>
    public void DeleteItem()
    {
        if (MyMoveable is Item)
        {
            Item item = (Item)MyMoveable;
            if (item.MySlot != null)
            {
                item.MySlot.Clear();
            }
            else if (item.MyCharButton != null)
            {
                item.MyCharButton.DequipArmor();
            }
      
        }

        Drop();

        InventoryScript.MyInstance.FromSlot = null;
    }
}
