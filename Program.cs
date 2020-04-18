#nullable enable
using System;
using System.IO;
using System.Collections.Generic;

namespace configgen2
{
    class Program
    {
        public const string KEY_PRIVATE_LOC = "keys/private";
        public const string KEY_PUBLIC_LOC = "keys/public";
        const string CONFIG_LOC = "config";
        private static Logger logger = new Logger("logger.log");

        static void Main(string[] args)
        {
            List<Peer> peers = new List<Peer>();
            if (args.Length == 0 || !File.Exists(args[0]))
            {
                logger.Write("Nincs paraméter vagy a megadott fájl nem létezik!", Logger.LogType.Warning);
                logger.Dispose();
                Environment.Exit(0);
            }
            logger.Write($"'{args[0]}' fájl beolvasása...");
            logger.Write($"Kulcsok létrehozása ellenőrzése");
            IfDirectoryNotExistThanCreate("config");
            IfDirectoryNotExistThanCreate("keys");
            IfDirectoryNotExistThanCreate(KEY_PRIVATE_LOC);
            IfDirectoryNotExistThanCreate(KEY_PUBLIC_LOC);
            const int startport = 12000;
            const int startip = 1;
            StreamReader file = new StreamReader(args[0]);
            int i = 0;
            while(!file.EndOfStream)
            {
                string[] row = file.ReadLine().Split(';');
                KeyCheck(row[0]);
                Peer temp = new Peer()
                {
                    name = row[0],
                    Address = $"192.168.69.{startip+i}",
                    ListenPort = $"{startport + i}",
                    Endpoint = (row.Length > 1)?(row[1]):null
                };
                temp.init();
                i++;
                peers.Add(temp);
            }
            file.Close();
            makeConfigs(peers);
            //Console.WriteLine(i);
            logger.Dispose();
        }
        static void makeConfigs(List<Peer> peers)
        {
            for (int i = 0; i < peers.Count; i++)
            {
                StreamWriter conf = new StreamWriter($"{CONFIG_LOC}/{peers[i].name}.conf");
                conf.WriteLine(peers[i].getInterface());
                for (int q = 0; q < peers.Count; q++)
                {
                    if(i != q)
                    {
                        conf.WriteLine(peers[q].getPeer());
                    }
                }
                conf.Close();
            }
        }
        static void IfDirectoryNotExistThanCreate(string path)
        {
            logger.Write($"{path} mappa ellenőrzése");
            if (!Directory.Exists(path))
            {
                DirectoryInfo di = Directory.CreateDirectory(path);
                logger.Write($"A {path} mappa létrehozása");
            }
        }
        static bool fileCheck(string path)
        {
            logger.Write($"{path} fájl ellenőrzése");
            if (!File.Exists(path))
            {
                logger.Write($"{path} nem létezik");
                Console.WriteLine("Nem létezik");
                return false;
            }
            return true;
        }

        static void KeyCheck(string owner)
        {
            logger.Write($"{owner} privát kulcsának ellenőrzése");
            if (!fileCheck($"keys/private/{owner}.key"))
            {
                logger.Write($"{owner} privát kulcsa nem létezik");
                logger.Write($"{owner} publikus kulcsának ellenőrzése");
                if (fileCheck($"keys/public/{owner}.pub"))
                {
                    logger.Write($"{owner} publikus kulcsának törlése");
                    RunLinuxCommand($@"rm public/{owner}.pub");
                }
                RunLinuxCommand($"umask 003 && wg genkey > keys/private/{owner}.key && wg pubkey <  keys/private/{owner}.key > keys/public/{owner}.pub");
            }
        }
        static void RunLinuxCommand(string cmd)
        {
            string ecmd = cmd.Replace("\"","\\\"");
            System.Diagnostics.Process process = new System.Diagnostics.Process();
            System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo();
            //startInfo.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
            startInfo.FileName = "/bin/bash";
            startInfo.Arguments = $"-c \"{ecmd}\"";
            startInfo.UseShellExecute = false;
            startInfo.RedirectStandardOutput = true;
            startInfo.CreateNoWindow = true;
            process.StartInfo = startInfo;
            process.Start();
            process.WaitForExit();
            logger.Write($@"A következő !LINUXOS! shell parancs futott le: {cmd}");
        }
    }
    class Peer
    {
        public string name = "Hiányzik a név";
        public string Address = "Hiányzó cím";
        public string subnet = "32";
        public string PrivateKey = "Hiányzó kulcs";
        public string PublicKey = "Hiányzó kulcs";
        public string ListenPort = "Hiányzó port";
        public string? Endpoint;
        public void init()
        {
            StreamReader pri = new StreamReader($"{Program.KEY_PRIVATE_LOC}/{name}.key");
            PrivateKey = pri.ReadLine();
            pri.Close();
            StreamReader pub = new StreamReader($"{Program.KEY_PUBLIC_LOC}/{name}.pub");
            PublicKey = pub.ReadLine();
            pub.Close();
        }
        public string getPeer()
        {
            string rtn = $"[Peer]\n"
                        +$"# Name = {name}\n"
                        +$"PublicKey = {PublicKey}\n"
                        +$"AllowedIPs = {Address}/{subnet}\n";
            if (Endpoint != null)
            {
                rtn += $"Endpoint = {Endpoint}:{ListenPort}\n";
            }
            return rtn;
        }
        public string getInterface()
        {
            string rtn =$"[Interface]\n"
                       +$"# Name = {name}\n"
                       +$"Address = {Address}/{subnet}\n"
                       +$"ListenPort = {ListenPort}\n"
                       +$"PrivateKey = {PrivateKey}\n";
            return rtn;
        }
    }
    class Logger : IDisposable
    {
        public enum LogType { Info, Warning, Error }
        private StreamWriter stream;
        public Logger(string file)
        {
            this.stream = new StreamWriter(file, true);
            this.stream.AutoFlush = true;
            this.stream.WriteLine($"-------------------------- NEW SESSION: {DateTime.Now.ToString()} --------------------------");
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