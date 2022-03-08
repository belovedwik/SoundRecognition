using System.Collections.Generic;
using System.IO;

namespace DeltaM.DeltaTell.Amd.Helpers
{
    public class PlayListBuilder
    {
        private const string BaseDirPath = @"tmp";
        private const string AutoDirPath = @"Auto";
        private const string InvalidDirPath = @"Invalid";
        private const string FileListPath = "FileList.txt";
        public List<string> validList;
        public List<string> FileList;
        public PlayListBuilder()
        {
            if (!Directory.Exists(BaseDirPath)) Directory.CreateDirectory(BaseDirPath);
            if (!Directory.Exists(AutoDirPath)) Directory.CreateDirectory(AutoDirPath);
            if (!Directory.Exists(InvalidDirPath)) Directory.CreateDirectory(InvalidDirPath);
            validList = new List<string>();
            FileList = new List<string>();
            BuildList();
            Getplaylist();
        }

        public static string InvalidDirPath1 => InvalidDirPath;

        public void Getplaylist()
        {
            for (int i = 0; i < FileList.Count; i++)
            {
                foreach (var item in Directory.GetFiles(BaseDirPath))
                {
                    if (item.Contains(FileList[i])) File.Copy(item, AutoDirPath + @"\" + Path.GetFileName(item), true);
                }
            }
        }

        private void BuildList()
        {
            using (StreamReader reader = new StreamReader(FileListPath))
            {
                while (!reader.EndOfStream)
                {
                    FileList.Add(reader.ReadLine());
                }
            }
        }
    }
}
