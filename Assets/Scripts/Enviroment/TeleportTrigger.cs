using UnityEngine;

/// <summary>
/// Teleporte le joueur local vers une position cible des qu'il entre dans ce
/// trigger. Utilise pour la porte de la Capitale : on place ce composant sur
/// un collider trigger juste devant la porte (cote exterieur), avec
/// targetPosition pointant juste a l'interieur des remparts -- ca donne
/// l'impression que la porte "s'ouvre" et fait entrer le joueur.
///
/// La position du joueur est entierement geree par NetworkTransform (voir le
/// clamp de carte dans Player.Update(), qui fait deja directement
/// transform.position = ... pour le joueur local) -- donc pas besoin de
/// [Command] ici, on fait pareil : seul le client qui possede ce joueur
/// (IsLocallyControlled) deplace son propre transform, et NetworkTransform se
/// charge de repercuter le changement aux autres clients.
/// </summary>
public class TeleportTrigger : MonoBehaviour
{
    [Tooltip("Position (monde) ou le joueur est teleporte en entrant dans ce trigger.")]
    [SerializeField]
    private Vector3 targetPosition;

    [Tooltip("Delai minimum (secondes) entre deux teleportations du meme joueur, pour eviter un aller-retour en boucle si la destination est proche du trigger.")]
    [SerializeField]
    private float cooldown = 1f;

    private float lastTeleportTime = -999f;

    private void OnTriggerEnter2D(Collider2D collision)
    {
        Player player = collision.GetComponent<Player>();
        if (player == null)
        {
            player = collision.GetComponentInParent<Player>();
        }

        if (player == null || !player.IsLocallyControlled)
        {
            return;
        }

        if (Time.time - lastTeleportTime < cooldown)
        {
            return;
        }

        lastTeleportTime = Time.time;
        player.transform.position = targetPosition;
    }
}
