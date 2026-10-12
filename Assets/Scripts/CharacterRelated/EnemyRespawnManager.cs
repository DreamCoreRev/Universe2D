using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Gestionnaire central de respawn des monstres (solo ou serveur dedie).
///
/// Avant, le minuteur vivait dans Enemy.Update() : si l'Update du cadavre
/// s'arretait (objet/composant desactive, exception...), plus rien ne le
/// faisait respawn. Ici le minuteur appartient a un objet independant des
/// monstres (DontDestroyOnLoad), base sur Time.realtimeSinceStartup (donc
/// insensible a Time.timeScale), et il reactive au besoin le monstre avant
/// de le ressusciter. Il ecrit aussi un "heartbeat" toutes les 10 s dans le
/// log tant que des monstres attendent : si ces lignes s'arretent, c'est que
/// tout le serveur est fige, pas seulement les monstres.
/// </summary>
public class EnemyRespawnManager : MonoBehaviour
{
    private class Entry
    {
        public Enemy enemy;
        public float respawnAt;
    }

    private static EnemyRespawnManager instance;

    private readonly List<Entry> pending = new List<Entry>();

    private float nextHeartbeat;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        EnsureInstance();
    }

    private static EnemyRespawnManager EnsureInstance()
    {
        if (instance == null)
        {
            GameObject go = new GameObject("EnemyRespawnManager");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<EnemyRespawnManager>();
        }

        return instance;
    }

    /// <summary>
    /// Programme le respawn d'un monstre mort (sans effet s'il l'est deja).
    /// </summary>
    public static void Schedule(Enemy enemy, float delay)
    {
        if (enemy == null)
        {
            return;
        }

        EnemyRespawnManager manager = EnsureInstance();

        foreach (Entry e in manager.pending)
        {
            if (e.enemy == enemy)
            {
                return;
            }
        }

        manager.pending.Add(new Entry { enemy = enemy, respawnAt = Time.realtimeSinceStartup + delay });
        Debug.Log("[Respawn] " + enemy.name + " programme dans " + delay + "s (" + manager.pending.Count + " en attente)");
    }

    private void Update()
    {
        if (pending.Count == 0)
        {
            return;
        }

        float now = Time.realtimeSinceStartup;

        if (now >= nextHeartbeat)
        {
            nextHeartbeat = now + 10f;
            Debug.Log("[Respawn] heartbeat : " + pending.Count + " monstre(s) en attente");
        }

        for (int i = pending.Count - 1; i >= 0; i--)
        {
            Entry entry = pending[i];

            // Monstre detruit, ou deja ressuscite par un autre chemin.
            if (entry.enemy == null || entry.enemy.IsAlive)
            {
                pending.RemoveAt(i);
                continue;
            }

            if (now < entry.respawnAt)
            {
                continue;
            }

            pending.RemoveAt(i);

            try
            {
                Enemy enemy = entry.enemy;

                // Si quelque chose a desactive le cadavre (ou son wrapper), on le reactive.
                for (Transform t = enemy.transform; t != null; t = t.parent)
                {
                    if (!t.gameObject.activeSelf)
                    {
                        Debug.LogWarning("[Respawn] " + t.name + " etait desactive : reactivation");
                        t.gameObject.SetActive(true);
                    }
                }

                if (!enemy.enabled)
                {
                    Debug.LogWarning("[Respawn] composant Enemy desactive sur " + enemy.name + " : reactivation");
                    enemy.enabled = true;
                }

                Debug.Log("[Respawn] delai ecoule, respawn de " + enemy.name);
                enemy.Respawn();
            }
            catch (System.Exception ex)
            {
                Debug.LogError("[Respawn] erreur : " + ex);
            }
        }
    }
}
