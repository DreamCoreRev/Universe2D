using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;

public delegate void HealthChanged(float health);

public delegate void CharacterRemoved();

public class Enemy : Character, IInteractable
{
    public event HealthChanged healthChanged;

    public event CharacterRemoved characterRemoved;

    /// <summary>
    /// Appele quand ce monstre meurt, avec le Character qui a porte le
    /// coup fatal -- utilise par EnemyNetworkSync pour crediter l'XP au
    /// bon joueur en reseau (voir aussi TakeDamage()).
    /// </summary>
    public event Action<Character> OnKilledBy;

    /// <summary>
    /// Vrai si CE processus doit faire tourner l'IA de ce monstre :
    /// - en solo (pas de session Mirror active), comme avant, toujours vrai ;
    /// - en reseau, seul le serveur dedie simule les monstres (partages
    ///   entre tous les joueurs connectes) -- voir EnemyNetworkSync, qui
    ///   synchronise aux clients ce qu'il faut pour l'affichage.
    /// </summary>
    public bool ShouldRunAI
    {
        get
        {
            return !NetworkClient.active || NetworkServer.active;
        }
    }

    /// <summary>
    /// A canvasgroup for the healthbar
    /// </summary>
    [SerializeField]
    private CanvasGroup healthGroup;

    /// <summary>
    /// The enemys current state
    /// </summary>
    private IState currentState;

    [SerializeField]
    private LootTable lootTable;

    [SerializeField]
    private AStar astar;

    //This is tmp for testing, later we will base damage on stats
    [SerializeField]
    protected int damage;

    private bool canDoDamage = true;

    /// <summary>
    /// The enemys attack range
    /// </summary>
    [SerializeField]
    private float attackRange;

    /// <summary>
    /// Portee d'attaque minimale appliquee meme si attackRange vaut 0 dans l'Inspector.
    /// </summary>
    [SerializeField]
    private float minAttackRange = 1.5f;


    /// <summary>
    /// How much time has passed since the last attack
    /// </summary>
    public float MyAttackTime { get; set; }

    public Vector3 MyStartPosition { get; set; }

    [SerializeField]
    private Sprite portrait;

    public Sprite MyPortrait
    {
        get
        {
            return portrait;
        }
    }

    [SerializeField]
    private float initAggroRange;

    public float MyAggroRange { get; set; }

    public bool InRange
    {
        get
        {
            // MyTarget peut pointer vers un Player deconnecte/detruit entre
            // temps (reconnexion, fermeture du client...) : MyTarget == null
            // detecte aussi ce cas (objet Unity "detruit mais reference"),
            // mais seulement si on verifie AVANT de toucher MyTarget.transform,
            // sinon ca plante en boucle et bloque l'IA du monstre pour de bon.
            if (MyTarget == null)
            {
                return false;
            }

            return Vector2.Distance(transform.position, MyTarget.transform.position) < MyAggroRange;
        }
    }

    public AStar MyAstar
    {
        get
        {
            return astar;
        }
    }

    public float MyAttackRange
    {
        get
        {
            // Plusieurs monstres de melee ont attackRange = 0 dans Demo.unity :
            // Follow/Path/AttackState testent "distance <= MyAttackRange", ce
            // qui n'est alors JAMAIS vrai -> le monstre colle le joueur sans
            // jamais attaquer, puis part en Evade des qu'il le perd. On
            // impose donc une portee minimale.
            return Mathf.Max(attackRange, minAttackRange);
        }

        set
        {
            attackRange = value;
        }
    }

    protected void Awake()
    {
        health.Initialize(initHealth, initHealth);
        SpriteRenderer sr;
        sr = GetComponent<SpriteRenderer>();
        sr.enabled = true;
        MyStartPosition = transform.position;
        MyAggroRange = initAggroRange;
        ChangeState(new IdleState());
    }

    protected override void Start()
    {
        base.Start();

        if (ShouldRunAI)
        {
            Debug.Log("[Enemy] IA active (log v2) : " + name);
        }

        MyAnimator.SetFloat("y", -1);
    }

    protected override void Update()
    {
        if (IsAlive && ShouldRunAI)
        {

            if (!IsAttacking)
            {
                MyAttackTime += Time.deltaTime;
            }

            currentState.Update();

            if (MyTarget != null && !MyTarget.IsAlive)
            {
                Evade("la cible est morte");
            }
        }

        base.Update();

    }

    /// <summary>
    /// When the enemy is selected
    /// </summary>
    /// <returns></returns>
    public Character Select()
    {
        //Shows the health bar
        healthGroup.alpha = 1;

        return this;
    }

    /// <summary>
    /// When we deselect our enemy
    /// </summary>
    public void DeSelect()
    {
        //Hides the healthbar
        healthGroup.alpha = 0;

        healthChanged -= new HealthChanged(UIManager.MyInstance.UpdateTargetFrame);

        characterRemoved -= new CharacterRemoved(UIManager.MyInstance.HideTargetFrame);
  
    }


