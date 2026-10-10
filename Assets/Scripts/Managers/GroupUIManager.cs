using Mirror;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Menu contextuel (clic droit sur un autre joueur -> "Inviter au groupe")
/// et popup d'invitation (Accepter/Refuser) recue par le joueur invite.
///
/// Purement visuel : ce composant ne touche jamais a Mirror directement,
/// il relaie vers Player.MyInstance (voir Player.InviteToGroup /
/// Player.RespondToGroupInvite), qui route lui-meme vers PlayerGroupSync --
/// meme separation que ChatManager/PlayerChatSync.
///
/// Toute l'UI est construite au demarrage par code, exactement comme
/// TouchJoystick.cs (voir son commentaire) : ca evite de devoir recopier a
/// la main des guids de composants Unity non deja verifies dans ce projet
/// (ScrollRect, etc. -- ici pas de composant exotique, mais le principe
/// "tout en code" reste le plus simple et le plus sur a maintenir). Un
/// Canvas dedie, avec la meme reference d'echelle que UICanvas (voir
/// TouchJoystick), pour rester aligne pixel pour pixel avec le reste de
/// l'interface.
/// </summary>
public class GroupUIManager : MonoBehaviour
{
    private static GroupUIManager instance;

    public static GroupUIManager MyInstance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GroupUIManager>();
            }

            return instance;
        }
    }

    private RectTransform canvasRect;
    private RectTransform contextMenuRect;
    private GameObject contextMenuGO;
    private Player contextMenuTarget;

    private GameObject invitePopupGO;
    private Text invitePopupText;
    private NetworkIdentity pendingInviterIdentity;

    private RectTransform playerTargetFrameRect;
    private GameObject playerTargetFrameGO;
    private Text playerTargetNameText;
    private Stat playerTargetHealthStat;
    private Stat playerTargetManaStat;
    private Text playerTargetLevelText;
    private Player currentPlayerTarget;
    private PlayerVitalsSync currentPlayerTargetVitals;

    // Nombre maximum de membres dans un groupe, portrait du chef compris --
    // doit rester identique a PlayerGroupSync.MaxGroupSize (voir ce
    // fichier) : c'est ce qui determine combien d'emplacements de groupe
    // preparer sous notre propre portrait (le 5e membre, c'est nous).
    private const int MaxGroupSize = 5;

    private readonly System.Collections.Generic.List<PartyFrameSlot> partyFrameSlots = new System.Collections.Generic.List<PartyFrameSlot>();

    private GameObject cachedHudFrameSource;
    private RectTransform ownHudFrameRect;
    private bool sceneUIBuilt;

    // Le bouton du menu contextuel ("Inviter au groupe" / "Quitter le
    // groupe", voir ShowContextMenu/ShowLeaveGroupMenu) est un seul et
    // meme objet reutilise pour les deux actions -- son libelle et son
    // action changent dynamiquement au lieu de dupliquer tout le menu.
    private Text contextMenuButtonText;
    private System.Action pendingContextMenuAction;

    /// <summary>
    /// Un emplacement de cadre de groupe (voir BuildPartyFrames) : memes
    /// composants que playerTargetFrameGO (portrait/niveau/vie/mana/nom
    /// clones depuis "UICanvas/Frame"), mais garde en plus le netId du
    /// membre actuellement affiche pour savoir quand (re)snapper les
    /// barres au lieu de les laisser glisser depuis l'ancienne valeur
    /// (voir RefreshPartyFrames).
    /// </summary>
    private class PartyFrameSlot
    {
        public GameObject Root;
        public Text NameText;
        public Stat HealthStat;
        public Stat ManaStat;
        public Text LevelText;
        public uint BoundNetId;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (instance != null)
        {
            return;
        }

        GameObject managerGO = new GameObject("GroupUIManager");
        instance = managerGO.AddComponent<GroupUIManager>();
        Object.DontDestroyOnLoad(managerGO);

        instance.BuildUI();
    }

    private void BuildUI()
    {
        GameObject canvasGO = new GameObject("GroupUICanvas");
        canvasGO.transform.SetParent(transform, false);
        Canvas canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000; // au-dessus du chat (voir ChatManager) et du joystick tactile (voir TouchJoystick)

        CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        // Meme reference que UICanvas (voir Demo.unity) et TouchJoystickCanvas
        // (voir TouchJoystick.cs) -- un pixel ici correspond exactement a un
        // pixel du reste de l'interface.
        scaler.referenceResolution = new Vector2(800, 600);
        scaler.matchWidthOrHeight = 1f;

        canvasGO.AddComponent<GraphicRaycaster>();
        canvasRect = canvasGO.GetComponent<RectTransform>();

        BuildContextMenu(canvasRect);
        BuildInvitePopup(canvasRect);

        // PAS d'appel ici a tout ce qui depend de "UICanvas/Frame" (portrait
        // de cible-joueur, cadres de groupe) : contrairement au menu
        // contextuel et au popup d'invitation (construits de toutes pieces
        // par code, sans dependance de scene), ces elements CLONENT
        // "UICanvas/Frame", qui vit dans Demo.unity -- et Install() (voir
        // [RuntimeInitializeOnLoadMethod]) s'execute UNE SEULE FOIS, juste
        // apres le chargement de la PREMIERE scene au demarrage du jeu,
        // c'est a dire Login.unity (voir EditorBuildSettings), bien avant
        // que Demo.unity (et donc "UICanvas/Frame") n'existe. GameObject.
        // Find echouerait donc silencieusement a ce moment-la, et comme ce
        // GroupUIManager persiste ensuite (DontDestroyOnLoad) sans jamais
        // reessayer, rien ne se construirait plus JAMAIS. On construit donc
        // ces elements paresseusement depuis Update() (voir
        // TryBuildSceneDependentUI), qui reessaie chaque frame jusqu'a ce
        // que Demo.unity soit charge.
    }

    private void BuildContextMenu(RectTransform parent)
    {
        contextMenuGO = new GameObject("GroupContextMenu", typeof(RectTransform));
        contextMenuGO.transform.SetParent(parent, false);

        contextMenuRect = contextMenuGO.GetComponent<RectTransform>();
        contextMenuRect.anchorMin = new Vector2(0.5f, 0.5f);
        contextMenuRect.anchorMax = new Vector2(0.5f, 0.5f);
        // Pivot en haut-centre : le point donne a ShowContextMenu est
        // desormais le bas du cadre-portrait qui a declenche l'ouverture
        // (voir ShowContextMenu), donc le menu doit s'etendre vers le bas
        // en restant centre horizontalement sous ce cadre -- avant, le
        // pivot (0,1) etendait le menu depuis le point de clic brut, qui
        // tombait forcement SUR le portrait lui-meme (puisque c'est lui
        // qu'on vient de cliquer), faisant apparaitre le menu par-dessus/
        // derriere le portrait au lieu d'a cote.
        contextMenuRect.pivot = new Vector2(0.5f, 1f);
        contextMenuRect.sizeDelta = new Vector2(162f, 38f);

        // Bordure bronze/doree -- meme famille de teinte que la barre de
        // nom (voir RepurposeXpBarAsNameLabel, qui reutilise la barre d'XP
        // orange/or), pour rester dans le theme du HUD plutot qu'un simple
        // encart gris plat.
        Image border = contextMenuGO.AddComponent<Image>();
        border.color = new Color(0.22f, 0.14f, 0.03f, 0.95f);

        GameObject buttonGO = BuildThemedButton(contextMenuRect, "Inviter au groupe", Vector2.zero, new Vector2(154f, 30f));
        contextMenuButtonText = buttonGO.GetComponentInChildren<Text>();
        Button button = buttonGO.GetComponent<Button>();
        button.onClick.AddListener(OnContextMenuButtonClicked);

        contextMenuGO.SetActive(false);
    }

    private void BuildInvitePopup(RectTransform parent)
    {
        invitePopupGO = new GameObject("GroupInvitePopup", typeof(RectTransform));
        invitePopupGO.transform.SetParent(parent, false);

        RectTransform rect = invitePopupGO.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(268f, 120f);
        rect.anchoredPosition = new Vector2(0f, 80f);

        // Meme esprit que le menu "Inviter au groupe" (voir
        // BuildContextMenu) : une bordure bronze/doree autour d'un panneau
        // fonce, au lieu du simple encart noir plat d'avant -- pour rester
        // dans le meme theme que le reste du HUD de groupe.
        Image border = invitePopupGO.AddComponent<Image>();
        border.color = new Color(0.22f, 0.14f, 0.03f, 0.95f);

        GameObject innerGO = new GameObject("InnerPanel", typeof(RectTransform));
        innerGO.transform.SetParent(invitePopupGO.transform, false);
        RectTransform innerRect = innerGO.GetComponent<RectTransform>();
        innerRect.anchorMin = Vector2.zero;
        innerRect.anchorMax = Vector2.one;
        innerRect.offsetMin = new Vector2(4f, 4f);
        innerRect.offsetMax = new Vector2(-4f, -4f);

        Image innerBg = innerGO.AddComponent<Image>();
        innerBg.color = new Color(0.07f, 0.07f, 0.09f, 0.95f);

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(invitePopupGO.transform, false);
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0f, -10f);
        textRect.sizeDelta = new Vector2(-20f, 56f);

        invitePopupText = textGO.AddComponent<Text>();
        invitePopupText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        invitePopupText.fontSize = 14;
        invitePopupText.fontStyle = FontStyle.Bold;
        // Dore clair, assorti a la bordure/au bouton "Inviter au groupe"
        // et a la barre de nom (voir BuildThemedButton/
        // RepurposeXpBarAsNameLabel), plutot que du blanc neutre.
        invitePopupText.color = new Color(0.95f, 0.82f, 0.45f, 1f);
        invitePopupText.alignment = TextAnchor.UpperCenter;
        invitePopupText.horizontalOverflow = HorizontalWrapMode.Wrap;
        invitePopupText.verticalOverflow = VerticalWrapMode.Overflow;
        invitePopupText.text = "";

        // Fine ligne doree sous le titre, comme separateur -- petite
        // touche "en-tete" qui reprend la teinte bronze de la bordure.
        GameObject dividerGO = new GameObject("Divider", typeof(RectTransform));
        dividerGO.transform.SetParent(invitePopupGO.transform, false);
        RectTransform dividerRect = dividerGO.GetComponent<RectTransform>();
        dividerRect.anchorMin = new Vector2(0f, 1f);
        dividerRect.anchorMax = new Vector2(1f, 1f);
        dividerRect.pivot = new Vector2(0.5f, 1f);
        dividerRect.anchoredPosition = new Vector2(0f, -48f);
        dividerRect.sizeDelta = new Vector2(-24f, 2f);

        Image dividerImg = dividerGO.AddComponent<Image>();
        dividerImg.color = new Color(0.6f, 0.45f, 0.15f, 0.8f);

        GameObject acceptGO = BuildButton(invitePopupGO.GetComponent<RectTransform>(), "Accepter", new Vector2(-62f, 14f), new Vector2(112f, 32f), new Color(0.2f, 0.55f, 0.22f, 1f));
        acceptGO.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0f);
        acceptGO.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0f);
        acceptGO.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
        acceptGO.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
        acceptGO.GetComponent<Button>().onClick.AddListener(OnAcceptButtonClicked);

        GameObject declineGO = BuildButton(invitePopupGO.GetComponent<RectTransform>(), "Refuser", new Vector2(62f, 14f), new Vector2(112f, 32f), new Color(0.58f, 0.2f, 0.2f, 1f));
        declineGO.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0f);
        declineGO.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0f);
        declineGO.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
        declineGO.GetComponentInChildren<Text>().fontStyle = FontStyle.Bold;
        declineGO.GetComponent<Button>().onClick.AddListener(OnDeclineButtonClicked);

        invitePopupGO.SetActive(false);
    }

    /// <summary>
    /// Essaie, chaque frame tant que ce n'est pas fait, de construire tout
    /// ce qui depend de "UICanvas/Frame" (portrait de cible-joueur, cadres
    /// de groupe) -- voir le commentaire dans BuildUI() pour pourquoi ca ne
    /// peut pas se faire directement dans BuildUI()/Install(). Une fois
    /// "UICanvas/Frame" trouve (des que Demo.unity est charge), tout se
    /// construit une seule fois pour de bon.
    /// </summary>
    private void TryBuildSceneDependentUI()
    {
        GameObject sourceFrame = GameObject.Find("UICanvas/Frame");

        if (sourceFrame == null)
        {
            return;
        }

        cachedHudFrameSource = sourceFrame;
        ownHudFrameRect = sourceFrame.GetComponent<RectTransform>();

        BuildPlayerTargetFrame(canvasRect);
        BuildPartyFrames(canvasRect);

        sceneUIBuilt = true;
    }

    /// <summary>
    /// Clone "UICanvas/Frame" (voir Player.ResolveLocalReferences -- c'est
    /// exactement l'objet que "UICanvas/Frame/HealthBackground/Health" etc.
    /// designent) : ca evite de devoir recopier a la main des guids de
    /// sprites qu'on ne peut pas resoudre depuis du code pur a l'execution
    /// (pas d'AssetDatabase en dehors de l'Editeur). Le clone recupere donc
    /// automatiquement le meme portrait, le meme cadre rond, et des barres
    /// de vie/mana en etat de marche (composants Stat inclus). Reutilise a
    /// la fois par BuildPlayerTargetFrame et BuildPartyFrames.
    /// </summary>
    private GameObject CloneHudFrame(string name, RectTransform parent)
    {
        if (cachedHudFrameSource == null)
        {
            return null;
        }

        GameObject clone = Instantiate(cachedHudFrameSource);
        clone.name = name;
        clone.transform.SetParent(parent, false);

        return clone;
    }

    private static Stat FindBarStat(GameObject frame, string backgroundChildName)
    {
        Transform background = frame.transform.Find(backgroundChildName);
        return background != null ? background.GetComponentInChildren<Stat>() : null;
    }

    private static Text FindLevelText(GameObject frame)
    {
        Transform levelFrame = frame.transform.Find("LevelFrame");
        return levelFrame != null ? levelFrame.GetComponentInChildren<Text>() : null;
    }

    /// <summary>
    /// Le nom du joueur n'a pas sa place dans le HUD d'origine (on n'a
    /// jamais besoin d'afficher notre propre nom), donc rien dans le clone
    /// ne s'en charge nativement -- mais la barre d'XP, elle, n'a pas de
    /// sens pour une cible ou un membre du groupe (comme sur WoW). On
    /// reutilise donc directement cet emplacement (deja positionne pile en
    /// dessous de la barre de mana, deja a la bonne taille -- voir
    /// "XPBackground" dans Demo.unity : {109.14, 13.94}, exactement comme
    /// Health/ManaBackground) au lieu d'ajouter une etiquette a part avec
    /// son propre style : on retire juste le Stat qui pilote le
    /// remplissage (la barre garde son fillAmount a 1 par defaut, donc
    /// reste pleine comme un simple fond uni) et la grille decorative de
    /// progression, puis on ecrit le nom du joueur a la place du texte
    /// "actuel/max". Resultat : exactement le meme theme visuel, exactement
    /// la meme taille que les barres de vie/mana, a l'endroit demande.
    /// </summary>
    private static Text RepurposeXpBarAsNameLabel(GameObject frame)
    {
        Transform xpBackground = frame.transform.Find("XPBackground");

        if (xpBackground == null)
        {
            return null;
        }

        xpBackground.gameObject.name = "NameBackground";

        Transform xpFill = xpBackground.Find("XP");

        if (xpFill == null)
        {
            return null;
        }

        Stat xpStat = xpFill.GetComponent<Stat>();
        if (xpStat != null)
        {
            Destroy(xpStat);
        }

        Transform xpGrid = xpFill.Find("XPGrid");
        if (xpGrid != null)
        {
            Destroy(xpGrid.gameObject);
        }

        Transform valueText = xpFill.Find("ValueText");
        Text nameText = valueText != null ? valueText.GetComponent<Text>() : null;

        if (nameText != null)
        {
            nameText.text = "";
        }

        return nameText;
    }

    /// <summary>
    /// Portrait de la cible-joueur (style WoW) : affiche au clic GAUCHE sur
    /// un autre joueur (voir GameManager.ClickTarget -- branche
    /// "PlayerClickable"). Meme emplacement ecran que l'ancien TargetFrame
    /// des monstres dans Demo.unity (ancre coin haut-gauche), decale de
    /// 150px vers la gauche pour le rapprocher de notre propre portrait :
    /// les deux cadres (cible-monstre et cible-joueur) sont mutuellement
    /// exclusifs (un seul affiche a la fois).
    ///
    /// Le clic DROIT sur ce portrait (et uniquement sur lui) affiche le
    /// menu "Inviter au groupe" (voir Update() plus bas).
    /// </summary>
    private void BuildPlayerTargetFrame(RectTransform parent)
    {
        playerTargetFrameGO = CloneHudFrame("PlayerTargetFrame", parent);

        if (playerTargetFrameGO == null)
        {
            return;
        }

        playerTargetFrameRect = playerTargetFrameGO.GetComponent<RectTransform>();
        playerTargetFrameRect.anchorMin = new Vector2(0f, 1f);
        playerTargetFrameRect.anchorMax = new Vector2(0f, 1f);
        playerTargetFrameRect.pivot = new Vector2(0.5f, 0.5f);
        playerTargetFrameRect.anchoredPosition = new Vector2(226.7f, -30.599976f);

        playerTargetHealthStat = FindBarStat(playerTargetFrameGO, "HealthBackground");
        playerTargetManaStat = FindBarStat(playerTargetFrameGO, "ManaBackground");
        playerTargetLevelText = FindLevelText(playerTargetFrameGO);

        playerTargetNameText = RepurposeXpBarAsNameLabel(playerTargetFrameGO);

        playerTargetFrameGO.SetActive(false);
    }

    /// <summary>
    /// Cadres des membres du groupe (style WoW) : empiles verticalement
    /// sous notre propre portrait ("UICanvas/Frame" dans Demo.unity,
    /// {31.9, -30.6}, 57.6x57.6). MaxGroupSize - 1 emplacements : le groupe
    /// compte au maximum MaxGroupSize joueurs (voir PlayerGroupSync), nous
    /// y compris -- notre propre portrait (deja affiche par le HUD
    /// d'origine) n'a donc jamais besoin d'un emplacement ici.
    /// </summary>
    private void BuildPartyFrames(RectTransform parent)
    {
        int slotCount = MaxGroupSize - 1;

        for (int i = 0; i < slotCount; i++)
        {
            GameObject slotGO = CloneHudFrame("PartyFrame" + (i + 1), parent);

            if (slotGO == null)
            {
                continue;
            }

            RectTransform slotRect = slotGO.GetComponent<RectTransform>();
            slotRect.anchorMin = new Vector2(0f, 1f);
            slotRect.anchorMax = new Vector2(0f, 1f);
            slotRect.pivot = new Vector2(0.5f, 0.5f);
            // 65.6 = 57.6 (hauteur du cadre) + 8 (petit espace) : un
            // emplacement par ligne, empile sous notre propre portrait.
            slotRect.anchoredPosition = new Vector2(31.900024f, -30.599976f - (i + 1) * 65.6f);

            PartyFrameSlot slot = new PartyFrameSlot
            {
                Root = slotGO,
                HealthStat = FindBarStat(slotGO, "HealthBackground"),
                ManaStat = FindBarStat(slotGO, "ManaBackground"),
                LevelText = FindLevelText(slotGO),
                NameText = RepurposeXpBarAsNameLabel(slotGO),
                BoundNetId = 0
            };

            slotGO.SetActive(false);
            partyFrameSlots.Add(slot);
        }
    }

    /// <summary>
    /// Appele par GameManager.ClickTarget quand on clique GAUCHE sur un
    /// autre joueur (collider "PlayerClickable").
    /// </summary>
    public void ShowPlayerTargetFrame(Player target)
    {
        if (target == null || playerTargetFrameGO == null)
        {
            return;
        }

        currentPlayerTarget = target;
        currentPlayerTargetVitals = target.GetComponent<PlayerVitalsSync>();

        PlayerChatSync chatSync = target.GetComponent<PlayerChatSync>();
        if (playerTargetNameText != null)
        {
            playerTargetNameText.text = chatSync != null ? chatSync.PlayerName : "Joueur";
        }

        RefreshPlayerTargetVitals(true);

        playerTargetFrameGO.SetActive(true);
    }

    public void HidePlayerTargetFrame()
    {
        if (playerTargetFrameGO != null)
        {
            playerTargetFrameGO.SetActive(false);
        }

        currentPlayerTarget = null;
        currentPlayerTargetVitals = null;
    }

    /// <summary>
    /// Vie/mana/niveau de la cible-joueur, lus depuis sa PlayerVitalsSync
    /// (voir ce script -- seul le client du joueur concerne connait ses
    /// propres valeurs, PlayerVitalsSync les diffuse aux autres). Appele une
    /// fois en "snap" (instantane, pas de lerp) a la selection, puis chaque
    /// frame tant que le cadre est affiche pour rester a jour (meme
    /// principe que Stat.Update(), qui lisse deja l'animation des barres).
    /// </summary>
    private void RefreshPlayerTargetVitals(bool snap)
    {
        if (currentPlayerTargetVitals == null)
        {
            return;
        }

        float hpCur = currentPlayerTargetVitals.CurrentHealth;
        float hpMax = Mathf.Max(currentPlayerTargetVitals.MaxHealth, 1f);
        float mpCur = currentPlayerTargetVitals.CurrentMana;
        float mpMax = Mathf.Max(currentPlayerTargetVitals.MaxMana, 1f);

        if (playerTargetHealthStat != null)
        {
            if (snap)
            {
                playerTargetHealthStat.Initialize(hpCur, hpMax);
            }
            else
            {
                playerTargetHealthStat.MyMaxValue = hpMax;
                playerTargetHealthStat.MyCurrentValue = hpCur;
            }
        }

        if (playerTargetManaStat != null)
        {
            if (snap)
            {
                playerTargetManaStat.Initialize(mpCur, mpMax);
            }
            else
            {
                playerTargetManaStat.MyMaxValue = mpMax;
                playerTargetManaStat.MyCurrentValue = mpCur;
            }
        }

        if (playerTargetLevelText != null)
        {
            playerTargetLevelText.text = currentPlayerTargetVitals.Level.ToString();
        }
    }

    /// <summary>
    /// Met a jour les cadres de groupe a partir de PlayerGroupSync.
    /// MyGroupMembers (voir ce script -- SyncList tenue a jour par le
    /// serveur a chaque changement de composition du groupe, deja filtree
    /// via NetworkClient.spawned pour retrouver chaque Player/
    /// PlayerVitalsSync distant). La liste contient TOUS les membres, nous
    /// y compris -- on se filtre nous-memes ici (notre propre portrait est
    /// deja affiche par le HUD d'origine, pas besoin d'un emplacement).
    /// </summary>
    private void RefreshPartyFrames()
    {
        if (partyFrameSlots.Count == 0 || Player.MyInstance == null)
        {
            return;
        }

        PlayerGroupSync mySync = Player.MyInstance.GetComponent<PlayerGroupSync>();
        NetworkIdentity myIdentity = Player.MyInstance.GetComponent<NetworkIdentity>();

        int slotIndex = 0;

        if (mySync != null && myIdentity != null)
        {
            foreach (uint memberNetId in mySync.MyGroupMembers)
            {
                if (memberNetId == myIdentity.netId)
                {
                    continue;
                }

                if (slotIndex >= partyFrameSlots.Count)
                {
                    // Ne devrait pas arriver (groupe limite a MaxGroupSize,
                    // voir PlayerGroupSync), simple garde-fou.
                    break;
                }

                ShowPartySlot(partyFrameSlots[slotIndex], memberNetId);
                slotIndex++;
            }
        }

        for (int i = slotIndex; i < partyFrameSlots.Count; i++)
        {
            HidePartySlot(partyFrameSlots[i]);
        }
    }

    private void ShowPartySlot(PartyFrameSlot slot, uint memberNetId)
    {
        bool isNewBinding = slot.BoundNetId != memberNetId;

        if (isNewBinding)
        {
            slot.BoundNetId = memberNetId;

            Player memberPlayer = ResolvePlayerByNetId(memberNetId);
            PlayerChatSync chatSync = memberPlayer != null ? memberPlayer.GetComponent<PlayerChatSync>() : null;

            if (slot.NameText != null)
            {
                slot.NameText.text = chatSync != null ? chatSync.PlayerName : "Joueur";
            }
        }

        PlayerVitalsSync vitals = ResolveVitalsByNetId(memberNetId);

        if (vitals != null)
        {
            float hpCur = vitals.CurrentHealth;
            float hpMax = Mathf.Max(vitals.MaxHealth, 1f);
            float mpCur = vitals.CurrentMana;
            float mpMax = Mathf.Max(vitals.MaxMana, 1f);

            if (slot.HealthStat != null)
            {
                if (isNewBinding) { slot.HealthStat.Initialize(hpCur, hpMax); }
                else { slot.HealthStat.MyMaxValue = hpMax; slot.HealthStat.MyCurrentValue = hpCur; }
            }

            if (slot.ManaStat != null)
            {
                if (isNewBinding) { slot.ManaStat.Initialize(mpCur, mpMax); }
                else { slot.ManaStat.MyMaxValue = mpMax; slot.ManaStat.MyCurrentValue = mpCur; }
            }

            if (slot.LevelText != null)
            {
                slot.LevelText.text = vitals.Level.ToString();
            }
        }

        if (!slot.Root.activeSelf)
        {
            slot.Root.SetActive(true);
        }
    }

    private void HidePartySlot(PartyFrameSlot slot)
    {
        if (slot.Root.activeSelf)
        {
            slot.Root.SetActive(false);
        }

        slot.BoundNetId = 0;
    }

    private static Player ResolvePlayerByNetId(uint netId)
    {
        if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity) && identity != null)
        {
            return identity.GetComponent<Player>();
        }

        return null;
    }

    private static PlayerVitalsSync ResolveVitalsByNetId(uint netId)
    {
        if (NetworkClient.spawned.TryGetValue(netId, out NetworkIdentity identity) && identity != null)
        {
            return identity.GetComponent<PlayerVitalsSync>();
        }

        return null;
    }

    /// <summary>
    /// Construit paresseusement le portrait de cible-joueur et les cadres
    /// de groupe des que possible (voir TryBuildSceneDependentUI), tient a
    /// jour les cadres de groupe en continu, et gere les deux points
    /// d'entree du menu contextuel partage (voir ShowContextMenu/
    /// ShowLeaveGroupMenu) : un clic droit sur le portrait de la cible-
    /// joueur affiche "Inviter au groupe", un clic droit sur NOTRE PROPRE
    /// portrait (si on est deja en groupe) affiche "Quitter le groupe".
    /// </summary>
    private void Update()
    {
        if (!sceneUIBuilt)
        {
            TryBuildSceneDependentUI();
        }

        RefreshPartyFrames();

        bool targetFrameShown = currentPlayerTarget != null && playerTargetFrameGO != null && playerTargetFrameGO.activeSelf;

        if (targetFrameShown)
        {
            RefreshPlayerTargetVitals(false);
        }

        if (!Input.GetMouseButtonDown(1))
        {
            return;
        }

        Vector3 pointerPos = TouchInput.GetPointerPosition();

        if (targetFrameShown && RectTransformUtility.RectangleContainsScreenPoint(playerTargetFrameRect, pointerPos, null))
        {
            ShowContextMenu(currentPlayerTarget, playerTargetFrameRect);
            return;
        }

        if (ownHudFrameRect != null && Player.MyInstance != null &&
            RectTransformUtility.RectangleContainsScreenPoint(ownHudFrameRect, pointerPos, null))
        {
            PlayerGroupSync mySync = Player.MyInstance.GetComponent<PlayerGroupSync>();

            // MyGroupMembers inclut toujours notre propre netId (voir
            // PlayerGroupSync) : Count > 1 signifie donc qu'on est dans un
            // groupe avec au moins un autre membre. Pas de menu sinon --
            // rien a quitter.
            if (mySync != null && mySync.MyGroupMembers.Count > 1)
            {
                ShowLeaveGroupMenu(ownHudFrameRect);
            }
        }
    }

    private GameObject BuildButton(RectTransform parent, string label, Vector2 anchoredPosition, Vector2 size, Color color)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = go.AddComponent<Image>();
        image.color = color;

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        GameObject textGO = new GameObject("Label", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textGO.AddComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 13;
        text.color = Color.white;
        text.raycastTarget = false;

        return go;
    }

    /// <summary>
    /// Variante doree/bronze de BuildButton, utilisee par "Inviter au
    /// groupe" (voir BuildContextMenu) pour rester dans le meme theme que
    /// les barres du HUD (vie verte, mana bleue, nom orange/or -- voir
    /// RepurposeXpBarAsNameLabel) plutot que le gris neutre de BuildButton.
    /// </summary>
    private GameObject BuildThemedButton(RectTransform parent, string label, Vector2 anchoredPosition, Vector2 size)
    {
        GameObject go = new GameObject(label + "Button", typeof(RectTransform));
        go.transform.SetParent(parent, false);

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = size;
        rect.anchoredPosition = anchoredPosition;

        Image image = go.AddComponent<Image>();
        image.color = new Color(0.82f, 0.62f, 0.16f, 1f);

        Button button = go.AddComponent<Button>();
        button.targetGraphic = image;

        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(0.92f, 0.74f, 0.28f, 1f);
        colors.pressedColor = new Color(0.68f, 0.5f, 0.1f, 1f);
        button.colors = colors;

        GameObject textGO = new GameObject("Label", typeof(RectTransform));
        textGO.transform.SetParent(go.transform, false);
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;

        Text text = textGO.AddComponent<Text>();
        text.text = label;
        text.alignment = TextAnchor.MiddleCenter;
        text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        text.fontSize = 13;
        text.fontStyle = FontStyle.Bold;
        text.color = new Color(0.22f, 0.12f, 0.02f, 1f);
        text.raycastTarget = false;

        return go;
    }

    /// <summary>
    /// Place le menu contextuel juste EN DESSOUS du cadre donne, centre
    /// horizontalement, au lieu du point de clic brut : le clic se fait
    /// forcement SUR le cadre clique (portrait de cible ou notre propre
    /// portrait), donc ancrer au point de clic faisait apparaitre le menu
    /// par-dessus/derriere ce cadre au lieu d'a cote. Reutilise par
    /// ShowContextMenu et ShowLeaveGroupMenu -- seul ce qui determine le
    /// cadre-ancre change entre les deux.
    /// </summary>
    private void PositionContextMenuBelow(RectTransform anchorRect)
    {
        // Point juste sous le bas du cadre (6px d'espace), converti du
        // monde vers l'ecran puis vers l'espace local du canvas.
        Vector3 anchorWorldPoint = anchorRect.TransformPoint(new Vector3(0f, -anchorRect.rect.height / 2f - 6f, 0f));
        Vector2 screenPoint = RectTransformUtility.WorldToScreenPoint(null, anchorWorldPoint);

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, null, out Vector2 localPoint);

        // On garde le menu entierement visible meme pres d'un bord d'ecran.
        // Pivot du menu en haut-centre (voir BuildContextMenu) : il s'etend
        // symetriquement en largeur, d'ou le demi-largeur de chaque cote.
        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 menuSize = contextMenuRect.sizeDelta;
        float minX = -canvasSize.x / 2f + menuSize.x / 2f;
        float maxX = canvasSize.x / 2f - menuSize.x / 2f;
        float minY = -canvasSize.y / 2f + menuSize.y;
        float maxY = canvasSize.y / 2f;

        localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
        localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);

        contextMenuRect.anchoredPosition = localPoint;
    }

    /// <summary>
    /// Appele par Update() des qu'un clic droit touche le cadre-portrait
    /// d'un autre joueur (voir playerTargetFrameRect) : affiche "Inviter au
    /// groupe". Le bouton (contextMenuButtonText/pendingContextMenuAction)
    /// est partage avec ShowLeaveGroupMenu -- un seul menu, dont le libelle
    /// et l'action changent selon d'ou il a ete ouvert.
    /// </summary>
    public void ShowContextMenu(Player target, RectTransform anchorRect)
    {
        if (target == null || canvasRect == null || anchorRect == null)
        {
            return;
        }

        contextMenuTarget = target;

        if (contextMenuButtonText != null)
        {
            contextMenuButtonText.text = "Inviter au groupe";
        }

        pendingContextMenuAction = InviteContextMenuTarget;

        PositionContextMenuBelow(anchorRect);

        contextMenuGO.transform.SetAsLastSibling();
        contextMenuGO.SetActive(true);
    }

    private void InviteContextMenuTarget()
    {
        if (contextMenuTarget != null && Player.MyInstance != null)
        {
            Player.MyInstance.InviteToGroup(contextMenuTarget);
        }
    }

    /// <summary>
    /// Appele par Update() des qu'un clic droit touche NOTRE PROPRE
    /// portrait (voir ownHudFrameRect) alors qu'on est dans un groupe :
    /// affiche "Quitter le groupe", au meme endroit et dans le meme style
    /// que "Inviter au groupe" (voir ShowContextMenu) -- seuls le libelle
    /// et l'action du bouton changent.
    /// </summary>
    public void ShowLeaveGroupMenu(RectTransform anchorRect)
    {
        if (canvasRect == null || anchorRect == null)
        {
            return;
        }

        // Pas de cible-joueur pour cette action (voir contextMenuTarget,
        // utilise seulement par InviteContextMenuTarget).
        contextMenuTarget = null;

        if (contextMenuButtonText != null)
        {
            contextMenuButtonText.text = "Quitter le groupe";
        }

        pendingContextMenuAction = LeaveGroupFromContextMenu;

        PositionContextMenuBelow(anchorRect);

        contextMenuGO.transform.SetAsLastSibling();
        contextMenuGO.SetActive(true);
    }

    private static void LeaveGroupFromContextMenu()
    {
        if (Player.MyInstance != null)
        {
            Player.MyInstance.LeaveGroup();
        }
    }

    public void HideContextMenu()
    {
        if (contextMenuGO != null)
        {
            contextMenuGO.SetActive(false);
        }

        contextMenuTarget = null;
        pendingContextMenuAction = null;
    }

    private void OnContextMenuButtonClicked()
    {
        pendingContextMenuAction?.Invoke();

        HideContextMenu();
    }

    /// <summary>
    /// Appele par PlayerGroupSync.TargetReceiveInvite (cote joueur invite)
    /// quand un autre joueur vient de l'inviter dans son groupe.
    /// </summary>
    public void ShowInvitePopup(string inviterName, NetworkIdentity inviterIdentity)
    {
        if (invitePopupGO == null)
        {
            return;
        }

        pendingInviterIdentity = inviterIdentity;
        invitePopupText.text = inviterName + " vous invite dans son groupe.";
        invitePopupGO.SetActive(true);
    }

    private void OnAcceptButtonClicked()
    {
        RespondToPendingInvite(true);
    }

    private void OnDeclineButtonClicked()
    {
        RespondToPendingInvite(false);
    }

    private void RespondToPendingInvite(bool accepted)
    {
        if (pendingInviterIdentity != null && Player.MyInstance != null)
        {
            Player.MyInstance.RespondToGroupInvite(pendingInviterIdentity, accepted);
        }

        pendingInviterIdentity = null;
        invitePopupGO.SetActive(false);
    }
}
