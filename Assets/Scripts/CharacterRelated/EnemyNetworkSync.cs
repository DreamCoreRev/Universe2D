using Mirror;
using UnityEngine;

/// <summary>
/// Rend un monstre (Enemy/RangedEnemy place dans la scene) partage entre
/// tous les joueurs connectes : un seul monstre, visible et combattu par
/// tout le monde, avec des PV communs.
///
/// Principe : l'IA (deplacement, ciblage, attaque, voir Enemy.ShouldRunAI)
/// ne tourne que sur le serveur dedie (RPG.exe -batchmode -nographics),
/// seul habilite a modifier les PV. Ce composant synchronise aux clients
/// ce qu'il faut pour l'affichage (PV, direction d'animation, etat
/// "en train d'attaquer") et sert de relais pour :
/// - les degats infliges a ce monstre (CmdDealDamage, appele par le
///   client qui vient de le toucher) ;
/// - les degats que CE monstre inflige a un joueur (NotifyDealDamageToPlayer,
///   appele uniquement par le serveur -- voir CombatNetworking) ;
/// - le credit d'XP au joueur qui a porte le coup fatal (TargetGrantXP).
/// </summary>
public class EnemyNetworkSync : NetworkBehaviour
{
    private Enemy enemy;

    [SyncVar(hook = nameof(OnHealthChanged))]
    private float syncedHealth;

    [SyncVar(hook = nameof(OnDirectionChanged))]
    private Vector2 syncedDirection;

    [SyncVar(hook = nameof(OnAttackingChanged))]
    private bool syncedIsAttacking;

    private void Awake()
    {
        enemy = GetComponent<Enemy>();

        if (enemy != null)
        {
            enemy.OnKilledBy += HandleKilledBy;
        }
    }

    private void OnDestroy()
    {
        if (enemy != null)
        {
            enemy.OnKilledBy -= HandleKilledBy;
        }
    }

    public override void OnStartServer()
    {
        base.OnStartServer();

        if (enemy != null && enemy.MyHealth != null)
        {
            syncedHealth = enemy.MyHealth.MyCurrentValue;
        }
    }

    private void Update()
    {
        // Seul le serveur fait tourner l'IA (voir Enemy.ShouldRunAI) : c'est
        // donc lui, et lui seul, qui pousse direction/attaque aux clients.
        if (!isServer || enemy == null)
        {
            return;
        }

        if (syncedDirection != enemy.Direction)
        {
            syncedDirection = enemy.Direction;
        }

        if (syncedIsAttacking != enemy.IsAttacking)
        {
            syncedIsAttacking = enemy.IsAttacking;
        }
    }

    /// <summary>
    /// Appele par le client qui vient de toucher ce monstre (sort, fleche,
    /// melee...) : seul le serveur applique reellement les degats, ce qui
    /// evite qu'un monstre partage perde des PV plusieurs fois (une fois
    /// par client attaquant).
    /// </summary>
    [Command(requiresAuthority = false)]
    public void CmdDealDamage(float damage, NetworkIdentity sourceIdentity)
    {
        if (enemy == null || !enemy.IsAlive)
        {
            return;
        }

        Character source = sourceIdentity != null ? sourceIdentity.GetComponent<Character>() : null;

        if (source == null)
        {
            return;
        }

        enemy.TakeDamage(damage, source);

        syncedHealth = enemy.MyHealth.MyCurrentValue;
    }

    /// <summary>
    /// Appele uniquement par le serveur (voir CombatNetworking.DealDamage)
    /// quand ce monstre vient de toucher un joueur : on relaie au client
    /// concerne, seul a pouvoir modifier sa propre vie (comme en solo).
    /// </summary>
    public void NotifyDealDamageToPlayer(Player target, float damage)
    {
        if (!isServer || target == null)
        {
            return;
        }

        NetworkIdentity targetIdentity = target.GetComponent<NetworkIdentity>();

        if (targetIdentity != null && targetIdentity.connectionToClient != null)
        {
            TargetTakeDamage(targetIdentity.connectionToClient, damage);
        }
    }

    [TargetRpc]
    private void TargetTakeDamage(NetworkConnectionToClient target, float damage)
    {
        if (Player.MyInstance != null && enemy != null)
        {
            Player.MyInstance.TakeDamage(damage, enemy);
        }
    }

    private void HandleKilledBy(Character source)
    {
        if (!isServer)
        {
            return;
        }

        if (!(source is Player killer))
        {
            return;
        }

        NetworkIdentity killerIdentity = killer.GetComponent<NetworkIdentity>();

        if (killerIdentity != null && killerIdentity.connectionToClient != null)
        {
            TargetGrantXP(killerIdentity.connectionToClient, XPManager.CalculateXP(enemy, killer.MyLevel));
        }
    }

    [TargetRpc]
    private void TargetGrantXP(NetworkConnectionToClient target, int xp)
    {
        if (Player.MyInstance != null)
        {
            Player.MyInstance.GainXP(xp);
        }
    }

    private void OnHealthChanged(float oldValue, float newValue)
    {
        if (enemy == null || enemy.MyHealth == null || isServer)
        {
            // Le serveur a deja tout applique lui-meme via TakeDamage().
            return;
        }

        bool wasAlive = enemy.MyHealth.MyCurrentValue > 0;

        enemy.MyHealth.MyCurrentValue = newValue;
        enemy.OnHealthChanged(newValue);

        if (wasAlive && newValue <= 0)
        {
            enemy.Direction = Vector2.zero;

            if (enemy.MyRigidbody != null)
            {
                enemy.MyRigidbody.velocity = Vector2.zero;
            }

            GameManager.MyInstance.OnKillConfirmed(enemy);
            enemy.MyAnimator.SetTrigger("die");
        }
    }

    private void OnDirectionChanged(Vector2 oldValue, Vector2 newValue)
    {
        if (enemy == null || isServer)
        {
            return;
        }

        enemy.Direction = newValue;
    }

    private void OnAttackingChanged(bool oldValue, bool newValue)
    {
        if (enemy == null || isServer)
        {
            return;
        }

        enemy.IsAttacking = newValue;

        if (newValue)
        {
            enemy.MyAnimator.SetTrigger("attack");
        }
    }
}
