using System;
using System.Collections;
using System.Collections.Generic;
using Mirror;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// This is the player script, it contains functionality that is specific to the Player
/// </summary>
public class Player : Character
{
    private static Player instance;

    public static Player MyInstance
    {
        get
        {
            if (instance == null)
            {
                instance = FindObjectOfType<Player>();
            }

            return instance;
        }
    }

    private NetworkIdentity netIdentity;

    private PlayerEquipmentSync equipmentSync;

    private PlayerCombatSync combatSync;

    private float debugWrapperLogTimer;

    /// <summary>
    /// Vrai si c'est NOUS qui controlons ce Player (clavier/joystick/UI) :
    /// - en solo (pas de session Mirror active, ex: Play direct sur Demo.unity
    ///   sans passer par le login), on se comporte exactement comme avant ;
    /// - en reseau, seul le Player possede par notre connexion l'est.
    /// L'avatar d'un autre joueur connecte ne doit JAMAIS lire nos touches
    /// ni toucher a notre UI (minimap, barres de vie, camera...).
    /// </summary>
    public bool IsLocallyControlled
    {
        get
        {
            if (netIdentity == null)
            {
                netIdentity = GetComponent<NetworkIdentity>();
            }

            // Solo veritable (aucune session Mirror, ni client ni serveur) :
            // comportement d'origine, inchange.
            if (netIdentity == null || (!NetworkClient.active && !NetworkServer.active))
            {
                return true;
            }

            // Serveur dedie (RPG.exe -batchmode -nographics) : NetworkServer.active
            // est vrai mais NetworkClient.active est faux puisqu'il ne pilote
            // lui-meme aucun personnage. L'ancienne condition (!NetworkClient.active)
            // le faisait pourtant traiter CHAQUE joueur connecte comme "le notre" --
            // lecture clavier/souris pour rien (inoffensif), mais surtout
            // Player.instance ecrase au hasard et le PlayerParent (voir
            // EnsurePlayerParent/Update) jamais garde a jour pour personne,
            // ce qui empechait les monstres de jamais estimer correctement
            // leur distance a un joueur et donc de jamais attaquer.
            if (!NetworkClient.active)
            {
                return false;
            }

            return netIdentity.isLocalPlayer;
        }
    }

 
    #region STATS


    /// <summary>
    /// The player's mana
    /// </summary>
    [SerializeField]
    private Stat mana;

    /// <summary>
    /// The player's xpStat
    /// </summary>
    [SerializeField]
    private Stat xpStat;

    private int intellect;

    private int stamina;

    private int strength;

    private int intellectMultiplier = 15;

    #endregion

    /// <summary>
    /// The level text
    /// </summary>
    [SerializeField]
    private Text levelText;

    /// <summary>
    /// The player's initial mana
    /// </summary>
    private float initMana = 50;

    private Vector2 initPos;

    [SerializeField]
    private SpriteRenderer[] gearRenderers;

    /// <summary>
    /// An array of blocks used for blocking the player's sight
    /// </summary>
    [SerializeField]
    private Block[] blocks;

    /// <summary>
    /// Exit points for the spells
    /// </summary>
    [SerializeField]
    private Transform[] exitPoints;

    [SerializeField]
    private Animator ding;

    [SerializeField]
    private Transform minimapIcon;

    [SerializeField]
    private Camera mainCam;

    /// <summary>
    /// Index that keeps track of which exit point to use, 2 is default down
    /// </summary>
    private int exitIndex = 2;

    public Coroutine MyInitRoutine { get; set; }

    private List<IInteractable> interactables = new List<IInteractable>();

    #region PATHFINDING

    private Vector3 destination;

    private Vector3 current;

    private Vector3 goal;

    [SerializeField]
    private AStar astar;

    #endregion

    private Vector3 min, max;

    [SerializeField]
    private GearSocket[] gearSockets;

    [SerializeField]
    private Profession profession;

    private GameObject unusedSpell;

    private Spell aoeSpell;

    public int MyGold { get; set; }

    public bool InCombat { get; set; } = false;

    public List<IInteractable> MyInteractables
    {
        get
        {
            return interactables;
        }

        set
        {
            interactables = value;
        }
    }

    public Stat MyXp
    {
        get
        {
            return xpStat;
        }

        set
        {
            xpStat = value;
        }
    }

    public Stat MyMana
    {
        get
        {
            return mana;
        }

        set
        {
            mana = value;
        }
    }

