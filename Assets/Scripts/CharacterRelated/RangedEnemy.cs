using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class RangedEnemy : Enemy
{
    [SerializeField]
    private GameObject arrowPrefab;

    [SerializeField]
    private Transform[] exitPoints;

    private float fieldOfView = 120;

    private bool updateDirection = false;

    protected override void Update()
    {
        LookAtTarget();
        base.Update();
    }

    private void LateUpdate()
    {
        UpdateDirection();
    }

    /// <summary>
    /// Appele par un Animation Event sur le clip d'attaque. Comme pour
    /// DoDamage(), ce clip joue visuellement sur TOUS les clients (voir
    /// EnemyNetworkSync.OnAttackingChanged) -- seul celui qui fait tourner
    /// l'IA (ShouldRunAI) doit instancier la vraie fleche qui inflige des
    /// degats. Les autres clients recoivent une fleche purement visuelle
    /// via EnemyNetworkSync.BroadcastArrowVisual, pour pouvoir la voir
    /// partir sans la resoudre une deuxieme fois.
    /// </summary>
    public void Shoot(int exitIndex)
    {
        if (!ShouldRunAI || MyTarget == null)
        {
            return;
        }

        SpellScript s = Instantiate(arrowPrefab, exitPoints[exitIndex].position, Quaternion.identity).GetComponent<SpellScript>();

        s.Initialize(MyTarget.MyHitbox, damage, this);

        EnemyNetworkSync sync = GetComponent<EnemyNetworkSync>();

        if (sync != null)
        {
            NetworkIdentity targetIdentity = MyTarget.GetComponent<NetworkIdentity>();
            sync.BroadcastArrowVisual(exitIndex, targetIdentity);
        }
    }

    /// <summary>
    /// Instancie une fleche purement cosmetique (voir SpellScript.VisualOnly)
    /// sur un client qui n'est pas aux commandes de l'IA, pour que le tir
    /// reste visible meme si les degats sont resolus ailleurs.
    /// </summary>
    public void ShootVisual(int exitIndex, Transform target)
    {
        SpellScript s = Instantiate(arrowPrefab, exitPoints[exitIndex].position, Quaternion.identity).GetComponent<SpellScript>();

        s.VisualOnly = true;
        s.Initialize(target, damage, this);
    }

    private void UpdateDirection()
    {
        if (updateDirection)
        {
            Vector2 dir = Vector2.zero;

            if (MySpriteRenderer.sprite.name.Contains("up"))
            {
                dir = Vector2.up;
            }
            else if (MySpriteRenderer.sprite.name.Contains("down"))
            {
                dir = Vector2.down;
            }
            else if (MySpriteRenderer.sprite.name.Contains("left"))
            {
                dir = Vector2.left;

            }
            else if (MySpriteRenderer.sprite.name.Contains("right"))
            {
                dir = Vector2.right;
            }

            MyAnimator.SetFloat("x", dir.x);
            MyAnimator.SetFloat("y", dir.y);
            updateDirection = false;
        }

     
    }

    private void LookAtTarget()
    {
        if (MyTarget != null)
        {
            Vector2 directionToTarget = (MyTarget.transform.position - transform.position).normalized;

            Vector2 faceing = new Vector2(MyAnimator.GetFloat("x"), MyAnimator.GetFloat("y"));

            float angleToTarget = Vector2.Angle(faceing, directionToTarget);

            if (angleToTarget > fieldOfView /2 )
            {
                MyAnimator.SetFloat("x", directionToTarget.x);
                MyAnimator.SetFloat("y", directionToTarget.y);

                updateDirection = true;
            }
        }
    }

}
