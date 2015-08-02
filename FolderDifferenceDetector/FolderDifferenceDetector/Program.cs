using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;

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
            watch.Stop();
            if (differences.Length != 0)
            {
                Console.Clear();
                foreach (var difference in differences)
                {
                    Console.WriteLine("File: {0}\n", difference.SourceFile);
                }
                
                Console.WriteLine(Environment.NewLine + "Scan done in " + watch.ElapsedMilliseconds +
                                  " milliseconds, found {0} differences\nUpload differences? y/n", differences.Length);
                if (Console.ReadKey().Key.ToString().ToLower() == "y")
                {
                    Console.Clear();
                    watch.Reset();
                    watch.Start();
                    UploadToFtp(differences, args[2], args[3]);
                    watch.Stop();
                    Console.WriteLine("Upload done in " + watch.ElapsedMilliseconds + " milliseconds");
                }
            }
            else
            {
                Console.Clear();
                Console.WriteLine("Scan done in " + watch.ElapsedMilliseconds + ". No differences found.");
                Console.WriteLine("Press any key to end program");
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

                    //Iteratively create directories
                    foreach (var dir in GetDirectoryStructure(remoteDirectory))
                    {
                        if (!Directory.Exists(dir))
                        {
                            WebRequest request = WebRequest.Create("ftp:" + EscapeFTPPath(dir));
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
                        FtpWebRequest request = (FtpWebRequest)WebRequest.Create("ftp:" + EscapeFTPPath(file.TargetFile));
                        request.Method = WebRequestMethods.Ftp.UploadFile;
                        request.Credentials = credential;

                        using (FileStream fileStream = new FileStream(file.SourceFile, FileMode.Open))
                        using (Stream requestStream = request.GetRequestStream())
                        {
                            byte[] fileContents = new byte[fileStream.Length];
                            fileStream.Read(fileContents, 0, (int)fileStream.Length);
                            request.ContentLength = fileContents.Length;
                            requestStream.Write(fileContents, 0, fileContents.Length);
                        }

                        using (var response = (FtpWebResponse)request.GetResponse())
                        {
                            if (response.StatusCode != FtpStatusCode.FileActionOK && response.StatusCode != FtpStatusCode.ClosingData)
                            {
                                Console.WriteLine(response.StatusCode);
                            }
                            else
                            {
                                Console.WriteLine("Successfully uploaded: " + file.TargetFile);
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

        private static string EscapeFTPPath(string ftpPath)
        {
            StringBuilder builder = new StringBuilder();

            foreach (string ftpPathPart in ftpPath.Split('\\'))
            {
                builder.Append(Uri.EscapeDataString(ftpPathPart.Replace('\\', ' ')) + '\\');
            }

            //remove final '\\'
            return builder.ToString(0, builder.Length - 1).Replace("\\", "/");
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