    protected override void Start()
    {
        base.Start();

        EnsurePlayerParent();

        if (IsLocallyControlled)
        {
            instance = this;
            ResolveLocalReferences();
            StartCoroutine(Regen());

            // En reseau, SaveManager.Start() s'est deja execute avant que ce
            // Player existe (voir SaveManager.cs) -- on le rappelle ici pour
            // qu'il initialise nos stats maintenant qu'on existe vraiment.
            // Sans ca la vie reste a 0 et le personnage est considere mort
            // des le depart (IsAlive == false).
            SaveManager saveManager = FindObjectOfType<SaveManager>();
            if (saveManager != null)
            {
                saveManager.TryInitializePlayer();
            }

            // Meme probleme que SaveManager : CameraFollow.Start() s'execute
            // avant que ce Player (reseau) existe, donc sa camera ne nous
            // suit jamais -- on la branche nous-meme une fois pret.
            // Important : il y a DEUX CameraFollow dans Demo.unity (Main
            // Camera pour l'ecran + MinimapCamera pour la minimap) --
            // FindObjectOfType<T>() n'en renvoie qu'une seule au hasard, il
            // faut donc les initialiser TOUTES les deux avec ce Player.
            CameraFollow[] cameraFollows = FindObjectsOfType<CameraFollow>();
            foreach (CameraFollow cameraFollow in cameraFollows)
            {
                cameraFollow.Initialize(this);
            }
        }
    }

    /// <summary>
    /// ClickToMove/SetDefaultValues/Respawn deplacent transform.parent, pas
    /// ce transform directement -- en solo ce parent ("PlayerParent") existe
    /// deja dans la scene. Un Player cree dynamiquement par Mirror n'a pas
    /// de parent du tout : on s'en recree un identique ici (meme config que
    /// PlayerParent dans Demo.unity) pour que le reste du script marche sans
    /// aucun changement, que ce soit notre joueur ou celui d'un autre.
    /// </summary>
    private void EnsurePlayerParent()
    {
        if (transform.parent != null)
        {
            return;
        }

        GameObject parentGO = new GameObject("PlayerParent");
        parentGO.transform.position = transform.position;

        Rigidbody2D parentRb = parentGO.AddComponent<Rigidbody2D>();
        parentRb.gravityScale = 0;
        parentRb.constraints = RigidbodyConstraints2D.FreezeRotation;

        // Le prefab a son propre Rigidbody2D (visible dans l'editeur de
        // prefab), mais la scene solo le retire et utilise a la place celui
        // du parent -- sinon on a 2 corps physiques empiles qui se genent.
        // On reproduit ca ici.
        Rigidbody2D ownRb = GetComponent<Rigidbody2D>();
        if (ownRb != null)
        {
            Destroy(ownRb);
        }

        SetRigidbody(parentRb);

        transform.SetParent(parentGO.transform, true);
    }

    /// <summary>
    /// mainCam/astar/minimapIcon/levelText/ding/profession sont branches a
    /// la main dans Demo.unity sur l'UNIQUE Player de la scene -- un Player
    /// cree dynamiquement par Mirror (nous y compris, une fois connecte en
    /// reseau) n'a aucune de ces references. On les retrouve nous-memes ici.
    /// Seulement pour le joueur qu'on controle : l'UI (minimap, texte de
    /// niveau...) est la notre, pas celle d'un autre joueur connecte.
    /// </summary>
    private void ResolveLocalReferences()
    {
        if (mainCam == null)
        {
            mainCam = Camera.main;
        }

        if (astar == null)
        {
            astar = FindObjectOfType<AStar>();
        }

        if (minimapIcon == null)
        {
            GameObject icon = GameObject.Find("MinimapIcon");
            if (icon != null)
            {
                minimapIcon = icon.transform;
            }
        }

        if (levelText == null)
        {
            GameObject text = GameObject.Find("UICanvas/Frame/LevelFrame/Text");
            if (text != null)
            {
                levelText = text.GetComponent<Text>();
            }
        }

        if (ding == null)
        {
            GameObject dingGO = GameObject.Find("Ding");
            if (dingGO != null)
            {
                ding = dingGO.GetComponent<Animator>();
            }
        }

        if (profession == null)
        {
            profession = FindObjectOfType<Profession>();
        }

        if (health == null)
        {
            GameObject hp = GameObject.Find("UICanvas/Frame/HealthBackground/Health");
            if (hp != null)
            {
                health = hp.GetComponent<Stat>();
            }
        }

        if (mana == null)
        {
            GameObject mp = GameObject.Find("UICanvas/Frame/ManaBackground/Mana");
            if (mp != null)
            {
                mana = mp.GetComponent<Stat>();
            }
        }

        if (xpStat == null)
        {
            GameObject xp = GameObject.Find("UICanvas/Frame/XPBackground/XP");
            if (xp != null)
            {
                xpStat = xp.GetComponent<Stat>();
            }
        }
    }

