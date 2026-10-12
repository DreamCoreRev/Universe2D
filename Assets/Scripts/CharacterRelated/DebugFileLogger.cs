using System;
using System.IO;
using UnityEngine;

/// <summary>
/// Copie les logs de debug dans un fichier a cote de l'exe (debug_server.log
/// ou debug_client.log), en ecrivant chaque ligne IMMEDIATEMENT sur disque.
/// Player.log, lui, est tronque tant que le processus tourne (ou s'il est
/// tue sans fermeture propre) : on y perdait la fin des sessions.
/// Ne garde que les lignes [Enemy], [Respawn] et les erreurs/exceptions.
/// </summary>
public static class DebugFileLogger
{
    private static string path;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Init()
    {
        try
        {
            string dir = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            path = Path.Combine(dir, Application.isBatchMode ? "debug_server.log" : "debug_client.log");
            File.WriteAllText(path, "=== demarrage " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                + " (pid " + System.Diagnostics.Process.GetCurrentProcess().Id + ") ===\r\n");
            Application.logMessageReceived += OnLog;
        }
        catch (Exception)
        {
            path = null;
        }
    }

    private static void OnLog(string message, string stackTrace, LogType type)
    {
        if (path == null)
        {
            return;
        }

        bool keep = type != LogType.Log && type != LogType.Warning
            || message.StartsWith("[Enemy]") || message.StartsWith("[Respawn]");

        if (!keep)
        {
            return;
        }

        try
        {
            string line = DateTime.Now.ToString("HH:mm:ss.fff") + " " + message + "\r\n";

            if (type == LogType.Exception || type == LogType.Error)
            {
                line += stackTrace + "\r\n";
            }

            File.AppendAllText(path, line);
        }
        catch (Exception)
        {
            // Fichier verrouille : on ignore, ce n'est que du debug.
        }
    }
}