    /// <summary>
    /// Makes the enemy take damage when hit
    /// </summary>
    /// <param name="damage"></param>
    public override void TakeDamage(float damage, Character source)
    {
        if (!(currentState is EvadeState))
        {
            if (IsAlive)
            {
                SetTarget(source);

                base.TakeDamage(damage, source);

                OnHealthChanged(health.MyCurrentValue);

                if (!IsAlive)
                {
                    source.RemoveAttacker(this);

                    if (!NetworkClient.active && !NetworkServer.active)
                    {
                        // Solo (pas de session Mirror) : comportement d'origine, inchange.
                        Player.MyInstance.GainXP(XPManager.CalculateXP((this as Enemy)));
                    }

                    OnKilledBy?.Invoke(source);
                }
            }

        }

    }

    /// <summary>
    /// Appele par un Animation Event sur le clip d'attaque. Ce clip joue
    /// visuellement sur TOUS les clients (voir EnemyNetworkSync.OnAttackingChanged),
    /// mais seul celui qui fait reellement tourner l'IA (ShouldRunAI -- solo
    /// ou serveur dedie) doit resoudre les degats, sinon CombatNetworking
    /// ferait ce travail en double (ou pour rien, sur un simple client).
    /// </summary>
    public void DoDamage()
    {
        if (!ShouldRunAI)
        {
            return;
        }

        if (canDoDamage)
        {
            CombatNetworking.DealDamage(MyTarget, damage, this);
            canDoDamage = false;
        }
      
    }

    public void CanDoDamage()
    {
        canDoDamage = true;
    }
       

    /// <summary>
    /// Changes the enemys state
    /// </summary>
    /// <param name="newState">The new state</param>
    public void ChangeState(IState newState)
    {
        if (currentState != null) //Makes sure we have a state before we call exit
        {
            if (ShouldRunAI)
            {
                Debug.Log("[Enemy] " + name + " : " + currentState.GetType().Name + " -> "
                    + newState.GetType().Name + " (cible : "
                    + (MyTarget != null ? MyTarget.name : "aucune") + ")");
            }

            currentState.Exit();
        }

        //Sets the new state
        currentState = newState;

        //Calls enter on the new state
        currentState.Enter(this);
    }

    /// <summary>
    /// Passe en fuite (retour a la position de depart). Passer par cette
    /// methode plutot que ChangeState(new EvadeState()) : elle ecrit la
    /// RAISON dans le log (server.log pour le serveur dedie), et evite de
    /// relancer EvadeState en boucle (EvadeState.Exit() soigne le monstre
    /// et lui retire sa cible a chaque sortie).
    /// </summary>
    public void Evade(string reason)
    {
        if (currentState is EvadeState)
        {
            return;
        }

        string details = "";
        if (MyTarget != null)
        {
            details = " | monstre=" + transform.position
                + " cible=" + MyTarget.transform.position
                + " parentCible=" + (MyTarget.transform.parent != null ? MyTarget.transform.parent.position.ToString() : "aucun")
                + " distance=" + Vector2.Distance(transform.position, MyTarget.transform.position).ToString("F2")
                + " aggro=" + MyAggroRange.ToString("F2");
        }

        Debug.Log("[Enemy] " + name + " prend la fuite : " + reason + details);
        ChangeState(new EvadeState());
    }

    public void SetTarget(Character target)
    {
        if (MyTarget == null && !(currentState is EvadeState))
        {
            float distance = Vector2.Distance(transform.position, target.transform.position);
            MyAggroRange = initAggroRange;
            MyAggroRange += distance;
            MyTarget = target;

            if (ShouldRunAI)
            {
                Debug.Log("[Enemy] " + name + " prend pour cible " + target.name
                    + " | monstre=" + transform.position
                    + " cible=" + target.transform.position
                    + " distance=" + distance.ToString("F2")
                    + " aggro=" + MyAggroRange.ToString("F2"));
            }
            target.AddAttacker(this);
        }
    }

    public void Reset()
    {
        this.MyTarget = null;
        this.MyAggroRange = initAggroRange;
        this.MyHealth.MyCurrentValue = this.MyHealth.MyMaxValue;
        OnHealthChanged(health.MyCurrentValue);
    }

    public void Interact()
    {
        if (!IsAlive)
        {
            List<Drop> drops = new List<Drop>();

            foreach (IInteractable interactable in Player.MyInstance.MyInteractables)
            {
                if (interactable is Enemy && !(interactable as Enemy).IsAlive)
                {
                    drops.AddRange((interactable as Enemy).lootTable.GetLoot());
                }
            }

            LootWindow.MyInstance.CreatePages(drops);

        }
    }

    public void StopInteract()
    {
        LootWindow.MyInstance.Close();
    }

    public void OnHealthChanged(float health)
    {
        if (healthChanged != null)
        {
            healthChanged(health);
        }

    }

    public void OnCharacterRemoved()
    {
        if (characterRemoved != null)
        {
            characterRemoved();
        }

        Destroy(gameObject);
    }
}
