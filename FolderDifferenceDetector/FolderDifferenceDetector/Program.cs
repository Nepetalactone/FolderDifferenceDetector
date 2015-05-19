using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;

namespace FolderDifferenceDetector
{
    class Program
    {
        static void Main(string[] args)
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();
            FolderDifferenceDetector detector = new FolderDifferenceDetector(args[0], args[1]);
            var differences = detector.DetectMissingElementsInSecondDir();
            Console.Clear();
            foreach (var difference in differences)
            {
                Console.WriteLine("MasterFile: " + difference.SourceFile + "\nTargetFile: " + difference.TargetFile);
                Console.WriteLine(Environment.NewLine);
            }
            watch.Stop();
            Console.Write(Environment.NewLine);
            Console.WriteLine("Scan done in " + watch.ElapsedMilliseconds + " milliseconds");
            Console.WriteLine("Upload differences? y/n");
            if (Console.ReadKey().Key.ToString().ToLower() == "y")
            {
                watch.Reset();
                watch.Start();
                UploadToFtp(differences, args[2], args[3]);
                watch.Stop();
                Console.WriteLine("Upload done in " + watch.ElapsedMilliseconds + " milliseconds");
                Console.ReadKey();
            }
        }

        static void UploadToFtp(IEnumerable<MissingFile> files, string username, string password)
        {
            NetworkCredential credential = new NetworkCredential(username, password);
            using (WebClient client = new WebClient())
            {
                client.Credentials = credential;
                foreach (var file in files)
                {
                    String remoteDirectory = Path.GetDirectoryName(file.TargetFile);

                    //Start with root directory first
                    foreach (var dir in GetDirectoryStructure(remoteDirectory))
                    {
                        if (!Directory.Exists(dir))
                        {
                            WebRequest request = WebRequest.Create("ftp:/" + dir.Replace("\\", "/"));
                            request.Method = WebRequestMethods.Ftp.MakeDirectory;
                            request.Credentials = credential;
                            using (var response = (FtpWebResponse) request.GetResponse())
                            {
                                if (response.StatusCode != FtpStatusCode.PathnameCreated)
                                {
                                    Console.WriteLine(response.StatusCode);
                                }
                            }
                        }
                    }
                    try
                    {
                        WebRequest request = WebRequest.Create("ftp:" + file.TargetFile.Replace("\\", "/"));
                        request.Method = WebRequestMethods.Ftp.UploadFile;
                        request.Credentials = credential;

                        using (var response = (FtpWebResponse) request.GetResponse())
                        {
                            if (response.StatusCode != FtpStatusCode.FileActionOK)
                            {
                                Console.WriteLine(response.StatusCode);
                            }
                        }
                    }
                    catch (WebException w)
                    {
                        Console.WriteLine("Couldn't upload file: {0}\n{1}\n{2}", file.SourceFile, w.Status, w.Message);
                    }
                }
                Console.WriteLine("Done");
            }
        }

        /// <summary>
        /// Gets full pathnames of all parent directories
        /// </summary>
        /// <param name="directory"></param>
        /// <returns></returns>
        static IEnumerable<string> GetDirectoryStructure(String directory)
        {
            DirectoryInfo dir = new DirectoryInfo(directory);
            List<String> parentDirs = new List<string>();
            while (dir != null)
            {
                parentDirs.Add(dir.FullName);
                dir = dir.Parent;
            }

            return parentDirs.ToArray().Reverse();
        }
    }
}
