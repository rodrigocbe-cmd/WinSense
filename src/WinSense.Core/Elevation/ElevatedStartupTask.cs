using System.Diagnostics;
using System.Security;
using System.Security.Principal;

namespace WinSense.Core.Elevation;

/// <summary>
/// Opens WinSense as administrator without the UAC prompt, through a Task Scheduler task set to
/// "run with highest privileges". Only an elevated process can create such a task, so WinSense
/// creates it the first time it runs elevated (e.g. from the installer's last page).
/// Tasks are only created for an executable in a folder that standard users cannot modify
/// (Program Files); otherwise anyone could swap the exe and get admin rights without a prompt.
/// The uninstaller deletes every task in the \WinSense\ folder.
/// </summary>
public static class ElevatedStartupTask
{
    public const string TaskFolder = @"\WinSense\";

    public static bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    /// <summary>One task per user, so each runs in its owner's session.</summary>
    public static string TaskName => $"{TaskFolder}Abrir WinSense ({ScheduledTasks.CurrentUserSid})";

    /// <summary>Starts the elevated copy through the task. False if the task does not exist (or failed).</summary>
    public static bool TryRun() => ScheduledTasks.Run(TaskName);

    /// <summary>Creates or refreshes the task for the current user. Call only from an elevated process.</summary>
    public static void EnsureCreated(string exePath)
    {
        if (!IsProtectedLocation(exePath))
            return;

        ScheduledTasks.Create(TaskName, BuildTaskXml(ScheduledTasks.CurrentUserSid, exePath,
            "Abre o WinSense como administrador sem o aviso do UAC. Criada pelo WinSense e removida ao desinstalá-lo."));
    }

    /// <summary>Program Files (64 or 32 bit): only administrators can write there.</summary>
    public static bool IsProtectedLocation(string exePath)
    {
        var full = Path.GetFullPath(exePath);
        return new[]
            {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
            }
            .Where(dir => !string.IsNullOrEmpty(dir))
            .Any(dir => full.StartsWith(dir.TrimEnd('\\') + "\\", StringComparison.OrdinalIgnoreCase));
    }

    /// <param name="arguments">Command-line arguments for WinSense, or null.</param>
    /// <param name="atLogon">Also start the task when this user signs in.</param>
    public static string BuildTaskXml(string userSid, string exePath, string description,
        string? arguments = null, bool atLogon = false)
    {
        var sid = SecurityElement.Escape(userSid);
        // A short delay lets the desktop and the Bluetooth stack come up first.
        var triggers = atLogon
            ? $"""
              <Triggers>
                <LogonTrigger>
                  <UserId>{sid}</UserId>
                  <Delay>PT5S</Delay>
                </LogonTrigger>
              </Triggers>
            """
            : "";
        var args = arguments is null ? "" : $"<Arguments>{SecurityElement.Escape(arguments)}</Arguments>";

        return $"""
            <?xml version="1.0" encoding="UTF-16"?>
            <Task version="1.2" xmlns="http://schemas.microsoft.com/windows/2004/02/mit/task">
              <RegistrationInfo>
                <Description>{SecurityElement.Escape(description)}</Description>
              </RegistrationInfo>
            {triggers}
              <Principals>
                <Principal id="Author">
                  <UserId>{sid}</UserId>
                  <LogonType>InteractiveToken</LogonType>
                  <RunLevel>HighestAvailable</RunLevel>
                </Principal>
              </Principals>
              <Settings>
                <MultipleInstancesPolicy>Parallel</MultipleInstancesPolicy>
                <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
                <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
                <ExecutionTimeLimit>PT0S</ExecutionTimeLimit>
                <AllowHardTerminate>true</AllowHardTerminate>
                <StartWhenAvailable>false</StartWhenAvailable>
                <Priority>4</Priority>
              </Settings>
              <Actions Context="Author">
                <Exec>
                  <Command>"{SecurityElement.Escape(exePath)}"</Command>
                  {args}
                </Exec>
              </Actions>
            </Task>
            """;
    }
}

public interface IStartWithWindows
{
    /// <summary>False for the portable version (outside Program Files), where it would be unsafe.</summary>
    bool IsAvailable { get; }

    bool IsEnabled();

    void SetEnabled(bool enabled);
}

/// <summary>
/// Starts WinSense when the user signs in, already elevated (no UAC prompt) and hidden in the
/// notification area, through a logon-triggered task. Needs administrator rights to change.
/// </summary>
public sealed class StartWithWindows(string exePath) : IStartWithWindows
{
    /// <summary>Command-line argument: start hidden in the notification area.</summary>
    public const string BackgroundArgument = "--background";

    public static string TaskName => $"{ElevatedStartupTask.TaskFolder}Iniciar com o Windows ({ScheduledTasks.CurrentUserSid})";

    public bool IsAvailable => ElevatedStartupTask.IsProtectedLocation(exePath);

    public bool IsEnabled() => ScheduledTasks.Exists(TaskName);

    public void SetEnabled(bool enabled)
    {
        if (!enabled)
        {
            ScheduledTasks.Delete(TaskName);
            return;
        }

        if (!IsAvailable)
            throw new InvalidOperationException("Disponível só na versão instalada do WinSense.");

        ScheduledTasks.Create(TaskName, ElevatedStartupTask.BuildTaskXml(ScheduledTasks.CurrentUserSid, exePath,
            "Inicia o WinSense com o Windows, nos ícones ocultos. Criada pelo WinSense e removida ao desinstalá-lo.",
            BackgroundArgument, atLogon: true));
    }
}

internal static class ScheduledTasks
{
    public static string CurrentUserSid
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.User?.Value ?? throw new InvalidOperationException("Usuário sem SID.");
        }
    }

    public static bool Run(string name) => Schtasks($"/run /tn \"{name}\"") == 0;

    public static bool Exists(string name) => Schtasks($"/query /tn \"{name}\"") == 0;

    public static void Delete(string name)
    {
        if (Exists(name) && Schtasks($"/delete /tn \"{name}\" /f") != 0)
            throw new InvalidOperationException("O Agendador de Tarefas recusou a remoção da tarefa.");
    }

    public static void Create(string name, string xml)
    {
        var xmlPath = Path.Combine(Path.GetTempPath(), $"winsense-task-{Environment.ProcessId}-{Guid.NewGuid():N}.xml");
        try
        {
            // schtasks wants the declared encoding to match the file: UTF-16.
            File.WriteAllText(xmlPath, xml, System.Text.Encoding.Unicode);
            if (Schtasks($"/create /tn \"{name}\" /xml \"{xmlPath}\" /f") != 0)
                throw new InvalidOperationException("O Agendador de Tarefas recusou a criação da tarefa.");
        }
        finally
        {
            File.Delete(xmlPath);
        }
    }

    private static int Schtasks(string arguments)
    {
        try
        {
            using var process = Process.Start(new ProcessStartInfo("schtasks.exe", arguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            });
            if (process is null)
                return -1;
            process.StandardOutput.ReadToEnd();
            process.StandardError.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode;
        }
        catch (Exception)
        {
            return -1;
        }
    }
}
