using UnityEngine;

/// <summary>
/// Teleporte le joueur local vers une position cible des qu'il entre dans ce
/// trigger. Utilise pour la porte de la Capitale : un trigger cote exterieur
/// envoie le joueur juste a l'interieur des remparts, et un second trigger
/// cote interieur le renvoie juste a l'exterieur -- ca donne l'impression que
/// la porte "s'ouvre" et fait passer le joueur.
///
/// La position du joueur est entierement geree par NetworkTransform (voir le
/// clamp de carte dans Player.Update(), qui fait deja directement
/// transform.position = ... pour le joueur local) -- donc pas besoin de
/// [Command] ici, on fait pareil : seul le client qui possede ce joueur
/// (IsLocallyControlled) deplace son propre transform, et NetworkTransform se
/// charge de repercuter le changement aux autres clients.
///
/// IMPORTANT : lastTeleportTime est STATIC, partage par TOUS les
/// TeleportTrigger de la scene (pas un champ par instance). Avec une paire
/// entree/sortie proche l'une de l'autre (porte de la Capitale), un cooldown
/// par instance ne protege que contre le re-declenchement du MEME trigger --
/// si le joueur retraverse tout de suite le trigger OPPOSE (celui qui
/// ramenait d'ou il vient), rien ne l'empechait de faire l'aller-retour en
/// boucle. Un cooldown partage bloque toute teleportation (quel que soit le
/// trigger) pendant un court delai apres chacune, le temps que le joueur
/// s'eloigne de la zone d'arrivee.
/// </summary>
public class TeleportTrigger : MonoBehaviour
{
    [Tooltip("Position (monde) ou le joueur est teleporte en entrant dans ce trigger.")]
    [SerializeField]
    private Vector3 targetPosition;

    [Tooltip("Delai minimum (secondes) entre deux teleportations, tous teleporteurs confondus, pour eviter un aller-retour en boucle entre deux triggers proches.")]
    [SerializeField]
    private float cooldown = 1.5f;

    private static float lastTeleportTime = -999f;

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
