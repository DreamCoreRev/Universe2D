using Assets.Scripts.Debuffs;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpellScript : MonoBehaviour {

    /// <summary>
    /// A reference to the spell's rigid body
    /// </summary>
    private Rigidbody2D myRigidBody;
    
    /// <summary>
    /// The spell's movement speed
    /// </summary>
    [SerializeField]
    protected float speed;

    /// <summary>
    /// The spells target
    /// </summary>
    public Transform MyTarget { get; protected set; }

    public Character Source { get; set; }

    protected float damage;

    private Debuff debuff;

    // Use this for initialization
    void Start ()
    {
        //Creates a reference to the spell's rigidbody
        myRigidBody = GetComponent<Rigidbody2D>();
	}

    public void Initialize(Transform target, float damage, Character source, Debuff debuff)
    {
        this.MyTarget = target;
        this.damage = damage;
        this.Source = source;
        this.debuff = debuff;
    }

    public void Initialize(Transform target, float damage, Character source)
    {
        this.MyTarget = target;
        this.damage = damage;
        this.Source = source;
    }

    private void FixedUpdate()
    {
        if (MyTarget != null)
        {
            //Calculates the spells direction
            Vector2 direction = MyTarget.position - transform.position;

            //Moves the spell by using the rigid body
            myRigidBody.velocity = direction.normalized * speed;

            //Calculates the rotation angle
            float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;

            //Rotates the spell towards the target
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);
        }


    }

    protected virtual void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.tag == "HitBox" && collision.transform == MyTarget)
        {
            Character c = collision.GetComponentInParent<Character>();
            speed = 0;
            c.TakeDamage(damage, Source);

            if (debuff != null)
            {
                Debuff clone = debuff.Clone();
                clone.Apply(c);
            }

            GetComponent<Animator>().SetTrigger("impact");
            myRigidBody.velocity = Vector2.zero;
            MyTarget = null;
        }
    }
}
