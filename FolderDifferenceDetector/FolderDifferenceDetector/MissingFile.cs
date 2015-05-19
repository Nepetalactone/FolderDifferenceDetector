namespace FolderDifferenceDetector
{
    class MissingFile
    {
        private readonly string _sourceFile;
        private readonly string _targetFile;

        public string SourceFile
        {
            get { return _sourceFile; }
        }

        public string TargetFile
        {
            get { return _targetFile; }
        }

        public MissingFile(string sourceFile, string targetFile)
        {
            _sourceFile = sourceFile;
            _targetFile = targetFile;
        }
    }
}
