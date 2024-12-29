using System.Diagnostics;
using System.Reflection;
using System.Text;
using dynimp.Models;
using dynimp.Utils;
using Newtonsoft.Json;

namespace dynimp;

class Program
{
    #region Singleton
    private static Program? _instance;
    public static Program Instance
    {
        get
        {
            return _instance ??= new Program();
        }
    }
    static int Main(string[] args) => Instance.Start().GetAwaiter().GetResult(); 
    #endregion
    
    private async Task<int> Start()
    {
        Logger.TraceLn($"dynimp v{Assembly.GetExecutingAssembly().GetName().Version} was created by rydev.");
        Logger.TraceLn($"Loading dynimp...");

        var parser = new ArgumentParser(Environment.GetCommandLineArgs());
        if (parser.Has("help"))
        {
            Logger.TraceLn($"Usage: dynimp [options]");
            Logger.TraceLn($"Options:");
            Logger.TraceLn($" -help / Show this help message and exit.");
            Logger.TraceLn($" -machine [x64|x86] / Specify the architecture to use (x64 or x86). Default is x64.");
            Logger.TraceLn($" -dynimp [*.dynimp.json] / Specify the dynamic imports file.");
            Logger.TraceLn($" -genlib / Generate libraries for imports.");
            Logger.TraceLn($" -modbin [*.exe|*.dll] / Modify portable executable imports to support dynamic importing.");
            Logger.TraceLn($" -outdir [directory] / Output directory for generated files. Default is current directory.");
            return 0;
        }
        
        if (parser.Get("machine", "x64") is not ("x64" or "x86"))
        {
            Logger.ErrorLn($"Invalid machine architecture specified. Please specify either x64 or x86.");
            return -1;
        }
        
        if (!parser.Has("dynimp"))
        {
            Logger.ErrorLn($"No dynamic imports file specified. Please specify a dynamic imports file.");
            return -1;
        }
        else if (File.Exists(parser.Get("dynimp")) is false)
        {
            Logger.ErrorLn($"Dynamic imports file does not exist. Please specify a valid dynamic imports file.");
            return -1;
        }
        
        if (VCTools.IsVCToolsInstalled())
        {
            Logger.TraceLn($"VC++ workload is installed.");
        }
        else
        {
            Logger.ErrorLn($"VC++ workload is not installed. Please install Visual Studio with the VC++ workload and try again.");
            return -1;
        }

        string outDir = parser.Get("outdir", Directory.GetCurrentDirectory());
        string machine = parser.Get("machine", "x64");
        
        string dynimpFileText = await File.ReadAllTextAsync(parser.Get("dynimp"));
        try
        {
            var dynImp = JsonConvert.DeserializeObject<DynImp>(dynimpFileText);
            if (dynImp is null)
            {
                Logger.ErrorLn($"Failed to parse dynamic imports file. Please ensure the file is valid.");
                return -1;
            }
            
            Logger.TraceLn($"Loaded dynamic imports file.");
            Logger.TraceLn($"Module: {dynImp.Module}");
            Logger.TraceLn($"Imports: {dynImp.Imports.Count}");
            
            if (parser.Has("genlib"))
            {
                string defFileContents = "";
                defFileContents += $"LIBRARY \"{dynImp.Module}\"\n";
                defFileContents += $"EXPORTS\n";
                
                Logger.TraceLn($"Parsing imports...");
                foreach (var import in dynImp.Imports)
                {
                    Logger.TraceLn($"Parsing \"{import.Symbol}\" dynamic import...");
                    defFileContents += $"    {import.Symbol}\n";
                }
                
                Logger.TraceLn($"Generating export definition file (.def) for {dynImp.Module}...");
                await File.WriteAllBytesAsync($"{Path.GetFileNameWithoutExtension(dynImp.Module)}.def", Encoding.UTF8.GetBytes(defFileContents));
                
                Logger.TraceLn($"Generating import library (.lib) for {dynImp.Module}...");
                string libExePath = $"{VCTools.GetVCToolsPath(machine)}/lib.exe";
                string defFile = $"{Path.GetFileNameWithoutExtension(dynImp.Module)}.def";
                string libFile = $"{Path.GetFileNameWithoutExtension(dynImp.Module)}.lib";
                
                ProcessStartInfo libStartInfo = new ProcessStartInfo
                {
                    FileName = libExePath,
                    Arguments = $"/def:\"{defFile}\" /machine:{machine} /out:\"{libFile}\"",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                Process? proc = Process.Start(libStartInfo);
                if (proc is null)
                {
                    Logger.ErrorLn($"Failed to start lib.exe. Please ensure the VC++ workload is installed.");
                    return -1;
                }
                
                Logger.WriteLn(await proc.StandardOutput.ReadToEndAsync(), ConsoleColor.Gray);
            }
            else if (parser.Has("modbin"))
            {
                Logger.TraceLn($"Modifying portable executable imports to support dynamic importing...");
                string modBin = parser.Get("modbin");
                if (File.Exists(modBin) is false)
                {
                    Logger.ErrorLn($"Portable executable does not exist. Please specify a valid portable executable.");
                    return -1;
                }
                
                Logger.TraceLn($"Modifying {modBin}...");
            }
            else
            {
                Logger.WarnLn("No action specified. Please specify an action to perform.");
            }
        }
        catch (Exception ex)
        {
            Logger.ErrorLn(ex);
        }
        
        return 0;
    }
}