    /// <summary>
    /// We are overriding the characters update function, so that we can execute our own functions
    /// </summary>
    protected override void Update()
    {
        // Tout ce bloc (clavier/joystick/clic, limites de la map, ciblage
        // de sort AOE) ne doit s'executer QUE pour le joueur qu'on controle
        // reellement -- voir IsLocallyControlled. L'avatar d'un autre
        // joueur connecte continue de s'animer (base.Update() plus bas)
        // mais ne lit jamais nos entrees ni ne touche a notre UI.
        if (IsLocallyControlled)
        {
            //Executes the GetInput function
            GetInput();
            ClickToMove();

            //Clamps the player inside the tilemap
            transform.position = new Vector3(Mathf.Clamp(transform.position.x, min.x, max.x),
                Mathf.Clamp(transform.position.y, min.y, max.y),
                transform.position.z);

            if (unusedSpell != null)
            {
                Vector3 mouseScreenPostion = mainCam.ScreenToWorldPoint(Input.mousePosition);
                unusedSpell.transform.position = new Vector3(mouseScreenPostion.x, mouseScreenPostion.y, 0);

                float distance = Vector2.Distance(transform.position, mainCam.ScreenToWorldPoint(Input.mousePosition));

                if (distance >= aoeSpell.MyRange)
                {
                    unusedSpell.GetComponent<AOESpell>().OutOfRange();
                }
                else
                {
                    unusedSpell.GetComponent<AOESpell>().InRange();
                }

                if (Input.GetMouseButtonDown(0) && distance <= aoeSpell.MyRange)
                {
                    AOESpell s = Instantiate(aoeSpell.MySpellPrefab, unusedSpell.transform.position, Quaternion.identity).GetComponent<AOESpell>();
                    Vector3 castPosition = s.transform.position;
                    Destroy(unusedSpell);
                    unusedSpell = null;
                    s.Source = this;
                    s.Initialize(aoeSpell.MyDamage, aoeSpell.MyDuration);
                    mana.MyCurrentValue -= aoeSpell.ManaCost;
                    StartCoroutine(SpellBook.MyInstance.CastCooldown(aoeSpell));
                    BroadcastAOESpellCast(aoeSpell, castPosition);
                }
            }
        }
        else
        {
            debugWrapperLogTimer += Time.deltaTime;
            if (debugWrapperLogTimer >= 2f)
            {
                debugWrapperLogTimer = 0f;
                Debug.Log($"[DEBUG-PLAYERPARENT] {name} hasParent={transform.parent != null} pos={transform.position} parentPos={(transform.parent != null ? transform.parent.position.ToString() : "N/A")}");
            }
        }

        if (!IsLocallyControlled && transform.parent != null)
        {
            // Ce Player n'est pas le notre (voir IsLocallyControlled) : rien
            // ne fait donc jamais avancer son PlayerParent (voir
            // EnsurePlayerParent), qui reste fige a sa position de spawn.
            // Son propre transform, lui, est a jour (synchronise par
            // NetworkTransform). Resultat concret sans ce correctif : les
            // monstres, qui mesurent toutes leurs distances via
            // transform.parent.position (voir FollowState/PathState/
            // AttackState -- meme convention que pour eux-memes, voir
            // Enemy/EnemyParent), ne voyaient jamais ce joueur se rapprocher
            // et n'atteignaient donc jamais leur portee d'attaque. On garde
            // le wrapper colle a la vraie position a chaque frame.
            transform.parent.position = transform.position;
        }

        base.Update();
    }

    public void SetDefaultValues()
    {
        MyGold = 1000;
        stamina = 50;
        intellect = 10;
        strength = 0;
        ResetStats();
        MyXp.Initialize(0, Mathf.Floor(100 * MyLevel * Mathf.Pow(MyLevel, 0.5f)));
        if (levelText != null)
        {
            levelText.text = MyLevel.ToString();
        }
        initPos = transform.parent.position;
        UIManager.MyInstance.UpdateStatsText(intellect, stamina, strength);
    }

    private void ResetStats()
    {
        MyHealth.Initialize(stamina*StaminaMultiplier(), stamina*StaminaMultiplier());
        MyMana.Initialize(intellect * intellectMultiplier, intellect * intellectMultiplier);
    }

