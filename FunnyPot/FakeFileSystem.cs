using System.Text;

class FakeFileSystem
{
    private static readonly Dictionary<string, FakeFileSystem> SessionFilesystems = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object _lock = new();

    private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, SortedSet<string>> _directories = new(StringComparer.OrdinalIgnoreCase);

    public string CurrentDirectory { get; private set; } = "/home/remote";

    private FakeFileSystem()
    {
        SeedDirectories();
        SeedFiles();
    }

    public static FakeFileSystem GetOrCreate(string sessionId)
    {
        lock (_lock)
        {
            if (!SessionFilesystems.TryGetValue(sessionId, out var fs))
            {
                fs = new FakeFileSystem();
                SessionFilesystems[sessionId] = fs;
            }
            return fs;
        }
    }

    public static void Remove(string sessionId)
    {
        lock (_lock)
        {
            SessionFilesystems.Remove(sessionId);
        }
    }

    public string ResolvePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return CurrentDirectory;

        path = StripShellNoise(path);
        if (path == "~") return "/home/remote";
        if (path.StartsWith("~/", StringComparison.Ordinal))
            return NormalizePath("/home/remote/" + path[2..]);
        if (path.StartsWith('/'))
            return NormalizePath(path);
        if (path == ".")
            return CurrentDirectory;

