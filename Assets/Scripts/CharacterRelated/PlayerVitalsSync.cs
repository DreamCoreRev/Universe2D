using Mirror;
using UnityEngine;

/// <summary>
/// Diffuse la vie, la mana et le niveau du joueur local aux autres clients,
/// pour que le portrait de cible-joueur (voir GroupUIManager.
/// ShowPlayerTargetFrame) puisse afficher des barres de vie/mana a jour
/// comme sur WoW, au lieu de rester a 0 pour tout le monde sauf nous-meme.
///
/// Avant ce composant, Player.MyHealth/MyMana (des Stat, voir Stat.cs)
/// n'etaient meme PAS renseignes pour un joueur distant : Player.
/// ResolveLocalReferences() -- qui les relie aux barres de l'interface --
/// ne s'execute QUE si IsLocallyControlled (voir Player.Start()), donc ces
/// champs restent null sur l'avatar d'un autre joueur chez nous. Ce
/// composant lit donc ces valeurs UNIQUEMENT cote joueur local (lui seul
/// les a), les diffuse via Mirror, et n'importe quel client peut ensuite
/// lire CurrentHealth/MaxHealth/CurrentMana/MaxMana/Level sur n'importe
/// quel Player -- y compris un joueur distant -- sans jamais toucher a ses
/// propres champs health/mana (qui restent strictement locaux, comme
/// avant).
/// </summary>
public class PlayerVitalsSync : NetworkBehaviour
{
    private Player player;

    [SyncVar] private float currentHealth;
    [SyncVar] private float maxHealth;
    [SyncVar] private float currentMana;
    [SyncVar] private float maxMana;
    [SyncVar] private int level;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public float CurrentMana => currentMana;
    public float MaxMana => maxMana;
    public int Level => level;

    private float lastSentHealth = -1f;
    private float lastSentMaxHealth = -1f;
    private float lastSentMana = -1f;
    private float lastSentMaxMana = -1f;
    private int lastSentLevel = -1;

    private void Awake()
    {
        player = GetComponent<Player>();
    }

    private void Update()
    {
        // Seul le joueur local a des Stat health/mana renseignes (voir
        // commentaire de classe) -- un avatar distant ne doit donc jamais
        // essayer d'envoyer quoi que ce soit, il se contente de RECEVOIR
        // les SyncVar envoyees par son propre client.
        if (player == null || !player.IsLocallyControlled)
        {
            return;
        }

        if (player.MyHealth == null || player.MyMana == null)
        {
            return;
        }

        float hpCur = player.MyHealth.MyCurrentValue;
        float hpMax = player.MyHealth.MyMaxValue;
        float mpCur = player.MyMana.MyCurrentValue;
        float mpMax = player.MyMana.MyMaxValue;
        int lvl = player.MyLevel;

        if (hpCur == lastSentHealth && hpMax == lastSentMaxHealth
            && mpCur == lastSentMana && mpMax == lastSentMaxMana
            && lvl == lastSentLevel)
        {
            return;
        }

        lastSentHealth = hpCur;
        lastSentMaxHealth = hpMax;
        lastSentMana = mpCur;
        lastSentMaxMana = mpMax;
        lastSentLevel = lvl;

        CmdSetVitals(hpCur, hpMax, mpCur, mpMax, lvl);
    }

    [Command]
    private void CmdSetVitals(float hpCur, float hpMax, float mpCur, float mpMax, int lvl)
    {
        currentHealth = hpCur;
        maxHealth = hpMax;
        currentMana = mpCur;
        maxMana = mpMax;
        level = lvl;
    }
}
