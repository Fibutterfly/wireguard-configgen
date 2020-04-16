using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace VPNConfigGenerator
{
    class Program
    {
        const bool ISWINDOWSDEBUG = true;		// Csak a windowsos tesztelés miatt.
        const string KEY_PRIVATE_LOC = "keys/private";
        const string KEY_PUBLIC_LOC = "keys/public";
        const string CONFIG_LOC = "configs";
        private static Logger logger;

        static void Main(string[] args)
        {
            logger = new Logger("logger.log");
            if(args.Length == 0 || !File.Exists(args[0]))
            {
                logger.Write("Nincs paraméter vagy a megadott fájl nem létezik!", Logger.LogType.Warning);
                logger.Dispose();
                Environment.Exit(0);
            }

            logger.Write($"'{args[0]}' fájl beolvasása...");
            string[] names = File.ReadAllText(args[0]).Replace("\r", "").Split("\n");

            logger.Write($"{names.Length} db peer és hozzá tartozó kulcsok létrehozása...");
            int startIP = 2;
            int startPort = 12000;
            List<Peer> peers = new List<Peer>();

            // Server
            Peer server = new Peer() {
                Name = "Server",
                Address = "192.168.69.1/24",
                Port = startPort,
                AllowedIPs = "192.168.69.0/24",
                Endpoint = "foxy.varkovi.hu:12000",
                Table = "ValamiTableNev"
            };
            server.Keys = GetPeerKeys(server);
            peers.Add(server);
            foreach (string name in names)
            {
                Peer peer = new Peer() {
                    Name = name,
                    Address = $"192.168.69.{startIP}",
                    Port = startPort + startIP,
                    AllowedIPs = $"192.168.69.{startIP}/32"
                };
                peer.Keys = GetPeerKeys(peer);
                peers.Add(peer);
                startIP++;
                logger.Write(peer.ToString());
            }

            logger.Write("Konfig fájlok létrehozása...");
            CreateDirIfNotExist(CONFIG_LOC);
            foreach (Peer currentPeer in peers)
            {
                string confFile = $"{CONFIG_LOC}/{currentPeer.Name}.conf";
                if (File.Exists(confFile))
                    File.Delete(confFile);
                StreamWriter conf = new StreamWriter(confFile);
                conf.WriteLine(currentPeer.getAsInterface());
                foreach (Peer otherPeer in peers)
                {
                    if(currentPeer != otherPeer)
                        conf.WriteLine(otherPeer.getAsPeer());
                }
                conf.Close();
                logger.Write($"Config létrehozva: {confFile}");
            }
            logger.Write($"All done!\n");
            logger.Dispose();
        }
        static Keys GetPeerKeys(Peer peer)
        {
            Keys keys = new Keys() { 
                PrivateFile = $"{KEY_PRIVATE_LOC}/{peer.Name}.key",
                PublicFile = $"{KEY_PUBLIC_LOC}/{peer.Name}.pub"
            };
            if (!File.Exists(keys.PrivateFile))
            {
                logger.Write($"Új kulcsok generálás neki: {peer.Name}");
                CreateDirIfNotExist(KEY_PRIVATE_LOC);
                CreateDirIfNotExist(KEY_PUBLIC_LOC);
                if (File.Exists(keys.PublicFile))
                {
                    logger.Write($"Van publikus kulcs de nincs privát, WTF? Publikus kulcs törlése...", Logger.LogType.Warning);
                    if (ISWINDOWSDEBUG)
                        File.Delete(keys.PublicFile);
                    else
                        RunLinuxCommand($@"rm {keys.PublicFile}");
                }

                if (ISWINDOWSDEBUG)
                {
                    RNGCryptoServiceProvider rng = new RNGCryptoServiceProvider();

                    byte[] random1 = new Byte[32];
                    rng.GetBytes(random1);
                    StreamWriter file = new StreamWriter(keys.PrivateFile);
                    file.Write(System.Convert.ToBase64String(random1));
                    file.Close();

                    byte[] random2 = new Byte[32];
                    rng.GetBytes(random2);
                    StreamWriter file2 = new StreamWriter(keys.PublicFile);
                    file2.Write(System.Convert.ToBase64String(random2));
                    file2.Close();
                }
                else
                    RunLinuxCommand($"umask 003 && wg genkey > {keys.PrivateFile} && wg pubkey < {keys.PrivateFile} > {keys.PublicFile}");
                logger.Write($"Kulcsok legenerálva: {keys.PrivateFile} - {keys.PublicFile}");
            }
            return keys;
        }
        static void RunLinuxCommand(string cmd)
        {
            string ecmd = cmd.Replace("\"", "\\\"");
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            startInfo.FileName = "/bin/bash";
            startInfo.Arguments = $"-c \"{ecmd}\"";
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.CreateNoWindow = true;
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
            logger.Write($"A következő parancs futott le: {cmd}");
        }
        static void CreateDirIfNotExist(string path)
        {
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
                logger.Write($"Nem létező {path} mappa. Létrehozás...", Logger.LogType.Warning);
            }
        }

        // For windows testing...
        private static Random random = new Random();
        public static string RandomString(int length)
        {
            const string chars = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-.,;/,=";
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[random.Next(s.Length)]).ToArray());
        }
    }

    class Peer
    {
        public string Name { get; set; }
        public string Address { get; set; }
        public string AllowedIPs { get; set; }
        public int Port { get; set; }
        public string Endpoint { get; set; }
        public string Table { get; set; }
        public Keys Keys { get; set; }

        public override string ToString()
        {
            return base.ToString() + "\n" +
                $"Name: {this.Name}\n" +
                $"Address: {this.Address}\n" +
                $"Port: {this.Port}\n" +
                $"AllowedIPs: {this.AllowedIPs}\n" +
                $"Endpoint: {this.Endpoint}\n";
        }

        public string getAsPeer()
        {
            if (Keys == null)
                throw new Exception("Nincsenek beállítva a kulcsok!");
            return "[Peer]\n" +
            $"# Name = {this.Name}\n" +
            $"PublicKey = {this.Keys.getString(Keys.Types.Public)}\n" +
            $"AllowedIPs = {this.AllowedIPs}\n" +
            (this.Endpoint == null ? "" : $"Endpoint = {this.Endpoint}\n");
        }

        public string getAsInterface()
        {
            if (Keys == null)
                throw new Exception("Nincsenek beállítva a kulcsok!");
            return "[Interface]\n" +
            $"# Name = {this.Name}\n" +
            $"Address = {this.Address}\n" +
            $"ListenPort = {this.Port}\n" +
            $"PrivateKey = {this.Keys.getString(Keys.Types.Private)}\n" +
            (this.Table == null ? "" : $"Table = {this.Table}\n");
        }
    }

    class Keys
    {
        public enum Types { Public, Private }
        public string PrivateFile { get; set; }
        public string PublicFile { get; set; }

        public string getString(Types type)
        {
            return File.ReadAllText(type == Types.Public ? this.PublicFile : this.PrivateFile);
        }
    }

    class Logger : IDisposable
    {
        public enum LogType { Info, Warning, Error }
        private StreamWriter stream;
        public Logger(string file)
        {
            stream = new StreamWriter(file, true);
            stream.WriteLine($"-------------------------- NEW SESSION: {DateTime.Now.ToString()} --------------------------");
        }

        public void Write(string what, LogType type = LogType.Info)
        {
            string typeString = "";
            switch (type)
            {
                case LogType.Info:
                    typeString = "INFO";
                    break;
                case LogType.Warning:
                    typeString = "WARNING";
                    break;
                case LogType.Error:
                    typeString = "ERROR";
                    break;
            }
            this.stream.WriteLine($"[{typeString}]: {what}");
            Console.WriteLine($"[{typeString}]: {what}");
        }

        public void Dispose()
        {
            this.stream.Close();
        }
    }
}
