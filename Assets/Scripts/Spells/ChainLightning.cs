using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Windows.Speech;

public class ChainLightning : SpellScript
{
    [SerializeField]
    private LayerMask layerMask;

    private List<Transform> targets = new List<Transform>();

    protected bool hit = false;

    protected int targetIndex;

    protected override void OnTriggerEnter2D(Collider2D collision)
    {
        if (!hit && collision.tag == "HitBox" && collision.transform == MyTarget)
        {
            hit = true;

            Collider2D[] tmp = Physics2D.OverlapCircleAll(collision.transform.position, 10, layerMask);

            foreach (Collider2D collider in tmp)
            {
                if (collider.transform != MyTarget && collider.transform != transform && collider.tag == "HitBox" && collider.transform.parent != Source.transform)
                {
                    targets.Add(collider.transform);
                }
            }

            speed *= 2;
            PickTarget(collision);
        }
    }

    private void Update()
    {
        float distance = 0;

        if (MyTarget != null)
        {
            distance = Vector2.Distance(transform.position, MyTarget.position);
        }
        if (distance <= 0.1f)
        {
            if (hit && targetIndex < targets.Count)
            {
                PickTarget(MyTarget.GetComponent<Collider2D>());
            }
            else if (MyTarget != null)
            {
                base.OnTriggerEnter2D(MyTarget.GetComponent<Collider2D>());
            }
        }

            
    }

    private void PickTarget(Collider2D collision)
    {
        Character c = collision.GetComponentInParent<Character>();
        c.TakeDamage(damage, Source);
        MyTarget = targets[targetIndex];
        targetIndex++;
    
    }
}
