using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ActionButton : MonoBehaviour, IPointerClickHandler, IClickable, IPointerEnterHandler, IPointerExitHandler
{
    /// <summary>
    /// A reference t o the useable on the actionbutton
    /// </summary>
    public IUseable MyUseable { get; set; }

    [SerializeField]
    private Text stackSize;

    private float cooldownTime;

    private Stack<IUseable> useables = new Stack<IUseable>();

    private int count;

    [SerializeField]
    private Image cooldownOverlay;

    [SerializeField]
    private TMP_Text countDown;

    /// <summary>
    /// A reference to the actual button that this button uses
    /// </summary>
    public Button MyButton { get; private set; }

    public Image MyIcon
    {
        get
        {
            return icon;
        }

        set
        {
            icon = value;
        }
    }

    public int MyCount
    {
        get
        {
            return count;
        }
    }


    public Text MyStackText
    {
        get
        {
            return stackSize;
        }
    }

    public Stack<IUseable> MyUseables
    {
        get
        {
            return useables;
        }

        set
        {
            if (value.Count > 0)
            {
                MyUseable = value.Peek();
            }
            else
            {
                MyUseable = null;
            }
            
            useables = value;
        }
    }

    [SerializeField]
    private Image icon;

    // Use this for initialization
    void Start()
    {
        MyButton = GetComponent<Button>();
        MyButton.onClick.AddListener(OnClick);
        InventoryScript.MyInstance.itemCountChangedEvent += new ItemCountChanged(UpdateItemCount);

    }

    // Update is called once per frame
    void Update()
    {

    }

    /// <summary>
    /// This is executed the the button is clicked
    /// </summary>
    public void OnClick()
    {
        if (HandScript.MyInstance.MyMoveable == null)
        {
            if (MyUseable != null)
            {
                MyUseable.Use();
            }
            else if (MyUseables != null && MyUseables.Count > 0)
            {
                MyUseables.Peek().Use();
            }
        }

    }

    /// <summary>
    /// Checks if someone clicked on the actionbutton
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
        {
            if (HandScript.MyInstance.MyMoveable != null && HandScript.MyInstance.MyMoveable is IUseable)
            {
                SetUseable(HandScript.MyInstance.MyMoveable as IUseable);
            }
        }
    }

    /// <summary>
    /// Sets the useable on an actionbutton
    /// </summary>
    public void SetUseable(IUseable useable)
    {
        if (useable is Item)
        {
            MyUseables = InventoryScript.MyInstance.GetUseables(useable);
            if (InventoryScript.MyInstance.FromSlot != null)
            {
                InventoryScript.MyInstance.FromSlot.MyCover.enabled = false;
                InventoryScript.MyInstance.FromSlot.MyIcon.enabled = true;
                InventoryScript.MyInstance.FromSlot = null;
            }
 

        }
        else
        {
            MyUseables.Clear();
            this.MyUseable = useable;

            if (useable is ICastable)
            {
                cooldownTime = (useable as ICastable).MyCooldown;
                (useable as ICastable).CastDone += new Done(RunCooldown);
            }
        }

        count = MyUseables.Count;
        UpdateVisual(useable as IMoveable);
        UIManager.MyInstance.RefreshTooltip(MyUseable as IDescribable);
    }

    public void RunCooldown()
    {
        StartCoroutine(Progress());
    }

    private IEnumerator Progress()
    {
        cooldownOverlay.fillAmount = 1;
        countDown.enabled = true;
        cooldownOverlay.enabled = true;

        //How much time has passed since we started casting the spell
        float timePassed = Time.deltaTime;

        //How fast are we going to move the bar
        float rate = 1.0f / cooldownTime;

        //The current progress of the cast 
        float progress = 0.0f;

        while (progress <= 1.0)//As long as we are not done casting
        {
            //Updates the bar based on the progress
            cooldownOverlay.fillAmount = Mathf.Lerp(1, 0, progress);

            //Increases the progress
            progress += rate * Time.deltaTime;

            //Updates the time passed
            timePassed += Time.deltaTime;

            //Updates the cast time text
            countDown.text = (cooldownTime - timePassed).ToString("F2");

            if (cooldownTime - timePassed < 0) //Makes sure that the time doesn't go below 0
            {
                countDown.text = "0.00";

                if (cooldownTime -timePassed < 0)
                {
                    countDown.text = "0.00";
                    cooldownOverlay.enabled = false;
                    countDown.enabled = false;
                }
            }

            yield return null;
        }
    }

    /// <summary>
    /// Updates the visual representation of the actionbutton
    /// </summary>
    public void UpdateVisual(IMoveable moveable)
    {
        if (HandScript.MyInstance.MyMoveable != null)
        {
            HandScript.MyInstance.Drop();
        }

        MyIcon.sprite = moveable.MyIcon;
        MyIcon.enabled = true;

        if (count > 1)
        {
            UIManager.MyInstance.UpdateStackSize(this);
        }
        else if (MyUseable is Spell)
        {
            UIManager.MyInstance.ClearStackCount(this);
        }
    }

    public void UpdateItemCount(Item item)
    {
        if (item is IUseable && MyUseables.Count > 0)
        {
            if (MyUseables.Peek().GetType() == item.GetType())
            {
                MyUseables = InventoryScript.MyInstance.GetUseables(item as IUseable);

                count = MyUseables.Count;

                UIManager.MyInstance.UpdateStackSize(this);
            }
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        IDescribable tmp = null;

        if (MyUseable != null && MyUseable is IDescribable)
        {
            tmp = (IDescribable)MyUseable;
            //UIManager.MyInstance.ShowToolitip(transform.position);
        }
        else if (MyUseables.Count > 0)
        {
            // UIManager.MyInstance.ShowToolitip(transform.position);
        }
        if (tmp != null)
        {
            UIManager.MyInstance.ShowTooltip(new Vector2(1, 0), transform.position, tmp);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        UIManager.MyInstance.HideTooltip();
    }
}