    private void UpdateMaxStats()
    {
        MyHealth.SetMaxValue(stamina * StaminaMultiplier());
        MyMana.SetMaxValue(intellect * intellectMultiplier);
    }

    private int StaminaMultiplier()
    {
        if (MyLevel < 10)
        {
            return 1;
        }
        else if (MyLevel > 10)
        {
            return 2;
        }

        return 3;
    }

    /// <summary>
    /// Listen's to the players input
    /// </summary>
    private void GetInput()
    {
        Direction = Vector2.zero;

        ///THIS IS USED FOR DEBUGGING ONLY
        if (Input.GetKeyDown(KeyCode.KeypadMinus))
        {
            health.MyCurrentValue -= 10;
            MyMana.MyCurrentValue -= 10;
        }
        if (Input.GetKeyDown(KeyCode.X))
        {
            GainXP(600);
        }
        if (Input.GetKeyDown(KeyCode.KeypadPlus))
        {
            health.MyCurrentValue += 10;
            MyMana.MyCurrentValue += 10;
        }

        if (Input.GetKey(KeybindManager.MyInstance.Keybinds["UP"])) //Moves up
        {
            exitIndex = 0;
            Direction += Vector2.up;
            if (minimapIcon != null) { minimapIcon.eulerAngles = new Vector3(0, 0, 0); }
        }
        if (Input.GetKey(KeybindManager.MyInstance.Keybinds["LEFT"])) //Moves left
        {
            exitIndex = 3;
            Direction += Vector2.left;
            if (Direction.y == 0)
            {
                if (minimapIcon != null) { minimapIcon.eulerAngles = new Vector3(0, 0, 90); }
            }

        }
        if (Input.GetKey(KeybindManager.MyInstance.Keybinds["DOWN"]))
        {
            exitIndex = 2;
            Direction += Vector2.down;

            if (minimapIcon != null) { minimapIcon.eulerAngles = new Vector3(0, 0, 180); }
        }
        if (Input.GetKey(KeybindManager.MyInstance.Keybinds["RIGHT"])) //Moves right
        {
            exitIndex = 1;
            Direction += Vector2.right;
            if (Direction.y == 0)
            {
                if (minimapIcon != null) { minimapIcon.eulerAngles = new Vector3(0, 0, 270); }
            }

        }
        // Joystick tactile (TouchJoystick.cs) : s'ajoute à la direction du
        // clavier au lieu de la remplacer, donc le clavier marche toujours
        // pareil sur PC, et le joystick marche en plus sur téléphone/tablette
        // (ou à la souris dans l'éditeur). Vector2.zero quand rien n'est
        // touché, donc ça ne change rien quand on n'y touche pas.
        Direction += TouchJoystick.Direction;

        if (IsMoving)
        {
            StopAction();
            StopInit();
        }

        foreach (string action in KeybindManager.MyInstance.ActionBinds.Keys)
        {
            if (Input.GetKeyDown(KeybindManager.MyInstance.ActionBinds[action]))
            {
                UIManager.MyInstance.ClickActionButton(action);

            }
        }


    }

    /// <summary>
    /// Set's the player's limits so that he can't leave the game world
    /// </summary>
    /// <param name="min">The minimum position of the player</param>
    /// <param name="max">The maximum postion of the player</param>
    public void SetLimits(Vector3 min, Vector3 max)
    {
        this.min = min;
        this.max = max;
    }

    /// <summary>
    /// Expose un exitPoint (prive) pour PlayerCombatSync, qui a besoin de
    /// rejouer le meme point de sortie que le lanceur chez les autres clients.
    /// </summary>
    public Transform GetExitPoint(int index)
    {
        if (exitPoints == null || index < 0 || index >= exitPoints.Length)
        {
            return null;
        }

        return exitPoints[index];
    }

    /// <summary>
    /// A co routine for attacking
    /// </summary>
    /// <returns></returns>
    private IEnumerator AttackRoutine(ICastable castable)
    {
        Transform currentTarget = MyTarget.MyHitbox;

        yield return actionRoutine = StartCoroutine(ActionRoutine(castable));

        if (currentTarget != null && InLineOfSight())
        {
            Spell newSpell = SpellBook.MyInstance.GetSpell(castable.MyTitle);

            SpellScript s = Instantiate(newSpell.MySpellPrefab, exitPoints[exitIndex].position, Quaternion.identity).GetComponent<SpellScript>();

            s.Initialize(currentTarget, newSpell.MyDamage, this,newSpell.MyDebuff);

            mana.MyCurrentValue -= newSpell.ManaCost;

            BroadcastTargetedSpellCast(newSpell, exitIndex);
        }

        StopAction(); //Ends the attack
    }

