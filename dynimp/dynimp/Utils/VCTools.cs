namespace dynimp.Utils;

public static class VCTools
{
    public static bool IsVCToolsInstalled()
    {
        return File.Exists($"{Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)}/Microsoft Visual Studio/2022/Community/VC/Auxiliary/Build/Microsoft.VCToolsVersion.default.txt");
    }

    public static string GetVCToolsPath(string arch = "x64")
    {
        if (!IsVCToolsInstalled())
            return string.Empty;

        string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        string version = File
            .ReadAllText(
                $"{programFiles}/Microsoft Visual Studio/2022/Community/VC/Auxiliary/Build/Microsoft.VCToolsVersion.default.txt")
            .Trim();
        
        string path = $"{programFiles}/Microsoft Visual Studio/2022/Community/VC/Tools/MSVC/{version}/bin/Host{arch}/{arch}";
        return path;
    }
}