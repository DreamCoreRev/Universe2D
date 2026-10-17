using Assets.Scripts.Debuffs;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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

    // Boutons visibles du menu principal (menus[0]), decouverts a
    // l'ouverture via GetComponentsInChildren -- pas de wiring dans la
    // scene : ca reste a jour meme si les boutons actifs changent (voir
    // UpdateMainMenuGamepadNavigation()). mainMenuSelectedIndex est
    // l'index (dans ce tableau) actuellement mis en surbrillance par la
    // manette.
    private Button[] mainMenuButtons;
    private int mainMenuSelectedIndex;

    // Mise en surbrillance du bouton selectionne a la manette : le skin
    // "Selected" des boutons (gere par Unity via
    // EventSystem.SetSelectedGameObject) est trop discret pour qu'on le
    // distingue bien en jeu -- voir HighlightMainMenuButton() plus bas,
    // qui applique en plus une couleur et un agrandissement bien visibles,
    // reglables ici dans l'Inspecteur si besoin.
    [SerializeField]
    private Color mainMenuHighlightColor = new Color(1f, 0.82f, 0.15f);

    [SerializeField]
    private float mainMenuHighlightScale = 1.15f;

    /// <summary>
    /// Vrai pendant que le menu principal (Echap / Start manette) est
    /// ouvert -- utilise par Player.GetInput() pour desactiver la boucle
    /// ACT1-6 de la manette (A/B servent alors a valider/annuler dans ce
    /// menu, voir GamepadInput.MenuConfirmPressed/MenuCancelPressed).
    /// </summary>
    public bool IsMainMenuOpen => menus[0].alpha > 0;

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

    /// <summary>
    /// Les 6 Text affichant le bouton manette actuellement assigne a
    /// chaque sort (ACT1 a ACT6), dans le nouveau panneau "GamePad" du
    /// menu Keybinds -- construit au runtime, voir
    /// BuildGamepadKeybindSection(). Separe de keybindButtons ci-dessus
    /// (clavier) : keybindButtons est rempli une seule fois dans Awake(),
    /// avant que ces boutons manette n'existent.
    /// </summary>
    private readonly Text[] gamepadBindTexts = new Text[6];

    private void Awake()
    {
        keybindButtons = GameObject.FindGameObjectsWithTag("Keybind");
        tooltipText = tooltip.GetComponentInChildren<Text>();
    }

    // Use this for initialization
    void Start()
    {
        healthStat = targetFrame.GetComponentInChildren<Stat>();

        // Le texte du bouton "Tactile" du Main Menu est fige dans la scene --
        // on le met a jour ici pour qu'il reflete bien le choix sauvegarde
        // (voir ToggleTouchControls) des l'ouverture du menu, meme si on n'a
        // encore jamais touche au bouton cette session-ci.
        UpdateTouchControlsButtonText(TouchJoystick.IsEnabled);

        BuildGamepadKeybindSection();
    }

    private void OnDestroy()
    {
        if (KeybindManager.MyInstance != null)
        {
            KeybindManager.MyInstance.OnGamepadBindChanged -= UpdateGamepadKeyText;
        }
    }

    /// <summary>
    /// Construit au runtime, par CLONAGE de la section "Actions" existante
    /// du panneau Keybinds (voir Demo.unity, GameObject "TitleActions"),
    /// une nouvelle section "GamePad" juste en dessous, avec 6 lignes
    /// ACTION 1 a ACTION 6 permettant d'assigner le bouton de la manette
    /// qui declenche chaque sort -- exactement comme la section "Actions"
    /// le fait deja pour le clavier (KeybindManager.ActionBinds), mais
    /// pour KeybindManager.GamepadActionBinds.
    ///
    /// Construit par CODE plutot que par edition de la scene : cloner
    /// "TitleActions" recupere gratuitement tout son skin deja en place
    /// (police, sprite de bouton vert, ombre, mise en page
    /// Horizontal/VerticalLayoutGroup) sans avoir a le reconstruire a la
    /// main ni toucher Demo.unity -- seul le parent (Area, le Content du
    /// ScrollRect) a recu un ContentSizeFitter dans la scene pour que la
    /// zone de defilement s'agrandisse et englobe les nouvelles sections
    /// (ca corrige au passage le fait qu'ACTION 4 a 6, au clavier,
    /// existaient deja dans la scene mais etaient hors de la zone de
    /// defilement, donc invisibles).
    ///
    /// IMPORTANT : "TitleActions" sert a la fois de titre "Actions" ET de
    /// conteneur (VerticalLayoutGroup) pour ses 6 lignes, sur UNE SEULE
    /// RectTransform (374.76 x 30, pivot centre) -- ses lignes debordent
    /// simplement visuellement en dessous de cette petite boite (Unity ne
    /// les decoupe pas), ce qui est justement ce qui garde son titre bien
    /// centre en haut de la section. Agrandir cette RectTransform pour
    /// qu'elle "contienne" vraiment ses 6 lignes (ce qu'on a essaye avec
    /// un ContentSizeFitter dessus) la recentre AUTOUR de son pivot et
    /// deplace donc le titre au milieu de la section -- exactement le bug
    /// remonte par Aurora ("Actions"/"GamePad" affiches derriere ACTION
    /// 3). On NE touche donc PAS a la taille de TitleActions/TitleGamePad
    /// : a la place, on mesure la vraie hauteur de son contenu (en lisant
    /// la position reelle de sa derniere ligne apres un rebuild de mise
    /// en page force) et on insere un espaceur invisible, juste assez
    /// grand pour compenser l'ecart, avant la section suivante -- Area
    /// (le parent commun, voir plus haut) la positionne alors
    /// correctement tout seul, comme n'importe quel autre sibling.
    /// </summary>
    private void BuildGamepadKeybindSection()
    {
        GameObject actionsHeader = GameObject.Find("TitleActions");
        if (actionsHeader == null || !(actionsHeader.transform.parent is RectTransform area))
        {
            // Securite : si ce panneau n'existe pas dans cette scene (ex.
            // scene de test), on ne construit rien plutot que de planter.
            return;
        }

        RectTransform actionsRect = actionsHeader.GetComponent<RectTransform>();

        // Force le recalcul immediat de la mise en page -- a partir
        // d'Area (le PARENT), pas seulement de TitleActions : sinon
        // TitleActions lui-meme (sa position DANS Area, via le
        // VerticalLayoutGroup d'Area) peut encore avoir sa position
        // "brute" telle que sauvegardee dans la scene -- generalement
        // {0,0} -- au lieu de sa position reelle, le rebuild ne
        // s'executant normalement qu'en fin de frame. Rebuilder depuis
        // Area recalcule toute la hierarchie en cascade (Area -> ses
        // enfants -> leurs propres enfants), donc TitleActions ET ses 6
        // lignes se retrouvent tous les deux a une position fiable.
        LayoutRebuilder.ForceRebuildLayoutImmediate(area);

        float declaredHeight = actionsRect.rect.height;
        Transform lastActionsRow = actionsHeader.transform.GetChild(actionsHeader.transform.childCount - 1);
        RectTransform lastActionsRowRect = lastActionsRow.GetComponent<RectTransform>();

        // Distance entre le PIVOT (centre) de TitleActions et le bas
        // reel de sa derniere ligne -- la vraie hauteur de son contenu,
        // par opposition a sa hauteur DECLAREE (declaredHeight, restee
        // petite expres, voir plus haut).
        float realContentHeight = declaredHeight / 2f - (lastActionsRowRect.anchoredPosition.y - lastActionsRowRect.rect.height / 2f);

        // Position Y (dans le repere local d'Area) du bas REEL du contenu
        // de "Actions", c-a-d juste sous la ligne ACTION 6.
        float actionsContentBottomY = actionsRect.anchoredPosition.y + (lastActionsRowRect.anchoredPosition.y - lastActionsRowRect.rect.height / 2f);

        VerticalLayoutGroup areaLayout = area.GetComponent<VerticalLayoutGroup>();
        float areaSpacing = areaLayout != null ? areaLayout.spacing : 0f;

        // Espaceur invisible APRES TitleActions, pour qu'Area (via son
        // ContentSizeFitter) compte bien la vraie hauteur de la section
        // Actions dans sa propre hauteur totale (sinon elle ne compterait
        // que les 30px DECLARES de TitleActions, et la zone de defilement
        // s'arreterait bien avant la fin reelle des lignes ACTION 1-6).
        // Ce n'est plus cet espaceur qui fixe l'ecart visuel avant
        // "GamePad" (voir plus bas, positionnement manuel) : il sert
        // uniquement au calcul de taille.
        float spacerHeight = Mathf.Max(0f, (realContentHeight - declaredHeight) - areaSpacing);

        GameObject spacerGO = new GameObject("GamePadSectionSpacer", typeof(RectTransform));
        RectTransform spacerRect = spacerGO.GetComponent<RectTransform>();
        spacerRect.SetParent(area, false);
        spacerRect.anchorMin = new Vector2(0.5f, 0.5f);
        spacerRect.anchorMax = new Vector2(0.5f, 0.5f);
        spacerRect.pivot = new Vector2(0.5f, 0.5f);
        spacerRect.sizeDelta = new Vector2(actionsRect.sizeDelta.x, spacerHeight);
        spacerRect.SetSiblingIndex(actionsRect.GetSiblingIndex() + 1);

        GameObject gamepadHeader = Instantiate(actionsHeader, area);
        gamepadHeader.name = "TitleGamePad";
        gamepadHeader.transform.SetAsLastSibling();

        // CAUSE RACINE DE TOUTE CETTE SECTION (confirmee par plusieurs
        // allers-retours avec Aurora, diagnostic a l'ecran a l'appui) :
        // Area positionne ses enfants en cumulant leur hauteur DECLAREE +
        // son "spacing" (areaSpacing, 250 dans cette scene) -- deux fois
        // de suite de part et d'autre d'un espaceur intercale, ca donne un
        // plancher de 2*areaSpacing qui ne descend jamais en dessous d'un
        // certain ecart, meme en reduisant la hauteur de l'espaceur a 0.
        // Resultat : impossible d'obtenir un petit ecart "juste" entre
        // Actions et GamePad en jouant uniquement sur la hauteur d'un
        // espaceur soumis a la mise en page automatique d'Area.
        //
        // Solution : on sort TitleGamePad de la mise en page automatique
        // d'Area (LayoutElement.ignoreLayout = true) et on calcule sa
        // position directement, en pixels, a partir du vrai bas du
        // contenu "Actions" (actionsContentBottomY, mesure plus haut) --
        // ce qui decouple entierement l'ecart visuel voulu
        // (gamepadSectionGap) de la valeur d'espacement d'Area.
        RectTransform gamepadHeaderRect = gamepadHeader.GetComponent<RectTransform>();
        LayoutElement gamepadLayoutElement = gamepadHeader.GetComponent<LayoutElement>();
        if (gamepadLayoutElement == null)
        {
            gamepadLayoutElement = gamepadHeader.AddComponent<LayoutElement>();
        }
        gamepadLayoutElement.ignoreLayout = true;

        const float gamepadSectionGap = 20f;
        float gamepadDeclaredHeight = gamepadHeaderRect.rect.height;
        float gamepadTopY = actionsContentBottomY - gamepadSectionGap;
        gamepadHeaderRect.anchoredPosition = new Vector2(gamepadHeaderRect.anchoredPosition.x, gamepadTopY - gamepadDeclaredHeight / 2f);

        Text headerText = gamepadHeader.GetComponent<Text>();
        if (headerText != null)
        {
            headerText.text = "GamePad";
        }

        // Le clone a exactement les memes 6 enfants que "TitleActions"
        // (ACTION 1 a ACTION 6, clones avec) : on reutilise ces lignes
        // telles quelles, on change juste le bouton de chacune pour
        // qu'il assigne un bouton manette au lieu d'une touche clavier.
        for (int i = 0; i < gamepadBindTexts.Length && i < gamepadHeader.transform.childCount; i++)
        {
            string actKey = "ACT" + (i + 1);
            Transform row = gamepadHeader.transform.GetChild(i);

            Button button = row.GetComponentInChildren<Button>();
            if (button == null)
            {
                continue;
            }

            button.gameObject.name = "GP" + (i + 1);

            // RemoveAllListeners() NE retire PAS les listeners persistants
            // (ceux configures dans l'Inspecteur, ici l'appel a
            // KeyBindOnClick("ACT4") etc. copie depuis "TitleActions" lors
            // du clonage) -- un piege Unity bien connu. Remplacer
            // l'UnityEvent entier par un neuf est le seul moyen fiable de
            // s'en debarrasser : sinon cliquer ce bouton declencherait a
            // la fois la capture clavier (ancien listener) ET la capture
            // manette (nouveau), les deux ecoutant simultanement la
            // prochaine entree.
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => KeybindManager.MyInstance.GamepadKeyBindOnClick(actKey));

            gamepadBindTexts[i] = button.GetComponentInChildren<Text>();
        }

        if (KeybindManager.MyInstance != null)
        {
            KeybindManager.MyInstance.OnGamepadBindChanged += UpdateGamepadKeyText;

            foreach (KeyValuePair<string, KeyCode> bind in KeybindManager.MyInstance.GamepadActionBinds)
            {
                UpdateGamepadKeyText(bind.Key, bind.Value);
            }
        }

        // TitleGamePad etant maintenant en dehors de la mise en page
        // automatique d'Area (ignoreLayout), il ne contribue plus DU TOUT
        // a la hauteur totale qu'Area calcule pour elle-meme -- ni son
        // propre espacement normal (areaSpacing, qui se serait applique
        // juste avant lui), ni son contenu reel (ses lignes GP1-6, qui
        // debordent sous sa petite boite declaree) -- sans quoi la zone
        // de defilement s'arreterait net au niveau du titre "GamePad".
        // L'espaceur final doit donc, a lui seul, remplacer EXACTEMENT ce
        // que TitleGamePad aurait automatiquement apporte : un espacement
        // normal + toute sa hauteur de contenu reelle.
        LayoutRebuilder.ForceRebuildLayoutImmediate(area);

        float gamepadRealContentHeight = gamepadDeclaredHeight;
        if (gamepadHeader.transform.childCount > 0)
        {
            Transform lastGamepadRow = gamepadHeader.transform.GetChild(gamepadHeader.transform.childCount - 1);
            RectTransform lastGamepadRowRect = lastGamepadRow.GetComponent<RectTransform>();
            gamepadRealContentHeight = gamepadDeclaredHeight / 2f - (lastGamepadRowRect.anchoredPosition.y - lastGamepadRowRect.rect.height / 2f);
        }

        // Premiere valeur (areaSpacing + gamepadRealContentHeight, calquee
        // sur l'ancien total qui marchait avec TitleGamePad en layout
        // normal) laissait encore un peu de vide visible au gout d'Aurora.
        // On revient ici a une logique directe en pixels (comme pour
        // gamepadSectionGap plus haut) : juste assez pour couvrir le
        // debordement reel sous la boite declaree de TitleGamePad
        // (gamepadRealContentHeight - gamepadDeclaredHeight), plus une
        // petite marge de confort.
        const float gamepadTrailingMargin = 20f;
        float trailingSpacerHeight = Mathf.Max(0f, (gamepadRealContentHeight - gamepadDeclaredHeight) + gamepadTrailingMargin);

        GameObject trailingSpacerGO = new GameObject("GamePadSectionTrailingSpacer", typeof(RectTransform));
        RectTransform trailingSpacerRect = trailingSpacerGO.GetComponent<RectTransform>();
        trailingSpacerRect.SetParent(area, false);
        trailingSpacerRect.anchorMin = new Vector2(0.5f, 0.5f);
        trailingSpacerRect.anchorMax = new Vector2(0.5f, 0.5f);
        trailingSpacerRect.pivot = new Vector2(0.5f, 0.5f);
        trailingSpacerRect.sizeDelta = new Vector2(actionsRect.sizeDelta.x, trailingSpacerHeight);
        trailingSpacerRect.SetSiblingIndex(gamepadHeaderRect.GetSiblingIndex() + 1);

        // Force un dernier recalcul pour que la mise en page finale soit
        // bien a jour avant que le panneau ne s'affiche.
        LayoutRebuilder.ForceRebuildLayoutImmediate(area);
    }


    /// <summary>
    /// Met a jour le texte affichant le bouton manette assigne a
    /// l'emplacement de sort key ("ACT1".."ACT6") -- appelee a la
    /// construction du panneau "GamePad" (voir ci-dessus) et a chaque
    /// reassignation (voir KeybindManager.OnGamepadBindChanged).
    /// </summary>
    private void UpdateGamepadKeyText(string key, KeyCode bind)
    {
        if (string.IsNullOrEmpty(key) || !key.StartsWith("ACT"))
        {
            return;
        }

        if (!int.TryParse(key.Substring(3), out int number))
        {
            return;
        }

        int index = number - 1;
        if (index < 0 || index >= gamepadBindTexts.Length || gamepadBindTexts[index] == null)
        {
            return;
        }

        gamepadBindTexts[index].text = GamepadInput.GetFriendlyButtonName(bind);
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

        if (Input.GetKeyDown(KeyCode.Escape) || GamepadInput.StartPressed)
        {
            OpenClose(menus[0]);

            // Prepare/nettoie la navigation manette du menu principal a
            // chaque ouverture/fermeture (voir
            // UpdateMainMenuGamepadNavigation() plus bas) : les boutons
            // actifs sont redecouverts a chaque ouverture (au cas ou
            // SaveGame ou un autre bouton devienne actif/inactif), et la
            // selection visuelle d'Unity est nettoyee a la fermeture.
            if (IsMainMenuOpen)
            {
                mainMenuButtons = menus[0].GetComponentsInChildren<Button>(false);
                mainMenuSelectedIndex = 0;
                if (mainMenuButtons.Length > 0)
                {
                    EventSystem.current.SetSelectedGameObject(mainMenuButtons[0].gameObject);
                }
            }
            else
            {
                EventSystem.current.SetSelectedGameObject(null);
                ResetMainMenuHighlight();
            }
        }
        UpdateMainMenuGamepadNavigation();
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
            // Chaque classe a son propre arbre de talents (voir
            // Session.SelectedCharacterClass) : menus[7] pour le Mage
            // (Feu/Glace/Foudre), menus[8] pour le Guerrier (Frappe
            // Enflammee/Coup de Bouclier/Laceration, voir
            // TalentTreeGuerrier dans Demo.unity).
            OpenClose(Session.SelectedCharacterClass == "Guerrier" ? menus[8] : menus[7]);
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

    /// <summary>
    /// Navigation manette dans le menu principal (menus[0]) : stick
    /// gauche haut/bas pour deplacer la selection parmi mainMenuButtons
    /// (decouverts a l'ouverture, voir plus haut dans Update()), A pour
    /// valider le bouton selectionne, B pour fermer le menu sans valider.
    /// La mise en surbrillance visuelle est entierement deleguee a
    /// EventSystem.current.SetSelectedGameObject -- ca reutilise le
    /// skin "selected" existant des boutons sans dupliquer son rendu
    /// ici. Ne s'appuie pas sur la navigation manette integree d'Unity
    /// (Submit/Cancel) : elle est bindee sur les memes boutons
    /// manette (0/1) qu'ACT1/ACT2 et declencherait les deux a la fois.
    /// </summary>
    private void UpdateMainMenuGamepadNavigation()
    {
        if (!IsMainMenuOpen || mainMenuButtons == null || mainMenuButtons.Length == 0)
        {
            return;
        }

        if (GamepadInput.NavigateDownPressed)
        {
            mainMenuSelectedIndex = (mainMenuSelectedIndex + 1) % mainMenuButtons.Length;
            EventSystem.current.SetSelectedGameObject(mainMenuButtons[mainMenuSelectedIndex].gameObject);
        }
        if (GamepadInput.NavigateUpPressed)
        {
            mainMenuSelectedIndex = (mainMenuSelectedIndex - 1 + mainMenuButtons.Length) % mainMenuButtons.Length;
            EventSystem.current.SetSelectedGameObject(mainMenuButtons[mainMenuSelectedIndex].gameObject);
        }
        if (GamepadInput.MenuConfirmPressed)
        {
            mainMenuButtons[mainMenuSelectedIndex].onClick.Invoke();
        }
        if (GamepadInput.MenuCancelPressed)
        {
            OpenClose(menus[0]);
            EventSystem.current.SetSelectedGameObject(null);
            ResetMainMenuHighlight();
            return;
        }

        // Reapplique la surbrillance a chaque frame (et pas seulement
        // quand la selection change) : ca evite qu'elle disparaisse si le
        // skin "Selected" du bouton (gere par Unity, voir plus haut) se
        // redessine par-dessus notre couleur/echelle.
        HighlightMainMenuButton(mainMenuSelectedIndex);
    }

    /// <summary>
    /// Applique une surbrillance bien visible (couleur + agrandissement,
    /// voir mainMenuHighlightColor/mainMenuHighlightScale) au bouton du
    /// menu principal a l'index selectedIndex, et remet tous les autres a
    /// leur apparence normale.
    /// </summary>
    private void HighlightMainMenuButton(int selectedIndex)
    {
        if (mainMenuButtons == null)
        {
            return;
        }

        for (int i = 0; i < mainMenuButtons.Length; i++)
        {
            bool isSelected = i == selectedIndex;
            Image image = mainMenuButtons[i].GetComponent<Image>();
            if (image != null)
            {
                image.color = isSelected ? mainMenuHighlightColor : Color.white;
            }
            mainMenuButtons[i].transform.localScale = isSelected
                ? Vector3.one * mainMenuHighlightScale
                : Vector3.one;
        }
    }

    /// <summary>
    /// Remet tous les boutons du menu principal a leur apparence normale
    /// (voir HighlightMainMenuButton ci-dessus) -- a appeler a la
    /// fermeture du menu pour ne pas laisser un bouton agrandi/colore.
    /// </summary>
    private void ResetMainMenuHighlight()
    {
        HighlightMainMenuButton(-1);
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

    // Appele par le bouton "Tactile" du Main Menu (voir Demo.unity, panneau
    // MainMenu, a cote de Deconnexion) : permet de masquer le joystick
    // tactile (voir TouchJoystick.cs) pour les joueurs au clavier/souris sur
    // PC, qui le trouvaient genant au-dessus du chat. Le choix est retenu
    // (PlayerPrefs, voir TouchJoystick.SetEnabled) pour la prochaine
    // connexion.
    public void ToggleTouchControls()
    {
        bool isEnabled = !TouchJoystick.IsEnabled;
        TouchJoystick.SetEnabled(isEnabled);
        UpdateTouchControlsButtonText(isEnabled);
    }

    private void UpdateTouchControlsButtonText(bool isEnabled)
    {
        GameObject buttonGO = GameObject.Find("ToggleTouchControls");
        if (buttonGO == null)
        {
            return;
        }

        Text text = buttonGO.GetComponentInChildren<Text>();
        if (text != null)
        {
            text.text = isEnabled ? "Tactile : ON" : "Tactile : OFF";
        }
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