    private IEnumerator GatherRoutine(ICastable castable, List<Drop> items)
    {
        yield return actionRoutine = StartCoroutine(ActionRoutine(castable));//This is a hardcoded cast time, for debugging

        LootWindow.MyInstance.CreatePages(items);
    }

    public IEnumerator CraftRoutine(ICastable castable)
    {
        yield return actionRoutine = StartCoroutine(ActionRoutine(castable));

        profession.AdddItemsToInventory();
    }


    private IEnumerator ActionRoutine(ICastable castable)
    {
        SpellBook.MyInstance.Cast(castable);

        IsAttacking = true; //Indicates if we are attacking

        MyAnimator.SetBool("attack", IsAttacking); //Starts the attack animation

        foreach (GearSocket g in gearSockets)
        {
            g.MyAnimator.SetBool("attack", IsAttacking);
        }

        yield return new WaitForSeconds(castable.MyCastTime);

        StopAction();

    }

    /// <summary>
    /// Casts a spell
    /// </summary>
    public void CastSpell(Spell spell)
    {
        if (spell.OnCooldown)
        {
            return;
        }

        // exitIndex (la direction "face à") ne vient normalement que des
        // touches/du joystick tactile (voir GetInput()). Block() s'en sert
        // pour activer 2 des 4 colliders "Blocks" du joueur et bloquer la
        // ligne de vue DERRIERE lui -- ça empêche de lancer un sort sur une
        // cible qu'on ne "regarde" pas. Sur PC ça passait inaperçu car on
        // marche généralement vers ce qu'on attaque avant de lancer un
        // sort, donc exitIndex pointait déjà au bon endroit. Sur mobile, on
        // sélectionne la cible d'un tap sans bouger, donc exitIndex restait
        // sur une ancienne direction et le mur anti-dos-tourné se
        // retrouvait entre le joueur et la cible. On réoriente donc le
        // joueur vers sa cible juste avant d'activer les blocks.
        if (MyTarget != null)
        {
            exitIndex = GetExitIndexTowards(MyTarget.transform.position);
        }

        Block();

        if (spell.ManaCost > mana.MyCurrentValue)
        {
            return;
        }

        if (!spell.NeedsTarget && unusedSpell == null)
        {
            // Utilise la position du doigt/souris réelle (voir TouchInput.cs) plutot
            // que Input.mousePosition, qui n'est pas fiable pour un vrai tactile.
            Vector3 castPos = Camera.main.ScreenToWorldPoint(TouchInput.GetPointerPosition());
            unusedSpell = Instantiate(spell.MySpellPrefab, castPos, Quaternion.identity);
            unusedSpell.transform.position = new Vector3(unusedSpell.transform.position.x, unusedSpell.transform.position.y, 0);
            aoeSpell = spell;
        }
        else
        {
            Destroy(unusedSpell);
        }

        if (MyTarget != null && MyTarget.GetComponentInParent<Character>().IsAlive && !IsAttacking && !IsMoving && InLineOfSight() && InRange(spell, MyTarget.transform.position)) //Chcks if we are able to attack
        {
            MyInitRoutine = StartCoroutine(AttackRoutine(spell));
        }
    }

    /// <summary>
    /// Diffuse aux autres clients connectes qu'on vient de lancer un sort
    /// cible, pour qu'ils voient le projectile (voir PlayerCombatSync).
    /// No-op en solo ou si on ne controle pas localement ce Player.
    /// </summary>
    private void BroadcastTargetedSpellCast(Spell spell, int usedExitIndex)
    {
        if (!IsLocallyControlled)
        {
            return;
        }

        if (combatSync == null)
        {
            combatSync = GetComponent<PlayerCombatSync>();
        }

        if (combatSync == null || MyTarget == null)
        {
            return;
        }

        NetworkIdentity targetIdentity = MyTarget.GetComponent<NetworkIdentity>();

        combatSync.BroadcastTargetedSpell(spell.MyTitle, targetIdentity, usedExitIndex);
    }

    /// <summary>
    /// Diffuse aux autres clients connectes qu'on vient de lancer un sort
    /// de zone (AOE), pour qu'ils voient le nuage au sol (voir PlayerCombatSync).
    /// </summary>
    private void BroadcastAOESpellCast(Spell spell, Vector3 position)
    {
        if (!IsLocallyControlled)
        {
            return;
        }

        if (combatSync == null)
        {
            combatSync = GetComponent<PlayerCombatSync>();
        }

        if (combatSync == null)
        {
            return;
        }

        combatSync.BroadcastAOESpell(spell.MyTitle, position);
    }

