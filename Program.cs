using System;
using System.IO;
using System.Collections.Generic;

namespace configgen2
{
    class Program
    {
        static void MakeLog(string what)
        {
            StreamWriter log = new StreamWriter("log.txt", append: true);
            log.WriteLine($"{DateTime.Now.ToString("yyyy:MM:dd-HH:mm:ss")}: {what}");
            log.Close();
        }
        static bool fileCheck(string path)
        {
            MakeLog($"{path} fájl ellenőrzése");
            if (!File.Exists(path))
            {
                MakeLog($"{path} nem létezik");
                Console.WriteLine("Nem létezik");
                return false;
            }
            return true;
        }
        static string[] ReadFile(string path)
        {
            MakeLog($"{path} beolvasása");
            StreamReader file = new StreamReader(path);
            List<string> lines = new List<string>();
            while(!file.EndOfStream)
            {
                lines.Add(file.ReadLine());
            }
            file.Close();
            return lines.ToArray();
        }
        static bool TblCheck(string[] tbl)
        {
            MakeLog("A konfig tartalmának ellenőrzése");
            if (tbl.Length < 1)
            {
                MakeLog("A konfigban nem volt adat");
                Console.WriteLine("A konfigban nincs adat");
                return false;
            }
            return true;
        }
        static void IfDirectoryNotExistThanCreate(string path)
        {
            MakeLog($"{path} mappa ellenőrzése");
            if (!Directory.Exists(path))
            {
                DirectoryInfo di = Directory.CreateDirectory(path);
                MakeLog($"A {path} mappa létrehozása");
            }
        }
        static void CheckKeyFolder()
        {
            string[] folders = {"keys","keys/public","keys/private"};
            for (int i = 0; i < folders.Length; i++)
            {
                IfDirectoryNotExistThanCreate(folders[i]);
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
            MakeLog($@"A következő !LINUXOS! shell parancs futott le: {cmd}");
        }
       
        static void KeyCheck(string owner)
        {
            MakeLog($"{owner} privát kulcsának ellenőrzése");
            if (!fileCheck($"keys/private/{owner}.key"))
            {
                MakeLog($"{owner} privát kulcsa nem létezik");
                MakeLog($"{owner} publikus kulcsának ellenőrzése");
                if (fileCheck($"keys/public/{owner}.pub"))
                {
                    MakeLog($"{owner} publikus kulcsának törlése");
                    RunLinuxCommand($@"rm public/{owner}.pub");
                }
                RunLinuxCommand($"umask 003 && wg genkey > keys/private/{owner}.key && wg pubkey <  keys/private/{owner}.key > keys/public/{owner}.pub");
            }
        }
        static string[,] IPtable(string[] names)
        {
            string[,] rtn = new string[names.Length,4];
            for (int i = 0; i < names.Length; i++)
            {
                rtn[i,0] = names[i];
                rtn[i,1] = $"192.168.69.{i+1}";
                string[] helper = ReadFile($"keys/private/{names[i]}.key");
                rtn[i,2] = helper[0];
                helper = ReadFile($"keys/public/{names[i]}.pub");
                rtn[i,3] = helper[0];
            }
            return rtn;
        }
        static void makeServerConfig(string[,] datas)
        {
            MakeLog("Szerver konfig generálása");
            StreamWriter writer = new StreamWriter("config/Szerver.conf");
            writer.WriteLine(interfaceGen(datas[0, 0], datas[0, 1], datas[0, 2]).Replace("/32","/24"));
            for (int q = 1; q < datas.GetLength(0); q++)
            {
                writer.WriteLine(clientPeerGen(datas[q, 0], datas[q, 1], datas[q, 3]));
                MakeLog($"{datas[q,0]} szerverrevaló felcsatlakozásának előkészítése");
            }
            writer.Close();
        }
        static void makeConfigFiles(string[,] ipTable)
        {
            string serverPeer = serverPeerGen(ipTable[0,3]);
            makeServerConfig(ipTable);
            //Console.WriteLine(serverPeer);
            for (int i = 1; i < ipTable.GetLength(0); i++)
            {
                StreamWriter ncf = new StreamWriter($"config/{ipTable[i,0]}.conf");
                MakeLog($"config/{ipTable[i,0]}.conf fájl előkészítése");
                ncf.WriteLine(interfaceGen(ipTable[i,0],ipTable[i,1],ipTable[i,2]));
                ncf.WriteLine(serverPeer);
                for (int q = 1; q < ipTable.GetLength(0); q++)
                {
                    if(i != q)
                    {
                        MakeLog($"P2P előkészítése {ipTable[i,0]} <-> {ipTable[q,0]}");
                        ncf.WriteLine(clientPeerGen(ipTable[q,0],ipTable[q,1],ipTable[q,3]));
                    }
                }
                ncf.Close();
            }
        }
        static string clientPeerGen(string name, string ip, string key)
        {
            string rtn = "[Peer]\n"+
                        $"# Name = {name}\n"+
                        $"PublicKey = {key}\n"+
                        $"AllowedIPs = {ip}/32\n";
            return rtn;
        }
        static string interfaceGen(string name, string ip, string key)
        {
            MakeLog($"interface generálása {name} részére");
            string rtn = "[Interface]\n"+
                        $"# Name = {name}\n"+
                        $"Address = {ip}/32\n"+
                        "ListenPort = 12000\n"+
                        $"PrivateKey = {key}\n";
            return rtn;
        }
        static string serverPeerGen(string server)
        {
           MakeLog("Szerver peer létrehozása");
           string rtn = "[Peer]\n"+
                        "# Name = Szerver\n"+
                        $"PublicKey = {server}\n"+
                        "AllowedIPs = 192.168.69.0/24\n"+
                        "Endpoint = foxy.varkovi.hu:12000\n";
           return rtn; 
        }
        static void Main(string[] args)
        {
            if(args.GetLength(0) == 0)
            {
                Console.WriteLine("Nincs paraméter");
                Environment.Exit(0);
            }
            string userlist = args[0];
            if(!fileCheck(userlist))
            {
                Environment.Exit(0);
            }
            string[] names = ReadFile(userlist);
            if (!TblCheck(names))
            {
                Environment.Exit(0);
            }
            CheckKeyFolder();
            for (int i = 0; i < names.Length; i++)
            {
                KeyCheck(names[i]);
            }
            IfDirectoryNotExistThanCreate("config");
            string[,] ipTable = IPtable(names);
            makeConfigFiles(ipTable);
        }
    }
}