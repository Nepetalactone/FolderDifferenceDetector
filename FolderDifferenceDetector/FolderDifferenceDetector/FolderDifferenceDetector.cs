using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace FolderDifferenceDetector
{
    class FolderDifferenceDetector
    {
        private readonly DirectoryInfo _firstDirectory;
        private readonly DirectoryInfo _secondDirectory;
        private ConcurrentBag<MissingFile> _differences;
        private readonly int _nrOfFiles;
        private int _processedFiles;

        public FolderDifferenceDetector(String firstDirectory, String secondDirectory)
        {
            _firstDirectory = new DirectoryInfo(firstDirectory);
            _secondDirectory = new DirectoryInfo(secondDirectory);
            _differences = new ConcurrentBag<MissingFile>();
            _nrOfFiles = GetTotalNumberOfFiles(_firstDirectory);
            _processedFiles = 0;
        }

        private void DetectDifferences(DirectoryInfo masterDirectory, DirectoryInfo slaveDirectory)
        {
            //find folder in target
            Parallel.ForEach(masterDirectory.EnumerateDirectories(), masterSubDir =>
            {
                var correspondingFolder = (from DirectoryInfo slaveSubDir in slaveDirectory.EnumerateDirectories()
                    where slaveSubDir.Name.Equals(masterSubDir.Name)
                    select slaveSubDir).FirstOrDefault();

                //if folder doesn't exist, list all files in source as difference
                if (correspondingFolder == null)
                {
                    foreach (String file in GetAllFilesFromDirectoryAndSubdirectories(masterSubDir))
                    {
                        _differences.Add(CreateMissingFileEntry(file));
                    }
                }
                else
                {
                    DetectDifferences(masterSubDir, correspondingFolder);
                }
            }
            );

            //check for each file in source directory if it exists in the target directory aswell
            Parallel.ForEach(masterDirectory.EnumerateFiles(), masterFile =>
            {
                var correspondingFile = (from FileInfo slaveFile in slaveDirectory.EnumerateFiles()
                                         where slaveFile.Name.Equals(masterFile.Name) &&
                                         slaveFile.Length.Equals(masterFile.Length)
                                         select slaveFile).FirstOrDefault();

                //add file to difference list if it doesnt exist in the target directory
                if (correspondingFile == null)
                {
                    _differences.Add(CreateMissingFileEntry(masterFile.FullName));
                }
                _processedFiles++;
                PrintProgress();
            }
            );
        }

        private void PrintProgress()
        {
            Console.Write("\r{0}\\{1}", _processedFiles, _nrOfFiles);
        }

        public MissingFile[] DetectMissingElementsInSecondDir()
        {
            DetectDifferences(_firstDirectory, _secondDirectory);
            MissingFile[] result = _differences.ToArray();
            _differences = new ConcurrentBag<MissingFile>();
            return result;
        }

        public MissingFile[] DetectMissingElementsInFirstDir()
        {
            DetectDifferences(_secondDirectory, _firstDirectory);
            MissingFile[] result = _differences.ToArray();
            _differences = new ConcurrentBag<MissingFile>();
            return result;
        }

        private MissingFile CreateMissingFileEntry(String masterFile)
        {
            String relativePath = masterFile.Remove(0, _firstDirectory.FullName.Length);
            String targetPath = _secondDirectory.FullName + relativePath;

            return new MissingFile(masterFile, targetPath);
        }

        private String[] GetAllFilesFromDirectoryAndSubdirectories(DirectoryInfo directory)
        {
            List<String> fileList = new List<string>();
            foreach (DirectoryInfo subDir in directory.EnumerateDirectories())
            {
                fileList.AddRange(GetAllFilesFromDirectoryAndSubdirectories(subDir));
            }

            fileList.AddRange(directory.EnumerateFiles().Select(x => x.FullName));
            return fileList.ToArray();
        }

        private int GetTotalNumberOfFiles(DirectoryInfo dir)
        {
            int nrOfSubFiles = 0;
            foreach (DirectoryInfo subdir in dir.GetDirectories())
            {
                nrOfSubFiles += GetTotalNumberOfFiles(subdir);
                Console.Write("\r{0}", nrOfSubFiles);
            }

            return nrOfSubFiles + dir.GetFiles().Length;
        }
    }
}