        var combined = CurrentDirectory == "/" ? $"/{path}" : $"{CurrentDirectory}/{path}";
        return NormalizePath(combined);
    }

    public void ChangeDirectory(string path)
    {
        var resolved = ResolvePath(path);
        if (IsValidDirectory(resolved) || LooksLikeDirectoryPath(resolved))
        {
            EnsureDirectory(resolved);
            CurrentDirectory = resolved;
        }
    }

    public bool IsValidDirectory(string path)
    {
        var resolved = ResolvePath(path);
        return _directories.ContainsKey(resolved)
            || resolved.StartsWith("/home/", StringComparison.Ordinal)
            || resolved.StartsWith("/tmp/", StringComparison.Ordinal)
            || resolved.StartsWith("/run/", StringComparison.Ordinal)
            || resolved.StartsWith("/opt/", StringComparison.Ordinal)
            || resolved.StartsWith("/srv/", StringComparison.Ordinal)
            || resolved.StartsWith("/var/", StringComparison.Ordinal)
            || resolved.StartsWith("/usr/local/", StringComparison.Ordinal);
    }

    public string ListDirectory(string? path = null)
    {
        var dir = path is null ? CurrentDirectory : ResolvePath(path);
        if (!_directories.TryGetValue(dir, out var entries))
            return "";

        return string.Join("  ", entries);
    }

    public bool FileExists(string path)
    {
        var resolved = ResolvePath(path);
        if (_files.ContainsKey(resolved))
            return true;

        return IsBinaryPath(resolved)
            || resolved.StartsWith("/etc/", StringComparison.Ordinal)
            || resolved.StartsWith("/opt/", StringComparison.Ordinal)
            || resolved.StartsWith("/srv/", StringComparison.Ordinal)
            || resolved.StartsWith("/var/log", StringComparison.Ordinal)
            || resolved.StartsWith("/var/www/", StringComparison.Ordinal)
            || resolved.StartsWith("/var/lib/", StringComparison.Ordinal)
            || resolved.StartsWith("/usr/local/", StringComparison.Ordinal)
            || resolved.StartsWith("/home/remote/", StringComparison.Ordinal)
            || resolved.StartsWith("/tmp/", StringComparison.Ordinal)
            || resolved.StartsWith("/run/", StringComparison.Ordinal);
    }

    public string? ReadFile(string path)
    {
        var resolved = ResolvePath(path);
        if (_files.TryGetValue(resolved, out var content))
            return content;

        if (IsBinaryPath(resolved))
            return null;

        if (FileExists(resolved))
            return SyntheticFileContent(resolved);

        return null;
    }

    public void WriteFile(string path, string content, bool append = false)
    {
        var resolved = ResolvePath(path);
        EnsureDirectory(ParentDirectory(resolved));
        _files[resolved] = append && _files.TryGetValue(resolved, out var existing)
            ? existing + content
            : content;
        AddEntry(ParentDirectory(resolved), FileName(resolved));
    }

    public void Touch(string path)
    {
        var resolved = ResolvePath(path);
        EnsureDirectory(ParentDirectory(resolved));
        _files.TryAdd(resolved, "");
        AddEntry(ParentDirectory(resolved), FileName(resolved));
    }

    public void CreateDirectory(string path)
    {
        EnsureDirectory(ResolvePath(path));
    }

    public void RemovePath(string path)
    {
        var resolved = ResolvePath(path);
        _files.Remove(resolved);
        _directories.Remove(resolved);
        if (_directories.TryGetValue(ParentDirectory(resolved), out var parent))
            parent.Remove(FileName(resolved));
    }

    public void Copy(string source, string destination)
    {
        var src = ResolvePath(source);
        var dest = ResolvePath(destination);
        if (IsValidDirectory(dest))
            dest = NormalizePath($"{dest}/{FileName(src)}");

        WriteFile(dest, ReadFile(src) ?? SyntheticFileContent(src));
    }

    public void Move(string source, string destination)
    {
        Copy(source, destination);
        RemovePath(source);
    }

    private void SeedDirectories()
    {
        AddDirectory("/", "bin", "boot", "dev", "etc", "home", "lib", "lib64", "media", "mnt", "opt", "proc", "root", "run", "sbin", "srv", "sys", "tmp", "usr", "var");
        AddDirectory("/home", "remote", "secretOps");
        AddDirectory("/home/remote", "Desktop", "Documents", "Downloads", "Music", "Pictures", "Public", "Templates", "Videos", "projects", "notes", "repos", "archive", "backup", "work", "personal");
        AddDirectory("/home/secretOps", ".bash_history", ".bashrc", ".profile", ".ssh", "archive", "credentials.json", "deploy.sh", "downloads", "mission_brief.txt", "notes", "projects", "runbook.md", "work", ".env");
        AddDirectory("/etc", "adduser.conf", "apt", "cron.allow", "cron.d", "cron.daily", "cron.hourly", "crontab", "default", "dpkg", "environment", "fstab", "group", "hostname", "hosts", "init.d", "issue", "ld.so.conf", "login.defs", "motd", "mtab", "network", "nsswitch.conf", "opt", "os-release", "passwd", "profile", "resolv.conf", "security", "shadow", "ssh", "sudoers", "sysctl.conf", "timezone");
        AddDirectory("/root", ".bash_history", ".bashrc", ".cache", ".config", ".docker", ".gnupg", ".kube", ".profile", ".ssh", ".aws", "archive", "backup", "downloads", "notes.txt", "projects", "repos", "scripts", "todo.md");
        AddDirectory("/var", "backups", "cache", "crash", "lib", "local", "lock", "log", "mail", "opt", "run", "spool", "tmp", "www");
        AddDirectory("/var/log", "alternatives.log", "apt", "auth.log", "btmp", "cron.log", "daemon.log", "dmesg", "dpkg.log", "fail2ban.log", "kern.log", "lastlog", "messages", "mysql", "nginx", "redis", "syslog", "user.log", "wtmp");
        AddDirectory("/var/log/nginx", "access.log", "error.log");
        AddDirectory("/var/log/mysql", "error.log", "slow.log");
        AddDirectory("/var/log/apt", "history.log", "term.log");
        AddDirectory("/tmp", "systemd-private-abc123", "vmware-dragon");
        AddDirectory("/run", "lock", "network", "sshd", "systemd", "user");
        AddDirectory("/bin", "bash", "cat", "chmod", "cp", "date", "dd", "df", "echo", "false", "ln", "ls", "mkdir", "mv", "pwd", "rm", "rmdir", "sh", "sleep", "sort", "stat", "true", "uname");
        AddDirectory("/usr", "bin", "local", "sbin", "lib", "share");
        AddDirectory("/usr/bin", "python3", "python", "curl", "wget", "git", "gcc", "make", "perl", "ruby", "node", "php");
        AddDirectory("/sbin", "agetty", "fsck", "ifconfig", "ip", "fdisk");
        AddDirectory("/usr/sbin", "adduser", "chroot", "cron", "useradd", "userdel");
        AddDirectory("/proc", "1", "cpuinfo", "meminfo", "mounts", "stat", "uptime", "version");
        AddDirectory("/opt", "app", "legacy", "monitoring", "scripts", "third-party", "vendor");
        AddDirectory("/opt/app", "bin", "config.yml", "data", "deploy.sh", "logs", "secrets.json", ".env");
        AddDirectory("/opt/monitoring", "grafana", "prometheus", "alertmanager");
        AddDirectory("/srv", "backup", "data", "ftp", "git", "logs", "mail", "nfs", "samba", "www");
        AddDirectory("/srv/www", "html");
        AddDirectory("/srv/www/html", "index.html", "admin", "api", "uploads", ".htaccess");
        AddDirectory("/usr/local", "bin", "etc", "include", "lib", "man", "sbin", "share", "src");
        AddDirectory("/var/www", "html", "cgi-bin");
        AddDirectory("/var/www/html", "index.html", "admin", "uploads", "wp-config.php", ".htaccess");
        AddDirectory("/var/lib", "apt", "dpkg", "docker", "mongodb", "mysql", "postgresql", "redis", "systemd");
    }

    private void SeedFiles()
    {
        _files["/etc/passwd"] = "root:x:0:0:root:/root:/bin/bash\ndaemon:x:1:1:daemon:/usr/sbin:/usr/sbin/nologin\nbin:x:2:2:bin:/bin:/usr/sbin/nologin\nremote:x:1001:1001:,,,:/home/remote:/bin/bash\nsecretOps:x:1002:1001:,,,:/home/secretOps:/bin/bash";
        _files["/etc/shadow"] = "root:$6$rounds=656000$YqXrHvkz$H7b2Kl3mPnQ9rStUvWxYzAbCdEfGhIjKlMnOpQrStUv:19000:0:99999:7:::\nremote:$6$rounds=656000$AbCdEfGh$IjKlMnOpQrStUvWxYz0123456789AbCdEfGhIjKlMn:19000:0:99999:7:::\nsecretOps:$6$rounds=656000$QrStUvWx$YzAbCdEfGhIjKlMnOpQrStUvWxYzAbCdEfGhIjKl:19000:0:99999:7:::";
        _files["/etc/group"] = "root:x:0:\nusers:x:100:\nsecretOps:x:1001:secretOps\nsudo:x:27:remote";
        _files["/etc/hosts"] = "127.0.0.1   localhost\n127.0.1.1   omegablack";
        _files["/etc/hostname"] = "omegablack";
        _files["/etc/resolv.conf"] = "nameserver 8.8.8.8\nnameserver 8.8.4.4";
        _files["/etc/os-release"] = "PRETTY_NAME=\"Debian GNU/Linux 6.0.10 (squeeze)\"\nNAME=\"Debian GNU/Linux\"\nVERSION_ID=\"6.0.10\"\nVERSION=\"6.0.10 (squeeze)\"\nID=debian";
        _files["/etc/debian_version"] = "6.0.10";
        _files["/etc/issue"] = "Debian GNU/Linux 6 \\n \\l";
        _files["/etc/motd"] = "Warning: This is a classified system. All activity is monitored.\n";
        _files["/etc/fstab"] = "# /etc/fstab: static file system information.\nproc  /proc  proc  defaults  0  0\nUUID=ab12cd34-ef56-7890-abcd-ef1234567890  /  ext4  errors=remount-ro  0  1";
        _files["/etc/sudoers"] = "Defaults        env_reset\nroot    ALL=(ALL:ALL) ALL\n%sudo   ALL=(ALL:ALL) ALL";
        _files["/etc/profile"] = "# /etc/profile: system-wide .profile file\nif [ \"$PS1\" ]; then\n  if [ \"$BASH\" ]; then\n    PS1='\\u@\\h:\\w\\$ '\n  fi\nfi\numask 022";
        _files["/etc/environment"] = "PATH=\"/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin\"";
        _files["/etc/timezone"] = "Etc/UTC";
        _files["/etc/ssh/sshd_config"] = "Port 22\nPermitRootLogin no\nPasswordAuthentication yes\nPubkeyAuthentication yes\nSubsystem sftp /usr/lib/openssh/sftp-server";
        _files["/home/secretOps/.env"] = "AWS_ACCESS_KEY_ID=AKIAIOSFODNN7EXAMPLE\nAWS_SECRET_ACCESS_KEY=wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY\nDB_PASSWORD=s3cr3t!Vault99";
        _files["/home/secretOps/mission_brief.txt"] = "NIGHTFALL OPERATION - CLASSIFIED\n\nOperation: NIGHTFALL\nClassification: TOP SECRET\nStatus: ACTIVE\n\nObjective: Establish covert access to primary targets.\nContact: Use encrypted channel. Key ID: NIGHTFALL-2024-X9";
        _files["/root/.ssh/id_rsa"] = "-----BEGIN RSA PRIVATE KEY-----\nMIIEogIBAAJAKhP4n3M...\n-----END RSA PRIVATE KEY-----";
    }

    private void AddDirectory(string path, params string[] entries)
    {
        EnsureDirectory(path);
        foreach (var entry in entries)
            AddEntry(path, entry);
    }

    private void EnsureDirectory(string path)
    {
        var resolved = NormalizePath(path);
        if (_directories.ContainsKey(resolved))
            return;

        _directories[resolved] = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
        if (resolved != "/")
            AddEntry(ParentDirectory(resolved), FileName(resolved));
    }

    private void AddEntry(string directory, string entry)
    {
        var dir = NormalizePath(directory);
        if (!_directories.TryGetValue(dir, out var entries))
        {
            entries = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            _directories[dir] = entries;
        }
        entries.Add(entry);
    }

    private static string NormalizePath(string path)
    {
        var parts = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var result = new List<string>();
        foreach (var part in parts)
        {
            if (part == "..")
            {
                if (result.Count > 0) result.RemoveAt(result.Count - 1);
            }
            else if (part != ".")
            {
                result.Add(part);
            }
        }
        return result.Count == 0 ? "/" : "/" + string.Join("/", result);
    }

    private static string ParentDirectory(string path)
    {
        if (path == "/") return "/";
        var slash = path.LastIndexOf('/');
        return slash <= 0 ? "/" : path[..slash];
    }

    private static string FileName(string path)
    {
        if (path == "/") return "/";
        var slash = path.LastIndexOf('/');
        return slash < 0 ? path : path[(slash + 1)..];
    }

    private static string StripShellNoise(string value)
    {
        var clean = value.Trim().Trim('"', '\'');
        var redirect = clean.IndexOfAny(new[] { '>', '<', '|', ';', '&' });
        return redirect >= 0 ? clean[..redirect].Trim() : clean;
    }

    private static bool LooksLikeDirectoryPath(string path)
    {
        var name = FileName(path);
        return !name.Contains('.') || path.StartsWith("/tmp/", StringComparison.Ordinal) || path.StartsWith("/run/", StringComparison.Ordinal);
    }

    private static bool IsBinaryPath(string path)
    {
        return path.StartsWith("/bin/", StringComparison.Ordinal)
            || path.StartsWith("/sbin/", StringComparison.Ordinal)
            || path.StartsWith("/usr/bin/", StringComparison.Ordinal)
            || path.StartsWith("/usr/sbin/", StringComparison.Ordinal)
            || path.StartsWith("/usr/local/bin/", StringComparison.Ordinal)
            || path.StartsWith("/usr/local/sbin/", StringComparison.Ordinal)
            || path.StartsWith("/lib/", StringComparison.Ordinal)
            || path.StartsWith("/lib64/", StringComparison.Ordinal)
            || path.StartsWith("/usr/lib/", StringComparison.Ordinal)
            || path.StartsWith("/usr/lib64/", StringComparison.Ordinal);
    }

    private static string SyntheticFileContent(string path)
    {
        if (path == "/proc/uptime") return SyntheticHostClock.FormatProcUptime();
        if (path == "/proc/version") return Program.KernelProcVersion;
        if (path == "/proc/loadavg") return "0.42 0.31 0.27 1/234 5678";
        if (path == "/proc/meminfo") return "MemTotal:        4048460 kB\nMemFree:          234112 kB\nMemAvailable:    1845632 kB\nBuffers:          188204 kB\nCached:          1823456 kB";

        var name = FileName(path);
        if (name.EndsWith(".log", StringComparison.OrdinalIgnoreCase))
            return $"{DateTime.UtcNow:MMM dd HH:mm:ss} omegablack kernel: audit: {name} rotated\n{DateTime.UtcNow:MMM dd HH:mm:ss} omegablack sshd[1842]: Accepted password for remote from 192.168.1.100 port 54211 ssh2";
        if (name.EndsWith(".sh", StringComparison.OrdinalIgnoreCase))
            return "#!/bin/sh\n# recovered deployment helper\nPATH=/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin\n";
        if (name.EndsWith(".conf", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".yml", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".yaml", StringComparison.OrdinalIgnoreCase))
            return "enabled: true\nhost: omegablack\nenvironment: legacy\n";
        if (name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            return "{\"host\":\"omegablack\",\"environment\":\"legacy\",\"enabled\":true}";

        var builder = new StringBuilder();
        builder.AppendLine($"# {name}");
        builder.AppendLine("Generated maintenance artifact for omegablack.");
        builder.Append("status=active");
        return builder.ToString();
    }
}
