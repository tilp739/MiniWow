using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace MiniWow.Client.EditorTools
{
    /// <summary>
    /// Confort de test local : démarre Postgres (docker compose) et le serveur de jeu (dotnet run) en un
    /// clic depuis l'Éditeur, au lieu de jongler avec deux terminaux à chaque session de test. Éditeur
    /// uniquement (UnityEditor) : n'a aucun effet et n'est pas inclus dans un build du jeu.
    /// </summary>
    public static class DevBackendLauncher
    {
        private const string ServerProcessIdKey = "MiniWow.DevBackend.ServerPid";
        private const string DefaultDevPassword = "dev-local-only";

        [MenuItem("MiniWow/Dev Backend/Démarrer (Postgres + Serveur)")]
        public static void StartAll()
        {
            string repoRoot = GetRepoRoot();

            EnsureEnvFile(repoRoot);

            Debug.Log("[DevBackend] docker compose up -d ...");
            if (!RunToCompletion(repoRoot, "docker", "compose up -d", out string dockerOutput))
            {
                Debug.LogError($"[DevBackend] Échec de 'docker compose up -d'. Docker Desktop est-il lancé ?\n{dockerOutput}");
                return;
            }

            Debug.Log($"[DevBackend] Postgres démarré.\n{dockerOutput}");

            if (IsServerAlreadyRunning(out int existingPid))
            {
                Debug.Log($"[DevBackend] Le serveur de jeu tourne déjà (PID {existingPid}), pas de relance.");
                return;
            }

            StartServerProcess(repoRoot);
        }

        [MenuItem("MiniWow/Dev Backend/Arrêter le serveur")]
        public static void StopServer()
        {
            int pid = SessionState.GetInt(ServerProcessIdKey, 0);
            if (pid == 0)
            {
                Debug.Log("[DevBackend] Aucun serveur connu à arrêter (pas démarré depuis cet Éditeur, ou déjà arrêté).");
                return;
            }

            try
            {
                Process process = Process.GetProcessById(pid);
                if (!process.HasExited)
                {
                    KillProcessTree(pid); // 'dotnet run' lance l'exécutable réel en sous-processus.
                    Debug.Log($"[DevBackend] Serveur (PID {pid}) arrêté.");
                }
            }
            catch (ArgumentException)
            {
                // Le process n'existe déjà plus : rien à faire.
            }
            finally
            {
                SessionState.EraseInt(ServerProcessIdKey);
            }
        }

        [MenuItem("MiniWow/Dev Backend/Arrêter Postgres (docker compose down)")]
        public static void StopPostgres()
        {
            string repoRoot = GetRepoRoot();
            if (RunToCompletion(repoRoot, "docker", "compose down", out string output))
            {
                Debug.Log($"[DevBackend] Postgres arrêté.\n{output}");
            }
            else
            {
                Debug.LogError($"[DevBackend] Échec de 'docker compose down'.\n{output}");
            }
        }

        /// <summary>
        /// Tue le process et ses enfants. Process.Kill(bool) (.NET 5+) n'est pas disponible au niveau de
        /// compatibilité d'API de ce projet : on passe par 'taskkill /T' (Windows), qui couvre aussi
        /// l'exécutable réel que 'dotnet run' lance en sous-processus.
        /// </summary>
        private static void KillProcessTree(int pid)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "taskkill",
                Arguments = $"/PID {pid} /T /F",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using Process killer = Process.Start(startInfo);
            killer?.WaitForExit();
        }

        private static bool IsServerAlreadyRunning(out int pid)
        {
            pid = SessionState.GetInt(ServerProcessIdKey, 0);
            if (pid == 0)
            {
                return false;
            }

            try
            {
                return !Process.GetProcessById(pid).HasExited;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private static void StartServerProcess(string repoRoot)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "run --project Server/GameServer",
                WorkingDirectory = repoRoot,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            process.OutputDataReceived += (_, e) => { if (e.Data != null) Debug.Log($"[GameServer] {e.Data}"); };
            process.ErrorDataReceived += (_, e) => { if (e.Data != null) Debug.LogWarning($"[GameServer] {e.Data}"); };

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                Debug.LogError($"[DevBackend] Impossible de lancer 'dotnet run' : {ex.Message}");
                return;
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            SessionState.SetInt(ServerProcessIdKey, process.Id);
            Debug.Log($"[DevBackend] Serveur de jeu lancé (PID {process.Id}). Écoute attendue sur 127.0.0.1:7777 dans quelques secondes (compilation + migrations).");
        }

        /// <summary>Crée un .env local de test si absent, pour que 'docker compose up' fonctionne sans étape manuelle.</summary>
        private static void EnsureEnvFile(string repoRoot)
        {
            string envPath = Path.Combine(repoRoot, ".env");
            if (File.Exists(envPath))
            {
                return;
            }

            string examplePath = Path.Combine(repoRoot, ".env.example");
            string content = File.Exists(examplePath)
                ? File.ReadAllText(examplePath)
                : "MINIWOW_DB_PASSWORD=change-me-in-local-env\n";

            content = content.Replace("change-me-in-local-env", DefaultDevPassword);
            File.WriteAllText(envPath, content);
            Debug.LogWarning($"[DevBackend] .env créé automatiquement pour les tests locaux (mot de passe '{DefaultDevPassword}'). Fichier ignoré par Git, à ne jamais utiliser en production.");
        }

        private static bool RunToCompletion(string workingDirectory, string fileName, string arguments, out string output)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = startInfo };
            var builder = new StringBuilder();

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                output = ex.Message;
                return false;
            }

            builder.Append(process.StandardOutput.ReadToEnd());
            builder.Append(process.StandardError.ReadToEnd());
            process.WaitForExit();

            output = builder.ToString();
            return process.ExitCode == 0;
        }

        /// <summary>Application.dataPath = ".../Client/UnityProject/Assets" -> racine du dépôt 3 niveaux au-dessus.</summary>
        private static string GetRepoRoot()
        {
            var assets = new DirectoryInfo(Application.dataPath);
            DirectoryInfo repoRoot = assets.Parent?.Parent?.Parent;
            if (repoRoot == null)
            {
                throw new InvalidOperationException("Impossible de localiser la racine du dépôt depuis Application.dataPath.");
            }

            return repoRoot.FullName;
        }
    }
}
