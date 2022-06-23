using OWML.ModHelper;
using System.IO;

namespace PostBuildApp
{
    class Program
    {
        static void Main()
        {
            foreach (string assetPathS in Directory.EnumerateFiles(ModHelper.Manifest.ModFolderPath, "*.wav"))
            {
                File.Delete(assetPathS);
            }
            foreach (string characterDirectory in Directory.GetDirectories(ModHelper.Manifest.ModFolderPath))
            {
                foreach (string assetPathS in Directory.EnumerateFiles(characterDirectory, "*.wav", SearchOption.AllDirectories))
                {
                    File.Move(assetPathS, Path.Combine(ModHelper.Manifest.ModFolderPath, Path.GetFileName(assetPathS)));
                }
                Directory.Delete(characterDirectory);
            }
        }
    }
}
