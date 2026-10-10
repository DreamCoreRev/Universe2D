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
            // exactement comme un clic droit sur le sol : ça deplace le
            // personnage vers ce point, comme n'importe quel autre terrain.
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

                hit = Physics2D.Raycast(mainCamera.ScreenToWorldPoint(TouchInput.GetPointerPosition()), Vector2.zero, Mathf.Infinity, groundLayer);

                if (hit.collider != null)
                {
                    Player.MyInstance.GetPath(mainCamera.ScreenToWorldPoint(TouchInput.GetPointerPosition()));
                }
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
