using Assets.Scripts.Debuffs;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class UIManager : MonoBehaviour
{
    private static UIManager instance;

    public static UIManager MyInstance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<UIManager>();
            }

            return instance;
        }
    }

    /// <summary>
    /// A reference to all the action buttons
    /// </summary>
    [SerializeField]
    private ActionButton[] actionButtons;

    [SerializeField]
    private CanvasGroup[] menus;


    [SerializeField]
    private GameObject targetFrame;

    private Stat healthStat;

    [SerializeField]
    private Text levelText;

    [SerializeField]
    private Image portraitFrame;

    [SerializeField]
    private GameObject tooltip;

    [SerializeField]
    private CharacterPanel charPanel;

    private Text tooltipText;

    [SerializeField]
    private RectTransform tooltipRect;

    [SerializeField]
    private TargetDebuff targetDebuffPrefab;

    [SerializeField]
    private Transform targetDebuffsTransform;

    private List<TargetDebuff> targetDebuffs = new List<TargetDebuff>();

    [SerializeField]
    private TMP_Text txtStrength, txtStamina, txtIntellect;

    /// <summary>
    /// A reference to all the kibind buttons on the menu
    /// </summary>
    private GameObject[] keybindButtons;

    private void Awake()
    {
        keybindButtons = GameObject.FindGameObjectsWithTag("Keybind");
        tooltipText = tooltip.GetComponentInChildren<Text>();
    }

    // Use this for initialization
    void Start()
    {
        healthStat = targetFrame.GetComponentInChildren<Stat>();
    }

    // Update is called once per frame
    void Update()
    {
        if (ChatManager.MyInstance != null && ChatManager.MyInstance.IsTyping)
        {
            // On tape dans le chat (voir ChatManager.IsTyping) : les
            // raccourcis clavier de menus (I/B/C/L/P/N, Echap...) ne
            // doivent pas reagir, sinon ecrire un message qui contient ces
            // lettres ouvre/ferme des fenetres en meme temps (voir aussi
            // le meme garde-fou dans Player.GetInput()).
            return;
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            OpenClose(menus[0]);
        }
        if (Input.GetKeyDown(KeyCode.I))
        {
            OpenClose(menus[1]);
        }
        if (Input.GetKeyDown(KeyCode.B))
        {
            InventoryScript.MyInstance.OpenClose();
        }
        if (Input.GetKeyDown(KeyCode.C))
        {
            OpenClose(menus[2]);
        }
        if (Input.GetKeyDown(KeyCode.L))
        {
            OpenClose(menus[3]);
        }
        if (Input.GetKeyDown(KeyCode.P))
        {
            OpenClose(menus[6]);
        }
        if (Input.GetKeyDown(KeyCode.N))
        {
            OpenClose(menus[7]);
        }


        //if (Input.GetKeyDown(KeyCode.Escape))
        //{
        //    OpenClose(keybindMenu);
        //}
        //if (Input.GetKeyDown(KeyCode.I))
        //{
        //    OpenClose(spellBook);
        //}

        //if (Input.GetKeyDown(KeyCode.C))
        //{
        //    charPanel.OpenClose();
        //}
    }

    public void ShowTargetFrame(Enemy target)
    {
        targetFrame.SetActive(true);

        healthStat.Initialize(target.MyHealth.MyCurrentValue, target.MyHealth.MyMaxValue);

        portraitFrame.sprite = target.MyPortrait;

        levelText.text = target.MyLevel.ToString();

        target.healthChanged += new HealthChanged(UpdateTargetFrame);

        target.characterRemoved += new CharacterRemoved(HideTargetFrame);

        if (target.MyLevel >= Player.MyInstance.MyLevel + 5)
        {
            levelText.color = Color.red;
        }
        else if (target.MyLevel == Player.MyInstance.MyLevel + 3 || target.MyLevel == Player.MyInstance.MyLevel + 4)
        {
            levelText.color = new Color32(255, 124, 0, 255);
        }
        else if (target.MyLevel >= Player.MyInstance.MyLevel -2 && target.MyLevel <= Player.MyInstance.MyLevel+2)
        {
            levelText.color = Color.yellow;
        }
        else if (target.MyLevel <= Player.MyInstance.MyLevel-3 && target.MyLevel > XPManager.CalculateGrayLevel())
        {
            levelText.color = Color.green;
        }
        else
        {
            levelText.color = Color.grey;
        }

        for (int i = 0; i < targetDebuffs.Count; i++)
        {
            Destroy(targetDebuffs[i].gameObject);
        }

        targetDebuffs.Clear();

        foreach (Debuff debuff in target.MyDebuffs)
        {
            TargetDebuff td = Instantiate(targetDebuffPrefab, targetDebuffsTransform);
            td.Initialize(debuff);
            targetDebuffs.Add(td);
        }
    }

    public void HideTargetFrame()
    {
        targetFrame.SetActive(false);
    }

    /// <summary>
    /// Updates the targetframe
    /// </summary>
    /// <param name="health"></param>
    public void UpdateTargetFrame(float health)
    {
        healthStat.MyCurrentValue = health;
    }

    public void AddDebuffToTargetFrame(Debuff debuff)
    {
        if (targetFrame.activeSelf && debuff.MyCharacter == Player.MyInstance.MyTarget)
        {
            TargetDebuff td = Instantiate(targetDebuffPrefab, targetDebuffsTransform);
            td.Initialize(debuff);
            targetDebuffs.Add(td);
        }
    }

    public void RemoveDebuff(Debuff debuff)
    {
        if (targetFrame.activeSelf && debuff.MyCharacter == Player.MyInstance.MyTarget)
        {
            TargetDebuff td = targetDebuffs.Find(x => x.Debuff.Name == debuff.Name);

            targetDebuffs.Remove(td);
            Destroy(td.gameObject);
        }
    }

    /// <summary>
    /// Updates the text on a keybindbutton after the key has been changed
    /// </summary>
    /// <param name="key"></param>
    /// <param name="code"></param>
    public void UpdateKeyText(string key, KeyCode code)
    {
        Text tmp = Array.Find(keybindButtons, x => x.name == key).GetComponentInChildren<Text>();
        tmp.text = code.ToString();
    }

    public void ClickActionButton(string buttonName)
    {
        Array.Find(actionButtons, x => x.gameObject.name == buttonName).MyButton.onClick.Invoke();
    }

    public void OpenClose(CanvasGroup canvasGroup)
    {
        canvasGroup.alpha = canvasGroup.alpha > 0 ? 0 : 1;
        canvasGroup.blocksRaycasts = canvasGroup.blocksRaycasts == true ? false : true;
    }

    // Permet d'ouvrir/fermer un panneau de "menus" depuis un bouton (voir
    // TouchMenuButtons.cs) sans lui donner un accès direct au tableau privé
    // menus[]. Les index sont les mêmes que ceux utilisés juste au-dessus
    // dans Update() pour les raccourcis clavier : 0=MainMenu (Echap),
    // 1=SpellBook (Sorts), 2=CharacterPanel (Personnage), 3=Questlog
    // (Quêtes), 6=Profession (Métiers).
    public void ToggleMenu(int index)
    {
        OpenClose(menus[index]);
    }

    public void OpenSingle(CanvasGroup canvasGroup)
    {
        foreach (CanvasGroup canvas in menus)
        {
            CloseSingle(canvas);
        }

        canvasGroup.alpha = canvasGroup.alpha > 0 ? 0 : 1;
        canvasGroup.blocksRaycasts = canvasGroup.blocksRaycasts == true ? false : true;
    }

    public void CloseSingle(CanvasGroup canvasGroup)
    {
        canvasGroup.alpha  = 0;
        canvasGroup.blocksRaycasts = false;

    }

    // Appele par le bouton "Deconnexion" du menu Echap (voir Demo.unity,
    // panneau MainMenu). Avant (solo), rechargeait juste Login -- depuis
    // Mirror, il faut explicitement arreter le client reseau d'abord (sinon
    // la connexion reste active en arriere-plan : StartClient() ne fait
    // plus rien tant qu'on n'a pas vraiment quitte et relance le jeu), puis
    // revenir a l'ecran de SELECTION de personnage (pas au login -- le
    // compte reste connecte, voir CharacterSelectManager pour le vrai
    // logout de compte, qui lui revient a Login).
    public void Logout()
    {
        // Sauvegarde la progression du personnage avant de partir -- sans
        // ca, seule la sauvegarde automatique periodique (voir
        // SaveManager.AutoSaveLoop) ou la fermeture complete du jeu
        // (OnApplicationQuit) l'aurait fait, ce qui pouvait perdre jusqu'a
        // plusieurs minutes de jeu sur une simple deconnexion volontaire.
        if (Session.HasSelectedCharacter)
        {
            SaveManager saveManager = FindObjectOfType<SaveManager>();
            if (saveManager != null)
            {
                saveManager.SaveCharacter(Session.SelectedCharacterId);
            }
        }

        if (Mirror.NetworkManager.singleton != null && Mirror.NetworkClient.active)
        {
            Mirror.NetworkManager.singleton.StopClient();
        }

        SceneManager.LoadScene("CharacterSelect");
    }

    /// <summary>
    /// Updates the stacksize on a clickable slot
    /// </summary>
    /// <param name="clickable"></param>
    public void UpdateStackSize(IClickable clickable)
    {
        if (clickable.MyCount > 1) //If our slot has more than one item on it
        {
            clickable.MyStackText.text = clickable.MyCount.ToString();
            clickable.MyStackText.enabled = true;
            clickable.MyIcon.enabled = true;
        }
        else //If it only has 1 item on it
        {
            clickable.MyStackText.enabled = false;
            clickable.MyIcon.enabled = true;
        }
        if (clickable.MyCount == 0) //If the slot is empty, then we need to hide the icon
        {
            clickable.MyStackText.enabled = false;
            clickable.MyIcon.enabled = false;
        }
    }

    public void ClearStackCount(IClickable clickable)
    {
        clickable.MyStackText.enabled = false;
        clickable.MyIcon.enabled = true;
    }

    /// <summary>
    /// Shows the tooltip
    /// </summary>
    public void ShowTooltip(Vector2 pivot, Vector3 position, IDescribable description)
    {
        tooltipRect.pivot = pivot;
        tooltip.SetActive(true);
        tooltip.transform.position = position;
        tooltipText.text = description.GetDescription();
    }

    /// <summary>
    /// Hides the tooltip
    /// </summary>
    public void HideTooltip()
    {
        tooltip.SetActive(false);
    }

    public void RefreshTooltip(IDescribable description)
    {
        tooltipText.text = description.GetDescription();
    }

    public void UpdateStatsText(int intellect, int stamina, int strength)
    {
        this.txtStrength.text = "STRENGTH:<color=\"green\"> " + strength;
        this.txtStamina.text = "STAMINA:<color=\"green\"> " + stamina;
        this.txtIntellect.text = "INTELLECT:<color=\"green\"> " + intellect;

    }
}