    private IEnumerator Regen()
    {
        while (true)
        {
            if (!InCombat)
            {
                if (health.MyCurrentValue < health.MyMaxValue)
                {
                    int value = Mathf.FloorToInt(health.MyMaxValue * 0.05f);
                    health.MyCurrentValue += value;

                    CombatTextManager.MyInstance.CreateText(transform.position, value.ToString(), SCTTYPE.HEAL, false);
                }

                if (mana.MyCurrentValue < mana.MyMaxValue)
                {
                    int value = Mathf.FloorToInt(mana.MyMaxValue * 0.05f);
                    mana.MyCurrentValue += value;

                    CombatTextManager.MyInstance.CreateText(transform.position, value.ToString(), SCTTYPE.MANA, false);
                }
            }


            //This is how often we will get a regen tick
            yield return new WaitForSeconds(1.5f);
        }

     
    }

    private bool InRange(Spell spell, Vector2 targetPos)
    {

        if (Vector2.Distance(targetPos, transform.position) <= spell.MyRange)
        {
            return true;
        }
        MessageFeedManager.MyInstance.WriteMessage("OUT OF RANGE!", Color.red);
        return false;
    }

    public void Gather(ICastable castable, List<Drop> items)
    {
        if (!IsAttacking)
        {
            MyInitRoutine = StartCoroutine(GatherRoutine(castable, items));
        }
    }

    /// <summary>
    /// Checks if the target is in line of sight
    /// </summary>
    /// <returns></returns>
    private bool InLineOfSight()
    {
        if (MyTarget != null)
        {
            //Calculates the target's direction
            Vector3 targetDirection = (MyTarget.transform.position - transform.position).normalized;

            //Thorws a raycast in the direction of the target
            RaycastHit2D hit = Physics2D.Raycast(transform.position, targetDirection, Vector2.Distance(transform.position, MyTarget.transform.position), 256);

            //If we didn't hit the block, then we can cast a spell
            if (hit.collider == null)
            {
                return true;
            }

        }

        //If we hit the block we can't cast a spell
        return false;
    }

    /// <summary>
    /// Convertit une direction vers un point en le meme index que exitIndex
    /// (0=haut, 1=droite, 2=bas, 3=gauche -- voir GetInput()), pour pouvoir
    /// orienter le joueur vers sa cible au moment de lancer un sort.
    /// </summary>
    private int GetExitIndexTowards(Vector3 targetPos)
    {
        Vector2 dir = targetPos - transform.position;

        if (Mathf.Abs(dir.x) > Mathf.Abs(dir.y))
        {
            return dir.x > 0 ? 1 : 3; // droite : gauche
        }
        else
        {
            return dir.y > 0 ? 0 : 2; // haut : bas
        }
    }

    /// <summary>
    /// Changes the blocks based on the players direction
    /// </summary>
    private void Block()
    {
        foreach (Block b in blocks)
        {
            b.Deactivate();
        }

        blocks[exitIndex].Activate();
    }

    /// <summary>
    /// Stops the attack
    /// </summary>
    public void StopAction()
    {
        //Stop the spellbook from casting
        SpellBook.MyInstance.StopCating();

        IsAttacking = false; //Makes sure that we are not attacking

        MyAnimator.SetBool("attack", IsAttacking); //Stops the attack animation

        foreach (GearSocket g in gearSockets)
        {
            g.MyAnimator.SetBool("attack", IsAttacking);
        }


        if (actionRoutine != null) //Checks if we have a reference to an co routine
        {
            StopCoroutine(actionRoutine);
        }
    }

    private void StopInit()
    {
        if (MyInitRoutine != null)
        {
            StopCoroutine(MyInitRoutine);
        }
    }

    public override void HandleLayers()
    {
        base.HandleLayers();

        if (IsMoving)
        {
            foreach (GearSocket g in gearSockets)
            {
                g.SetXAndY(Direction.x, Direction.y);
            }
        }
    }

    public override void ActivateLayer(string layerName)
    {
        base.ActivateLayer(layerName);

        foreach (GearSocket g in gearSockets)
        {
            g.ActivateLayer(layerName);
        }
    }


