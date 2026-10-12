using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Range : MonoBehaviour
{

    private Enemy parent;

    private void Awake()
    {
        // Awake (et non Start) : OnTriggerEnter2D peut se declencher au
        // chargement de la scene, avant le premier Start().
        parent = GetComponentInParent<Enemy>();
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (parent == null)
        {
            parent = GetComponentInParent<Enemy>();

            if (parent == null)
            {
                return;
            }
        }

        // Seul celui qui fait tourner l'IA (solo ou serveur dedie) choisit
        // une cible ; un simple client n'en a pas besoin.
        if (!parent.ShouldRunAI)
        {
            return;
        }

        if (collision.tag == "Player")
        {
            // Le collider "Player" peut etre sur un objet enfant.
            Character target = collision.GetComponentInParent<Character>();

            if (target != null)
            {
                parent.SetTarget(target);
            }
        }
    }
}
