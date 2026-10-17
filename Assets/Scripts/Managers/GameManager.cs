using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public delegate void KillConfirmed(Character character);

public class GameManager : MonoBehaviour {

    public event KillConfirmed killConfirmedEvent;

    private Camera mainCamera;

    private static GameManager instance;

    // Avant, une reference scene-wired vers l'UNIQUE Player de Demo.unity
    // (voir [SerializeField] private Player player). En reseau, Mirror
    // instancie un nouveau Player a chaque connexion -- cette reference
    // figee pointait donc vers l'ancienne instance desactivee, jamais vers
    // celui qu'on controle reellement. player.MyTarget = ... ecrivait donc
    // sur le mauvais objet : le ciblage semblait marcher (barre de vie du
    // monstre affichee par Enemy.Select(), independant de ca) mais
    // Player.MyInstance.MyTarget restait toujours null, empechant tout
    // sort necessitant une cible de partir. On utilise donc partout
    // Player.MyInstance, comme le reste du projet.

    [SerializeField]
    private LayerMask clickableLayer, groundLayer;

    private Enemy currentTarget;
    private int targetIndex;

    private HashSet<Vector3Int> blocked = new HashSet<Vector3Int>();


    public static GameManager MyInstance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<GameManager>();
            }
            return instance;
        }

    }

    public HashSet<Vector3Int> Blocked
    {
        get
        {
            return blocked;
        }

        set
        {
            blocked = value;
        }
    }

    private void Start()
    {
        mainCamera = Camera.main;
    }

    // Update is called once per frame
    void Update ()
    {
        if (ChatManager.MyInstance != null && ChatManager.MyInstance.IsTyping)
        {
            // Meme garde-fou que Player.GetInput()/UIManager.Update() :
            // Tab (NextTarget) ne doit pas agir pendant qu'on tape dans
            // le chat.
            return;
        }

        NextTarget();

        SelectNearestEnemyForGamepad();

        LootNearestForGamepad();
	}

    // En LateUpdate (pas Update) : l'EventSystem traite les nouveaux
    // doigts/touches pendant son propre Update(), donc si on vérifie
    // TouchInput.IsPointerOverUI() depuis Update() comme avant, ça peut
    // tomber sur le tout premier frame d'un nouveau doigt, avant que
    // l'EventSystem ait fini de le traiter -- et IsPointerOverGameObject()
    // répond alors "oui, sur une UI" par défaut pour ce doigt-là, même en
    // tapant en plein sur une créature à découvert. En LateUpdate, ce frame
    // est déjà résolu.
    void LateUpdate()
    {
        //Executes click target
        ClickTarget();
    }

    private void ClickTarget()
    {
        // Voir TouchInput.cs : EventSystem.IsPointerOverGameObject() sans
        // argument ne reconnaît que le pointeur souris (id -1), jamais un
        // vrai doigt tactile -- du coup sur téléphone cette vérification
        // répondait presque toujours "pas sur une UI", même en tapant sur
        // un bouton, ce qui pouvait désélectionner la cible ou (si un doigt
        // traînait ailleurs en même temps, ex: le joystick tactile) faire
        // bouger le personnage sans qu'on l'ait demandé. On utilise aussi la
        // position du doigt qui a vraiment tapé plutôt que la souris simulée,
        // qui peut se mélanger si deux doigts touchent l'écran en même temps.
        //
        // Input.GetMouseButtonDown(0) tout seul ne suffit pas non plus : sur
        // certains appareils/réglages, la simulation "souris depuis le
        // tactile" ne déclenche jamais ce bouton pour un vrai appui du
        // doigt (contrairement aux clics UI, qui passent par un autre
        // système, l'EventSystem, et marchaient déjà). D'où le fait de
        // sélectionner une créature marchait à la souris sur PC mais pas du
        // tout au doigt sur téléphone. On détecte donc aussi directement le
        // tout premier instant où le doigt touche l'écran.
        bool leftPressed = Input.touchCount > 0
            ? Input.GetTouch(0).phase == TouchPhase.Began
            : Input.GetMouseButtonDown(0);

        bool overUI = TouchInput.IsPointerOverUI();

        if (leftPressed && !overUI)//If we click the left mouse button
        {
            // Un clic gauche dans le monde ferme le menu contextuel "Inviter
            // au groupe" s'il etait ouvert (voir GroupUIManager) -- comme
            // dans la plupart des jeux, cliquer ailleurs l'annule. Sans
            // incidence si rien n'est ouvert (HideContextMenu ne fait rien
            // dans ce cas). Idem pour le portrait de cible-joueur : un clic
            // gauche ailleurs le referme, il sera re-affiche plus bas si on
            // vient justement de cliquer sur un autre joueur.
            GroupUIManager.MyInstance?.HideContextMenu();
            GroupUIManager.MyInstance?.HidePlayerTargetFrame();

            //Makes a raycast from the pointer position into the game world
            Vector3 pointerPos = TouchInput.GetPointerPosition();
            Vector3 worldPos = mainCamera.ScreenToWorldPoint(pointerPos);
            RaycastHit2D hit = Physics2D.Raycast(worldPos,Vector2.zero,Mathf.Infinity,clickableLayer);

            if (hit.collider != null && hit.collider.tag == "Enemy")//If we hit an enemy
            {
                Enemy enemy = hit.collider.GetComponent<Enemy>();

                if (enemy != null && !enemy.IsAlive)
                {
                    // Une créature morte ne se cible pas (il n'y a plus rien à
                    // attaquer) : un tap dessus la loot directement, comme le
                    // ferait un clic droit sur PC (Enemy.Interact() gère le loot
                    // quand IsAlive est faux).
                    if (Player.MyInstance.MyInteractables.Contains(enemy))
                    {
                        enemy.Interact();
                    }
                }
                else
                {
                    DeSelectTarget();

                    SelectTarget(enemy);
                }
            }
            else if (hit.collider != null && hit.collider.tag == "PlayerClickable")//If we hit another player : show their portrait next to ours (style WoW), like SelectTarget does for an Enemy
            {
                // Collider dedie, separe du collider "Player" deja utilise par
                // l'aggro des monstres (voir Range.cs) -- voir Player.prefab.
                // Le script Player/Character vit sur le GameObject parent.
                Player targetPlayer = hit.collider.GetComponentInParent<Player>();

                if (targetPlayer != null && targetPlayer != Player.MyInstance)
                {
                    // Cibler un joueur et cibler un monstre sont mutuellement
                    // exclusifs (meme emplacement d'ecran pour les deux cadres) :
                    // on efface toute cible-monstre en cours avant d'afficher le
                    // portrait du joueur clique.
                    UIManager.MyInstance.HideTargetFrame();

                    DeSelectTarget();

                    currentTarget = null;
                    Player.MyInstance.MyTarget = null;

                    GroupUIManager.MyInstance?.ShowPlayerTargetFrame(targetPlayer);
                }
            }
            else if (hit.collider != null && hit.collider.tag == "Interactable")//If we hit a neutral NPC or object, interact with it directly
            {
                // Il n'y a pas de clic droit au tactile : un simple tap sur un PNJ
                // neutre ou un objet interactable (marchand, coffre, etc.)
                // déclenche directement l'interaction, comme le ferait un clic
                // droit sur PC (voir la branche Input.GetMouseButtonDown(1) ci-dessous).
                IInteractable entity = hit.collider.gameObject.GetComponent<IInteractable>();
                if (entity != null && Player.MyInstance.MyInteractables.Contains(entity))
                {
                    entity.Interact();
                }
            }
            else//Deselect the target
            {
                UIManager.MyInstance.HideTargetFrame();

                DeSelectTarget();

                //We remove the references to the target
                currentTarget = null;
                Player.MyInstance.MyTarget = null;
            }
        }
        else if (Input.GetMouseButtonDown(1) && !TouchInput.IsPointerOverUI())
        {
            //Makes a raycast from the pointer position into the game world
            RaycastHit2D hit = Physics2D.Raycast(mainCamera.ScreenToWorldPoint(TouchInput.GetPointerPosition()), Vector2.zero, Mathf.Infinity, clickableLayer);

            // Le clic droit DIRECTEMENT sur le personnage d'un autre joueur
            // dans le monde n'ouvre plus de menu ici -- ça entrait en conflit
            // avec le clic droit sur la map (deplacement), le personnage
            // marchant vers le joueur clique au lieu d'afficher le menu.
            // "Inviter au groupe" se declenche maintenant uniquement via un
            // clic droit sur le PORTRAIT du joueur cible (voir GroupUIManager,
            // affiche par un clic GAUCHE sur le joueur -- comme sur WoW). Un
            // clic droit sur le collider "PlayerClickable" est donc traite
            // exactement comme un clic droit sur le sol (il ne fait rien :
            // le deplacement au clic a ete retire).
            if (hit.collider != null && hit.collider.tag != "PlayerClickable")
            {
                GroupUIManager.MyInstance?.HideContextMenu();

                IInteractable entity = hit.collider.gameObject.GetComponent<IInteractable>();
                if ((hit.collider.tag == "Enemy" || hit.collider.tag == "Interactable") && Player.MyInstance.MyInteractables.Contains(entity))
                {
                    entity.Interact();
                }
            }
            else
            {
                GroupUIManager.MyInstance?.HideContextMenu();
            }
        }
   
    }

    private void NextTarget()
    {
        if (Input.GetKeyDown(KeyCode.Tab))
        {
            DeSelectTarget();

            if (Player.MyInstance.Attackers.Count > 0)
            {
                if (targetIndex < Player.MyInstance.Attackers.Count)
                {
                    SelectTarget(Player.MyInstance.Attackers[targetIndex] as Enemy);
                    targetIndex++;
                    if (targetIndex >= Player.MyInstance.Attackers.Count)
                    {
                        targetIndex = 0;
                    }

                }
                else
                {
                    targetIndex = 0;
                }
        
            }
        }

    }

    // Rayon (en unites monde, meme echelle que la taille de case de la
    // Grid -- 1.3) dans lequel la manette cherche une creature a cibler :
    // assez large pour choisir une creature "proche" sans viser au pixel
    // pres (pas de souris), mais pas l'ecran entier non plus.
    private const float GamepadTargetRange = 5f;

    /// <summary>
    /// Equivalent manette du clic gauche sur une creature (voir
    /// ClickTarget() ci-dessus, qui fait un raycast sous le curseur) :
    /// cible directement la creature VIVANTE la plus proche du joueur, dans
    /// un rayon de GamepadTargetRange, sans avoir besoin d'une souris.
    /// Reutilise SelectTarget()/DeSelectTarget() ci-dessous, donc l'UI
    /// (cadre de cible, contour de selection) se comporte exactement pareil
    /// qu'un ciblage a la souris.
    /// </summary>
    private void SelectNearestEnemyForGamepad()
    {
        if (!GamepadInput.TargetPressed)
        {
            return;
        }

        Collider2D[] hits = Physics2D.OverlapCircleAll(Player.MyInstance.transform.position, GamepadTargetRange, clickableLayer);

        Enemy nearest = null;
        float nearestDistanceSqr = float.MaxValue;

        foreach (Collider2D hit in hits)
        {
            if (hit.tag != "Enemy")
            {
                continue;
            }

            Enemy enemy = hit.GetComponent<Enemy>();
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            float distanceSqr = (enemy.transform.position - Player.MyInstance.transform.position).sqrMagnitude;
            if (distanceSqr < nearestDistanceSqr)
            {
                nearestDistanceSqr = distanceSqr;
                nearest = enemy;
            }
        }

        DeSelectTarget();

        if (nearest != null)
        {
            SelectTarget(nearest);
        }
        else
        {
            // Rien a portee : comme un clic gauche dans le vide, on efface
            // la cible en cours plutot que de ne rien faire.
            UIManager.MyInstance.HideTargetFrame();
            currentTarget = null;
            Player.MyInstance.MyTarget = null;
        }
    }

    /// <summary>
    /// Equivalent manette du clic gauche/droit sur une creature morte (voir
    /// ClickTarget() plus haut) : loote directement n'importe quelle
    /// creature morte a proximite (Player.MyInstance.MyInteractables,
    /// rempli par Player.OnTriggerEnter2D/Exit2D des qu'on marche a cote
    /// d'un cadavre), sans avoir besoin de viser avec une souris.
    /// Enemy.Interact() regroupe de toute facon le butin de TOUTES les
    /// creatures mortes a proximite en une seule fois (voir Enemy.cs), donc
    /// peu importe laquelle des creatures mortes on appelle ici.
    /// </summary>
    private void LootNearestForGamepad()
    {
        if (!GamepadInput.LootPressed)
        {
            return;
        }

        if (LootWindow.MyInstance.IsOpen)
        {
            // La fenetre est deja ouverte (meme bouton presse une premiere
            // fois juste avant) : Back/View ne fait plus rien ici -- une
            // fois ouverte, la fenetre se pilote avec le stick gauche
            // (deplacer la selection), A (prendre l'objet selectionne) et
            // B (fermer), voir LootWindow.UpdateGamepadNavigation().
            return;
        }

        foreach (IInteractable interactable in Player.MyInstance.MyInteractables)
        {
            if (interactable is Enemy && !(interactable as Enemy).IsAlive)
            {
                interactable.Interact();
                return;
            }
        }
    }

    private void DeSelectTarget()
    {
        if (currentTarget != null)
        {
            currentTarget.DeSelect();
        }

    }

    private void SelectTarget(Enemy enemy)
    {
        currentTarget = enemy;
        Player.MyInstance.MyTarget = currentTarget.Select();
        UIManager.MyInstance.ShowTargetFrame(currentTarget);


    }

    public void OnKillConfirmed(Character character)
    {
        if (killConfirmedEvent !=null)
        {
            killConfirmedEvent(character);
        }
    }
}
