using System;
using System.Collections;
using System.Collections.Generic;
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
        StartCoroutine(Regen());
    }

    /// <summary>
    /// We are overriding the characters update function, so that we can execute our own functions
    /// </summary>
    protected override void Update()
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
                Destroy(unusedSpell);
                unusedSpell = null;
                s.Initialize(aoeSpell.MyDamage, aoeSpell.MyDuration);
                mana.MyCurrentValue -= aoeSpell.ManaCost;
                StartCoroutine(SpellBook.MyInstance.CastCooldown(aoeSpell));
            }
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
        levelText.text = MyLevel.ToString();
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
            minimapIcon.eulerAngles = new Vector3(0, 0, 0);
        }
        if (Input.GetKey(KeybindManager.MyInstance.Keybinds["LEFT"])) //Moves left
        {
            exitIndex = 3;
            Direction += Vector2.left;
            if (Direction.y == 0)
            {
                minimapIcon.eulerAngles = new Vector3(0, 0, 90);
            }

        }
        if (Input.GetKey(KeybindManager.MyInstance.Keybinds["DOWN"]))
        {
            exitIndex = 2;
            Direction += Vector2.down;

            minimapIcon.eulerAngles = new Vector3(0, 0, 180);
        }
        if (Input.GetKey(KeybindManager.MyInstance.Keybinds["RIGHT"])) //Moves right
        {
            exitIndex = 1;
            Direction += Vector2.right;
            if (Direction.y == 0)
            {
                minimapIcon.eulerAngles = new Vector3(0, 0, 270);
            }

        }
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
        if (!spell.OnCooldown)
        {
            Block();

            if (spell.ManaCost <= mana.MyCurrentValue)
            {
                if (!spell.NeedsTarget && unusedSpell == null)
                {
                    unusedSpell = Instantiate(spell.MySpellPrefab, Camera.main.ScreenToWorldPoint(Input.mousePosition), Quaternion.identity);
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
        }
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
        ding.SetTrigger("Ding");
        levelText.text = MyLevel.ToString();
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
        levelText.text = MyLevel.ToString();
    }

    public void GetPath(Vector3 goal)
    {
        MyPath = astar.Algorithm(transform.position, goal);
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
