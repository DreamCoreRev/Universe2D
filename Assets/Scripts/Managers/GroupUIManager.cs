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
    private bool sceneUIBuilt;

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
        // BUG trouve le 10/10 : l'ancre doit etre au CENTRE du canvas
        // (0.5, 0.5), pas a son coin (0, 1). RectTransformUtility.
        // ScreenPointToLocalPointInRectangle (voir ShowContextMenu) donne
        // un point relatif au CENTRE du canvas -- avec une ancre au coin,
        // anchoredPosition s'additionnait a un point deja decale au coin
        // superieur gauche, donc le menu restait colle pres du portrait
        // (coin superieur gauche de l'ecran) quel que soit l'endroit
        // reellement clique. Le pivot (0, 1), lui, reste au coin
        // superieur gauche DU MENU -- c'est ce qui fait que le menu
        // s'etend vers la droite et le bas a partir du point clique,
        // comme un vrai menu contextuel.
        contextMenuRect.anchorMin = new Vector2(0.5f, 0.5f);
        contextMenuRect.anchorMax = new Vector2(0.5f, 0.5f);
        contextMenuRect.pivot = new Vector2(0f, 1f);
        contextMenuRect.sizeDelta = new Vector2(150f, 34f);

        Image bg = contextMenuGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);

        GameObject buttonGO = BuildButton(contextMenuRect, "Inviter au groupe", Vector2.zero, new Vector2(150f, 34f), new Color(1f, 1f, 1f, 0.12f));
        Button button = buttonGO.GetComponent<Button>();
        button.onClick.AddListener(OnInviteButtonClicked);

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
        rect.sizeDelta = new Vector2(260f, 110f);
        rect.anchoredPosition = new Vector2(0f, 80f);

        Image bg = invitePopupGO.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.85f);

        GameObject textGO = new GameObject("Text", typeof(RectTransform));
        textGO.transform.SetParent(invitePopupGO.transform, false);
        RectTransform textRect = textGO.GetComponent<RectTransform>();
        textRect.anchorMin = new Vector2(0f, 1f);
        textRect.anchorMax = new Vector2(1f, 1f);
        textRect.pivot = new Vector2(0.5f, 1f);
        textRect.anchoredPosition = new Vector2(0f, -8f);
        textRect.sizeDelta = new Vector2(-16f, 56f);

        invitePopupText = textGO.AddComponent<Text>();
        invitePopupText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        invitePopupText.fontSize = 14;
        invitePopupText.color = Color.white;
        invitePopupText.alignment = TextAnchor.UpperCenter;
        invitePopupText.horizontalOverflow = HorizontalWrapMode.Wrap;
        invitePopupText.verticalOverflow = VerticalWrapMode.Overflow;
        invitePopupText.text = "";

        GameObject acceptGO = BuildButton(invitePopupGO.GetComponent<RectTransform>(), "Accepter", new Vector2(-62f, 12f), new Vector2(110f, 30f), new Color(0.25f, 0.65f, 0.25f, 0.9f));
        acceptGO.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0f);
        acceptGO.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0f);
        acceptGO.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
        acceptGO.GetComponent<Button>().onClick.AddListener(OnAcceptButtonClicked);

        GameObject declineGO = BuildButton(invitePopupGO.GetComponent<RectTransform>(), "Refuser", new Vector2(62f, 12f), new Vector2(110f, 30f), new Color(0.65f, 0.25f, 0.25f, 0.9f));
        declineGO.GetComponent<RectTransform>().anchorMin = new Vector2(0.5f, 0f);
        declineGO.GetComponent<RectTransform>().anchorMax = new Vector2(0.5f, 0f);
        declineGO.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0f);
        declineGO.GetComponent<Button>().onClick.AddListener(OnDeclineButtonClicked);

        invitePopupGO.SetActive(false);
    }

    /// <summary>
    /// Portrait de la cible-joueur (style WoW) : affiche au clic GAUCHE sur
    /// un autre joueur (voir GameManager.ClickTarget -- branche
    /// "PlayerClickable"). Meme emplacement ecran que l'ancien TargetFrame
    /// des monstres dans Demo.unity (ancre coin haut-gauche, {376.7,
    /// -30.6}) : les deux cadres sont mutuellement exclusifs (un seul
    /// affiche a la fois), donc partager la meme case ecran est coherent
    /// visuellement.
    ///
    /// Pour que ca ressemble vraiment a notre propre cadre (meme portrait,
    /// meme cadre rond, memes barres vie/mana) sans avoir a recopier a la
    /// main des guids de sprites qu'on ne peut pas resoudre depuis du code
    /// pur a l'execution (pas d'AssetDatabase en dehors de l'Editeur), on
    /// CLONE a l'execution le "Frame" deja present dans Demo.unity (voir
    /// Player.ResolveLocalReferences -- c'est exactement l'objet que
    /// "UICanvas/Frame/HealthBackground/Health" etc. designent). Le clone
    /// recupere donc automatiquement le meme portrait, le meme cadre rond,
    /// et des barres de vie/mana en etat de marche (composants Stat
    /// inclus) -- il ne reste plus qu'a leur donner les bonnes valeurs
    /// (voir RefreshPlayerTargetVitals) et a retirer la barre d'XP, qui n'a
    /// pas de sens pour une cible.
    ///
    /// Le clic DROIT sur ce portrait (et uniquement sur lui) reaffiche le
    /// menu "Inviter au groupe" deja existant (voir Update() plus bas) --
    /// c'est ce qui remplace l'ancien clic droit sur le personnage dans le
    /// monde, qui entrait en conflit avec le deplacement.
    /// </summary>
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

        // La barre d'XP n'a de sens que pour nous-memes (comme sur WoW, ni
        // la cible ni les membres du groupe n'affichent leur XP).
        Transform xpBackground = clone.transform.Find("XPBackground");
        if (xpBackground != null)
        {
            Destroy(xpBackground.gameObject);
        }

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
    /// Petite etiquette avec un nom de joueur, au-dessus d'un cadre clone
    /// (le HUD d'origine n'affiche jamais notre propre nom, il n'a donc pas
    /// cet element -- on l'ajoute nous-memes). Reutilise par
    /// BuildPlayerTargetFrame et BuildPartyFrames.
    /// </summary>
    private static Text BuildNameLabel(Transform parent, Vector2 anchoredPosition)
    {
        GameObject nameGO = new GameObject("NameLabel", typeof(RectTransform));
        nameGO.transform.SetParent(parent, false);
        RectTransform nameRect = nameGO.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0.5f, 1f);
        nameRect.anchorMax = new Vector2(0.5f, 1f);
        nameRect.pivot = new Vector2(0.5f, 0f);
        nameRect.sizeDelta = new Vector2(170f, 20f);
        nameRect.anchoredPosition = anchoredPosition;

        Image nameBg = nameGO.AddComponent<Image>();
        nameBg.color = new Color(0f, 0f, 0f, 0.75f);
        // m_RaycastTarget reste a true (valeur par defaut d'Image) : c'est ce
        // qui fait que survoler ce portrait compte comme "sur de l'UI" pour
        // TouchInput.IsPointerOverUI(), empechant GameManager de traiter en
        // plus un clic droit ici comme un clic droit sur la map.

        GameObject nameTextGO = new GameObject("Text", typeof(RectTransform));
        nameTextGO.transform.SetParent(nameGO.transform, false);
        RectTransform nameTextRect = nameTextGO.GetComponent<RectTransform>();
        nameTextRect.anchorMin = Vector2.zero;
        nameTextRect.anchorMax = Vector2.one;
        nameTextRect.offsetMin = new Vector2(4f, 1f);
        nameTextRect.offsetMax = new Vector2(-4f, -1f);

        Text nameText = nameTextGO.AddComponent<Text>();
        nameText.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        nameText.fontSize = 13;
        nameText.color = Color.white;
        nameText.alignment = TextAnchor.MiddleCenter;
        nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
        nameText.verticalOverflow = VerticalWrapMode.Overflow;
        nameText.raycastTarget = false;
        nameText.text = "";

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

        // 55.5 ~ centre visuel du cadre + barres (le portrait est a gauche,
        // les barres s'etendent vers la droite -- voir HealthBackground/
        // ManaBackground dans Demo.unity), 4 ~ petit espace au-dessus.
        playerTargetNameText = BuildNameLabel(playerTargetFrameGO.transform, new Vector2(55.5f, 4f));

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
                NameText = BuildNameLabel(slotGO.transform, new Vector2(55.5f, 4f)),
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
    /// jour les cadres de groupe en continu, et reste le seul point
    /// d'entree pour "Inviter au groupe" depuis le monde : un clic droit
    /// sur le portrait de la cible-joueur (et non plus sur son personnage)
    /// reaffiche le menu contextuel deja existant (ShowContextMenu), seul
    /// l'endroit d'ou il est declenche a change.
    /// </summary>
    private void Update()
    {
        if (!sceneUIBuilt)
        {
            TryBuildSceneDependentUI();
        }

        RefreshPartyFrames();

        if (currentPlayerTarget == null || playerTargetFrameGO == null || !playerTargetFrameGO.activeSelf)
        {
            return;
        }

        RefreshPlayerTargetVitals(false);

        if (!Input.GetMouseButtonDown(1))
        {
            return;
        }

        Vector3 pointerPos = TouchInput.GetPointerPosition();
        if (RectTransformUtility.RectangleContainsScreenPoint(playerTargetFrameRect, pointerPos, null))
        {
            ShowContextMenu(currentPlayerTarget, pointerPos);
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
    /// Appele par GameManager des qu'un clic droit touche le collider
    /// "PlayerClickable" d'un autre joueur (voir Player.prefab -- un enfant
    /// dedie sur le layer Clickable, separe du collider "Player" deja
    /// utilise par l'aggro des monstres (voir Range.cs) et le ciblage de
    /// combat, pour ne jamais interferer avec eux).
    /// </summary>
    public void ShowContextMenu(Player target, Vector3 screenPosition)
    {
        if (target == null || canvasRect == null)
        {
            return;
        }

        contextMenuTarget = target;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPosition, null, out Vector2 localPoint);

        // On garde le menu entierement visible meme si on clique pres d'un bord d'ecran.
        Vector2 canvasSize = canvasRect.rect.size;
        Vector2 menuSize = contextMenuRect.sizeDelta;
        float minX = -canvasSize.x / 2f;
        float maxX = canvasSize.x / 2f - menuSize.x;
        float minY = -canvasSize.y / 2f + menuSize.y;
        float maxY = canvasSize.y / 2f;

        localPoint.x = Mathf.Clamp(localPoint.x, minX, maxX);
        localPoint.y = Mathf.Clamp(localPoint.y, minY, maxY);

        contextMenuRect.anchoredPosition = localPoint;
        contextMenuGO.SetActive(true);
    }

    public void HideContextMenu()
    {
        if (contextMenuGO != null)
        {
            contextMenuGO.SetActive(false);
        }

        contextMenuTarget = null;
    }

    private void OnInviteButtonClicked()
    {
        if (contextMenuTarget != null && Player.MyInstance != null)
        {
            Player.MyInstance.InviteToGroup(contextMenuTarget);
        }

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
