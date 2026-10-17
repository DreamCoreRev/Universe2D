using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LootWindow : MonoBehaviour
{
    private static LootWindow instance;

    public static LootWindow MyInstance
    {
        get
        {
            if (instance == null)
            {
                instance = GameObject.FindObjectOfType<LootWindow>();
            }
            return instance;
        }
    }


    [SerializeField]
    private LootButton[] lootButtons;

    private CanvasGroup canvasGroup;

    private List<List<Drop>> pages = new List<List<Drop>>();

    private List<Drop> droppedLoot = new List<Drop>();

    private int pageIndex = 0;

    // Index (dans pages[pageIndex], donc parmi les objets REELLEMENT
    // affiches sur la page courante) du bouton actuellement mis en
    // surbrillance par la manette -- voir UpdateGamepadNavigation() et
    // HighlightLootButton() plus bas. Meme principe de surbrillance
    // manuelle (couleur + agrandissement) que UIManager pour le Main
    // Menu : le skin "Selected" natif d'Unity est trop discret.
    private int selectedLootIndex;

    [SerializeField]
    private Color highlightColor = new Color(1f, 0.82f, 0.15f);

    [SerializeField]
    private float highlightScale = 1.15f;

    [SerializeField]
    private Text pageNumber;

    [SerializeField]
    private GameObject nextBtn, previousBtn;

    public IInteractable MyInteractable { get; set; }

    /// <summary>
    /// NOTE FOR DEBUGGING ONLY
    /// </summary>
    [SerializeField]
    private Item[] items;

    public bool IsOpen
    {
        get { return canvasGroup.alpha > 0; }
    }

    private void Awake()
    {
        canvasGroup = GetComponent<CanvasGroup>();
        canvasGroup.alpha = 0;
    }

    private void Update()
    {
        UpdateGamepadNavigation();
    }

    public void CreatePages(List<Drop> items)
    {
        if (!IsOpen)
        {
            List<Drop> page = new List<Drop>();

            droppedLoot = items;

            selectedLootIndex = 0;

            for (int i = 0; i < items.Count; i++)
            {
                page.Add(items[i]);

                if (page.Count == 4 || i == items.Count - 1)
                {
                    pages.Add(page);
                    page = new List<Drop>();
                }
            }

            AddLoot();

            Open();
        }


    }

    private void AddLoot()
    {
        if (pages.Count > 0)
        {
            //Handle page numbers
            pageNumber.text = pageIndex + 1 + "/" + pages.Count;

            //Handle next and prev buttons
            previousBtn.SetActive(pageIndex > 0);
            nextBtn.SetActive(pages.Count > 1 && pageIndex < pages.Count - 1);

            for (int i = 0; i < pages[pageIndex].Count; i++)
            {
                if (pages[pageIndex][i] != null)
                {
                    //Set the loot buttons icon
                    lootButtons[i].MyIcon.sprite = pages[pageIndex][i].MyItem.MyIcon;

                    lootButtons[i].MyLoot = pages[pageIndex][i].MyItem;

                    //Make sure the loot buttons is visible
                    lootButtons[i].gameObject.SetActive(true);

                    string title = string.Format("<color={0}>{1}</color>", QualityColor.MyColors[pages[pageIndex][i].MyItem.MyQuality], pages[pageIndex][i].MyItem.MyTitle);

                    //Set the title
                    lootButtons[i].MyTitle.text = title;
                }

            }
        }



    }

    public void ClearButtons()
    {
        foreach (LootButton btn in lootButtons)
        {
            btn.gameObject.SetActive(false);
        }
    }

    public void NextPage()
    {
        //we check if we have more pages
        if (pageIndex < pages.Count - 1)
        {
            pageIndex++;
            selectedLootIndex = 0;
            ClearButtons();
            AddLoot();
        }
    }

    public void PreviousPage()
    {
        //We are checking if we have more pages in the backwards direction
        if (pageIndex > 0)
        {
            pageIndex--;
            selectedLootIndex = 0;
            ClearButtons();
            AddLoot();
        }
    }

    public void TakeLoot(Item loot)
    {

        Drop drop = pages[pageIndex].Find(x => x.MyItem == loot);

        pages[pageIndex].Remove(drop);

        drop.Remove();

        if (pages[pageIndex].Count == 0)
        {
            //Removes the empty page
            pages.Remove(pages[pageIndex]);

            if (pageIndex == pages.Count && pageIndex > 0)
            {
                pageIndex--;
            }

            AddLoot();
        }
    }

    /// <summary>
    /// Navigation manette dans la fenetre de loot (meme principe que
    /// UIManager.UpdateMainMenuGamepadNavigation() pour le Main Menu) :
    /// stick gauche haut/bas pour deplacer la selection parmi les objets
    /// de la page courante, A pour prendre l'objet selectionne, B pour
    /// fermer la fenetre sans prendre le reste. Remplace l'ancien
    /// "ramasser tout" (TakeAll) qui ne faisait que retirer les objets de
    /// la table de butin (Drop.Remove()) SANS jamais les ajouter aux sacs
    /// -- contrairement au clic souris (voir LootButton.OnPointerClick),
    /// qui appelle InventoryScript.AddItem() avant de retirer le Drop.
    /// TakeSelectedLoot() ci-dessous fait maintenant la meme chose.
    /// </summary>
    private void UpdateGamepadNavigation()
    {
        if (!IsOpen || pages.Count == 0)
        {
            return;
        }

        int activeCount = pages[pageIndex].Count;
        if (activeCount == 0)
        {
            return;
        }

        if (selectedLootIndex >= activeCount)
        {
            selectedLootIndex = 0;
        }

        if (GamepadInput.NavigateDownPressed)
        {
            selectedLootIndex = (selectedLootIndex + 1) % activeCount;
        }
        if (GamepadInput.NavigateUpPressed)
        {
            selectedLootIndex = (selectedLootIndex - 1 + activeCount) % activeCount;
        }

        HighlightLootButton(selectedLootIndex, activeCount);

        if (GamepadInput.MenuConfirmPressed)
        {
            TakeSelectedLoot();
        }
        if (GamepadInput.MenuCancelPressed)
        {
            Close();
        }
    }

    /// <summary>
    /// Met en surbrillance (couleur + agrandissement, voir
    /// highlightColor/highlightScale) le bouton de loot a l'index
    /// selectedIndex parmi les activeCount premiers boutons (ceux
    /// reellement visibles sur la page courante -- voir AddLoot()), et
    /// remet tous les autres a leur apparence normale.
    /// </summary>
    private void HighlightLootButton(int selectedIndex, int activeCount)
    {
        for (int i = 0; i < lootButtons.Length; i++)
        {
            bool isSelected = i == selectedIndex && i < activeCount;
            Image background = lootButtons[i].GetComponent<Image>();
            if (background != null)
            {
                background.color = isSelected ? highlightColor : Color.white;
            }
            lootButtons[i].transform.localScale = isSelected ? Vector3.one * highlightScale : Vector3.one;
        }
    }

    /// <summary>
    /// Prend l'objet actuellement selectionne a la manette (voir
    /// UpdateGamepadNavigation() ci-dessus) : ajoute d'abord l'objet aux
    /// sacs (InventoryScript.AddItem(), comme LootButton.OnPointerClick a
    /// la souris), et ne le retire de la fenetre (TakeLoot()) que si
    /// l'ajout a reussi -- si les sacs sont pleins, l'objet reste dans la
    /// fenetre, meme comportement qu'a la souris.
    /// </summary>
    private void TakeSelectedLoot()
    {
        if (pages.Count == 0 || selectedLootIndex >= pages[pageIndex].Count)
        {
            return;
        }

        Item loot = pages[pageIndex][selectedLootIndex].MyItem;

        if (!InventoryScript.MyInstance.AddItem(loot))
        {
            return;
        }

        TakeLoot(loot);

        if (pages.Count == 0)
        {
            Close();
        }
        else if (selectedLootIndex >= pages[pageIndex].Count)
        {
            selectedLootIndex = pages[pageIndex].Count - 1;
        }
    }

    public void Close()
    {
        pageIndex = 0;
        selectedLootIndex = 0;
        pages.Clear();
        canvasGroup.alpha = 0;
        canvasGroup.blocksRaycasts = false;
        ClearButtons();
        HighlightLootButton(-1, 0);

        if (MyInteractable != null)
        {
            MyInteractable.StopInteract();
        }

        MyInteractable = null;
    }

    public void Open()
    {
        canvasGroup.alpha = 1;
        canvasGroup.blocksRaycasts = true;
    }
}
