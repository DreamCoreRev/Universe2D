using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

public delegate void KillConfirmed(Character character);

public class GameManager : MonoBehaviour {

    public event KillConfirmed killConfirmedEvent;

    private Camera mainCamera;

    private static GameManager instance;

    /// <summary>
    /// A reference to the player object
    /// </summary>
    [SerializeField]
    private Player player;

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
        //Executes click target
        ClickTarget();

        NextTarget();
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

        if (leftPressed && !TouchInput.IsPointerOverUI())//If we click the left mouse button
        {
            //Makes a raycast from the pointer position into the game world
            RaycastHit2D hit = Physics2D.Raycast(mainCamera.ScreenToWorldPoint(TouchInput.GetPointerPosition()),Vector2.zero,Mathf.Infinity,512);

            if (hit.collider != null && hit.collider.tag == "Enemy")//If we hit something
            {
                DeSelectTarget();

                SelectTarget(hit.collider.GetComponent<Enemy>());
            }
            else//Deselect the target
            {
                UIManager.MyInstance.HideTargetFrame();

                DeSelectTarget();

                //We remove the references to the target
                currentTarget = null;
                player.MyTarget = null;
            }
        }
        else if (Input.GetMouseButtonDown(1) && !TouchInput.IsPointerOverUI())
        {
            //Makes a raycast from the pointer position into the game world
            RaycastHit2D hit = Physics2D.Raycast(mainCamera.ScreenToWorldPoint(TouchInput.GetPointerPosition()), Vector2.zero, Mathf.Infinity, clickableLayer);

            if (hit.collider != null)
            {
                IInteractable entity = hit.collider.gameObject.GetComponent<IInteractable>();
                if (hit.collider != null && (hit.collider.tag == "Enemy" || hit.collider.tag == "Interactable") && player.MyInteractables.Contains(entity))
                {
                    entity.Interact();
                }
            }
            else
            {
                hit = Physics2D.Raycast(mainCamera.ScreenToWorldPoint(TouchInput.GetPointerPosition()), Vector2.zero, Mathf.Infinity, groundLayer);

                if (hit.collider != null)
                {
                    player.GetPath(mainCamera.ScreenToWorldPoint(TouchInput.GetPointerPosition()));
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
        player.MyTarget = currentTarget.Select();
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
