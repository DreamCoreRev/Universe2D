using Assets.Scripts.Debuffs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public delegate void Done();

[Serializable]
public class Spell : IUseable, IMoveable, IDescribable, ICastable
{
    public event Done CastDone;

    /// <summary>
    /// The Spell's name
    /// </summary>
    [SerializeField]
    private string title;

    /// <summary>
    /// The spell's damage
    /// </summary>
    [SerializeField]
    private float damage;

    [SerializeField]
    private float duration;

    [SerializeField]
    private float range;

    /// <summary>
    /// The spell's icon
    /// </summary>
    [SerializeField]
    private Sprite icon;

    /// <summary>
    /// The spell's speed
    /// </summary>
    [SerializeField]
    private float speed;

    /// <summary>
    /// The spell's cast time
    /// </summary>
    [SerializeField]
    private float castTime;

    /// <summary>
    /// The spell's cooldown
    /// </summary>
    [SerializeField]
    private float cooldown;

    /// <summary>
    /// The spell's prefab
    /// </summary>
    [SerializeField]
    private GameObject spellPrefab;

    [SerializeField]
    private string description;

    /// <summary>
    /// The spell's color
    /// </summary>
    [SerializeField]
    private Color barColor;

    [SerializeField]
    private bool needsTarget;

    [SerializeField]
    private int manaCost;

    public Debuff MyDebuff { get; set; }



    /// <summary>
    /// Property for accessing the spell's name
    /// </summary>
    public string MyTitle
    {
        get
        {
            return title;
        }
    }

    /// <summary>
    /// Property for reading the damage
    /// </summary>
    public float MyDamage
    {
        get
        {
            return Mathf.Ceil(damage);
        }
        set
        {
            damage = value;
        }
    }

    /// <summary>
    /// Property for reading the icon
    /// </summary>
    public Sprite MyIcon
    {
        get
        {
            return icon;
        }
    }

    /// <summary>
    /// Property for reading the speed
    /// </summary>
    public float MySpeed
    {
        get
        {
            return speed;
        }
    }

    /// <summary>
    /// Property for reading the cast time
    /// </summary>
    public float MyCastTime
    {
        get
        {
            return castTime;
        }
        set
        {
            castTime = value;
        }
    }

    /// <summary>
    /// Property for reading the spell's prefab
    /// </summary>
    public GameObject MySpellPrefab
    {
        get
        {
            return spellPrefab;
        }
    }

    /// <summary>
    /// Property for reading the color
    /// </summary>
    public Color MyBarColor
    {
        get
        {
            return barColor;
        }
    }

    public float MyRange
    {
        get
        {
            return range;
        }

        set
        {
            range = value;
        }
    }

    public bool NeedsTarget { get => needsTarget;}
    public float MyDuration { get => duration; set => duration = value; }
    public int ManaCost { get => manaCost; set => manaCost = value; }
    public bool OnCooldown { get; set; }
    public float MyCooldown { get => cooldown; set => cooldown = value; }

    public string GetDescription()
    {
        if (!needsTarget)
        {
            return $"{title}<color=#ffd100>\n{description}\nthat does {damage / MyDuration} damage\nevery sec for {MyDuration} sec</color>";
        }
        else
        {
            return string.Format("{0}\nCast time: {1} second(s)\n<color=#ffd111>{2}\nthat causes {3} damage</color>", title, castTime, description, MyDamage);
        }


    }

    public void OnCastDone()
    {
        if (CastDone != null)
        {
            CastDone.Invoke();
        }
    }

    public void Use()
    {
        Player.MyInstance.CastSpell(this);
    }
}
