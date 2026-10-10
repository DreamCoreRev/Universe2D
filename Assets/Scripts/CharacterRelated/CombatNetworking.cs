using Mirror;
using UnityEngine;

/// <summary>
/// Point d'entree unique pour tous les degats de combat (sorts, fleches,
/// attaques de melee), qu'on soit en solo ou en reseau.
///
/// - En solo (pas de session Mirror active) : comportement 100% d'origine,
///   TakeDamage() est appele directement.
/// - En reseau, cible = Enemy (monstre partage) : seul le serveur a le
///   droit de modifier ses PV, donc on route via EnemyNetworkSync.CmdDealDamage.
/// - En reseau, cible = Player (un monstre attaque un joueur) : seul le
///   serveur a l'autorite pour infliger des degats a un joueur (l'IA ne
///   tourne que sur le serveur, voir Enemy.ShouldRunAI). Un client qui
///   appelle ceci (ex: sa propre copie visuelle de l'animation d'attaque)
///   est ignore pour eviter d'infliger les degats plusieurs fois.
/// </summary>
public static class CombatNetworking
{
    public static void DealDamage(Character target, float damage, Character source)
    {
        if (target == null)
        {
            return;
        }

        bool networked = NetworkClient.active || NetworkServer.active;

        if (!networked)
        {
            target.TakeDamage(damage, source);
            return;
        }

        if (target is Enemy enemyTarget)
        {
            EnemyNetworkSync sync = enemyTarget.GetComponent<EnemyNetworkSync>();
            if (sync != null)
            {
                NetworkIdentity sourceIdentity = source != null ? source.GetComponent<NetworkIdentity>() : null;
                sync.CmdDealDamage(damage, sourceIdentity);
                return;
            }
        }

        if (target is Player targetPlayer && source is Enemy enemySource)
        {
            if (NetworkServer.active)
            {
                EnemyNetworkSync sync = enemySource.GetComponent<EnemyNetworkSync>();
                if (sync != null)
                {
                    sync.NotifyDealDamageToPlayer(targetPlayer, damage);
                }
            }

            return;
        }

        // Cas non couvert ci-dessus (ex: pas de EnemyNetworkSync trouve) :
        // on retombe sur le comportement d'origine plutot que de ne rien faire.
        target.TakeDamage(damage, source);
    }
}
