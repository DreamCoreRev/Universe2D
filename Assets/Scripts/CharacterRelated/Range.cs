using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Range : MonoBehaviour
{

    private Enemy parent;

    private void Awake()
    {
        // Awake (et non Start) : le trigger peut se declencher au
        // chargement de la scene, avant le premier Start().
        parent = GetComponentInParent<Enemy>();
    }

    // OnTriggerStay2D (et non Enter) : OnTriggerEnter2D ne se declenche qu'UNE
    // fois a l'entree dans la zone. Apres un Respawn() ou un Evade(), un joueur
    // deja dans la zone n'etait donc jamais re-cible : le monstre restait
    // passif tant que le joueur ne sortait pas puis rentrait.
    private void OnTriggerStay2D(Collider2D collision)
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

        // Un monstre mort ne doit pas prendre de cible (sinon il la garde
        // pendant tout le delai de respawn).
        if (!parent.IsAlive || parent.MyTarget != null)
        {
            return;
        }

        if (collision.tag == "Player")
        {
            // Le collider "Player" peut etre sur un objet enfant.
            Character target = collision.GetComponentInParent<Character>();

            if (target != null && target.IsAlive)
            {
                parent.SetTarget(target);
            }
        }
    }
}