    public void GainXP(int xp)
    {
        MyXp.MyCurrentValue += xp;
        CombatTextManager.MyInstance.CreateText(transform.position, xp.ToString(), SCTTYPE.XP, false);

        if (MyXp.MyCurrentValue >= MyXp.MyMaxValue)
        {
            StartCoroutine(Ding());
        }
    }

    private IEnumerator Ding()
    {
        while (!MyXp.IsFull)
        {
            yield return null;
        }

        MyLevel++;
        if (ding != null)
        {
            ding.SetTrigger("Ding");
        }
        if (levelText != null)
        {
            levelText.text = MyLevel.ToString();
        }
        MyXp.MyMaxValue = 100 * MyLevel * Mathf.Pow(MyLevel, 0.5f);
        MyXp.MyMaxValue = Mathf.Floor(MyXp.MyMaxValue);
        MyXp.MyCurrentValue = MyXp.MyOverflow;
        MyXp.Reset();
        stamina += IncreaseBaseStat();
        intellect += IncreaseBaseStat();
        ResetStats();
        if (MyXp.MyCurrentValue >= MyXp.MyMaxValue)
        {
            StartCoroutine(Ding());
        }

    }

    public void EquipGear(Armor armor)
    {
        stamina += armor.Stamina;
        intellect += armor.Intellect;
        strength += armor.Strength;
        UpdateMaxStats();
        UIManager.MyInstance.UpdateStatsText(intellect, stamina, strength);
    }

    public void DequipGear(Armor armor)
    {
        stamina -= armor.Stamina;
        intellect -= armor.Intellect;
        strength -= armor.Strength;
        UpdateMaxStats();
        UIManager.MyInstance.UpdateStatsText(intellect, stamina, strength);
    }

    /// <summary>
    /// Donne acces (en lecture) a un socket d'equipement visuel par index,
    /// pour PlayerEquipmentSync (qui doit rester un NetworkBehaviour
    /// separe -- voir ce script -- et n'a donc pas acces direct au champ
    /// prive gearSockets).
    /// </summary>
    public GearSocket GetGearSocket(int index)
    {
        if (gearSockets == null || index < 0 || index >= gearSockets.Length)
        {
            return null;
        }

        return gearSockets[index];
    }

    /// <summary>
    /// Convertit un ArmorType (voir Armor.cs : Head=0 Shoulders=1 Chest=2
    /// Hands=3 Legs=4 Feet=5 MainHand=6 Offhand=7 TwoHand=8) vers l'index
    /// du socket visuel correspondant dans gearSockets (voir
    /// Player.prefab : 0=Tete 1=Torse 2=Epaules 3=Main/Arme 4=Jambes
    /// 5=Pieds). Hands et Offhand n'ont pas de socket visuel dans le jeu
    /// actuel (-1) ; TwoHand reutilise le socket d'arme principale.
    /// </summary>
    public static int SocketIndexForArmorType(int armorType)
    {
        switch (armorType)
        {
            case 0: return 0; // Head
            case 1: return 2; // Shoulders
            case 2: return 1; // Chest
            case 4: return 4; // Legs
            case 5: return 5; // Feet
            case 6: return 3; // MainHand
            case 8: return 3; // TwoHand
            default: return -1; // Hands, Offhand
        }
    }

    /// <summary>
    /// Previent les autres joueurs (via le serveur) qu'on vient d'equiper
    /// ou de retirer une piece d'equipement visuelle, pour que
    /// GearSocket.Equip()/Dequip() s'applique aussi sur leurs clients. En
    /// solo (pas de session Mirror active) ou si ce Player ne nous
    /// appartient pas, ne fait rien -- CharButton applique deja le visuel
    /// localement de son cote.
    /// </summary>
    public void SyncEquippedArmor(int socketIndex, Armor armor)
    {
        if (socketIndex < 0)
        {
            return;
        }

        if (equipmentSync == null)
        {
            equipmentSync = GetComponent<PlayerEquipmentSync>();
        }

        if (netIdentity == null)
        {
            netIdentity = GetComponent<NetworkIdentity>();
        }

        if (equipmentSync == null || netIdentity == null || !NetworkClient.active || !netIdentity.isLocalPlayer)
        {
            return;
        }

        int armorId = (armor != null && ArmorDatabase.MyInstance != null) ? ArmorDatabase.MyInstance.GetId(armor) : -1;
        equipmentSync.CmdSetEquippedArmor(socketIndex, armorId);
    }

    private int IncreaseBaseStat()
    {
        if (MyLevel < 10)
        {
            return 3;
        }

        return 0;
    }

    public void UpdateLevel()
    {
        if (levelText != null)
        {
            levelText.text = MyLevel.ToString();
        }
    }

    public void GetPath(Vector3 goal)
    {
        // astar n'existe que pour le joueur qu'on controle (voir
        // ResolveLocalReferences) -- sans lui, pas de clic-pour-se-deplacer,
        // mais le reste (clavier/joystick) continue de marcher normalement.
        if (astar == null)
        {
            return;
        }

        // ClickToMove() drives movement through transform.parent (see
        // initPos/destination below), not through this script's own
        // transform -- so the search has to start from transform.parent
        // too, or it can plan a path from the wrong tile whenever the two
        // have drifted apart (see the velocity reset right below for why
        // that happens).
        MyPath = astar.Algorithm(transform.parent.position, goal);

        // Algorithm() returns null when no path exists (e.g. clicking a
        // blocked/out-of-bounds tile), and returns a single-node path when
        // the goal is the tile we're already standing on. Either way there
        // is nothing to Pop() twice, so bail out instead of crashing.
        if (MyPath == null || MyPath.Count < 2)
        {
            MyPath = null;
            return;
        }

        // Move() only drives MyRigidbody while MyPath is null (regular
        // WASD movement) and never clears it again, so any velocity still
        // left over from walking with the keyboard kept being applied by
        // the physics engine on top of ClickToMove's own positioning --
        // compounding into the character accelerating with every click,
        // and eventually drifting into a wall/off the path entirely.
        MyRigidbody.velocity = Vector2.zero;

        current = MyPath.Pop();
        destination = MyPath.Pop();
        this.goal = goal;
    }

    public IEnumerator Respawn()
    {
        MySpriteRenderer.enabled = false;
        yield return new WaitForSeconds(5f);
        health.Initialize(initHealth, initHealth);
        MyMana.Initialize(initMana, initMana);
        transform.parent.position = initPos;
        MySpriteRenderer.enabled = true;
        MyAnimator.SetTrigger("respawn");
        foreach (SpriteRenderer spriteRenderer in gearRenderers)
        {
            spriteRenderer.enabled = true;
        }
    }

    public void ClickToMove()
    {
        if (MyPath != null)
        {
            //Moves the enemy towards the target
            transform.parent.position = Vector2.MoveTowards(transform.parent.position, destination, 2 * Time.deltaTime);

            Vector3Int dest = astar.MyTilemap.WorldToCell(destination);
            Vector3Int cur = astar.MyTilemap.WorldToCell(current);

            float distance = Vector2.Distance(destination, transform.parent.position);

            if (cur.y > dest.y)
            {
                Direction = Vector2.down;
            }
            else if (cur.y < dest.y)
            {
                Direction = Vector2.up;
            }
            if (cur.y == dest.y)
            {
                if (cur.x > dest.x)
                {
                    Direction = Vector2.left;
                }
                else if (cur.x < dest.x)
                {
                    Direction = Vector2.right;
                }
            }
            if (distance <= 0f)
            {
                if (MyPath.Count > 0)
                {
                    current = destination;
                    destination = MyPath.Pop();
                }
                else
                {
                    MyPath = null;
                }
            }
        }

    }

    public void HideGear()
    {
        foreach (SpriteRenderer spriteRenderer in gearRenderers)
        {
            spriteRenderer.enabled = false;
        }
    }

    public override void AddAttacker(Character attacker)
    {
        int count = Attackers.Count;

        base.AddAttacker(attacker);

        if (count == 0)
        {
            InCombat = true;
            CombatTextManager.MyInstance.CreateText(transform.position, "+COMBAT", SCTTYPE.TEXT, false);
        }
    }

    public override void RemoveAttacker(Character attacker)
    {
        base.RemoveAttacker(attacker);
        if (Attackers.Count == 0)
        {
            InCombat = false;
            CombatTextManager.MyInstance.CreateText(transform.position, "-COMBAT", SCTTYPE.TEXT, false);

        }
        
    }

    public void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "Enemy" ||collision.tag== "Interactable")
        {
            IInteractable interactable = collision.GetComponent<IInteractable>();

            if (!MyInteractables.Contains(interactable))
            {
                MyInteractables.Add(interactable);
            }
        }
    }

    public void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.tag == "Enemy" || collision.tag == "Interactable")
        {
            if (MyInteractables.Count > 0)
            {
                IInteractable interactable = MyInteractables.Find(x => x == collision.GetComponent<IInteractable>());

                if (interactable != null)
                {
                    interactable.StopInteract();
                }

                MyInteractables.Remove(interactable);
            }

           
  
        }
    }
}
