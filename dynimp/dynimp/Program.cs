using System.Diagnostics;
using System.Reflection;
using System.Text;
using AsmResolver;
using AsmResolver.IO;
using AsmResolver.PE;
using AsmResolver.PE.File;
using AsmResolver.PE.Imports;
using dynimp.Models;
using dynimp.Utils;
using Newtonsoft.Json;

namespace dynimp;

class Program
{
    public const string IMPORT_SECTION_NAME = ".dynidt";
    public const string DYNIMP_SECTION_NAME = ".dynimp";
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

    private List<ModuleImportDescriptor> ReadImportsAtRVA(PEFile file, uint rva)
    {
        List<ModuleImportDescriptor> result = [];
        if (file.TryCreateReaderAtRva(rva, out var reader))
        {
            var buffer = new byte[20];
            while (true)
            {
                reader.ReadBytes(buffer);
                if (buffer.All(x => x == 0))
                    break;
                var moduleImportDescriptor = ModuleImportDescriptor.FromBytes(buffer);
                result.Add(moduleImportDescriptor);
            }
        }

        return result;
    }
    
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
            Logger.TraceLn($" -version [ver] / Specify the version to use with dynimp. If not specified only imports with \"any\" will be parsed.");
            Logger.TraceLn($" -dynimp [*.dynimp.json] / Specify the dynamic imports file.");
            Logger.TraceLn($" -genlib / Generate libraries for imports.");
            Logger.TraceLn($" -modbin [*.exe|*.dll] / Modify portable executable imports to support dynamic importing.");
            Logger.TraceLn($" -outdir [directory] / Output directory for generated files. Default is current directory.");
            return 0;
        }

        if (parser.Get("version") is null)
        {
            Logger.TraceLn($"No version specified. Using \"any\".");
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

        string outDir = parser.Get("outdir", Directory.GetCurrentDirectory())!;
        string machine = parser.Get("machine", "x64")!;
        string version = parser.Get("version", "any")!;
        
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
            Logger.TraceLn($"Module: {dynImp.Target}");
            Logger.TraceLn($"Imports: {dynImp.Imports.Count}");

            List<DynImp.ImportDescriptor> imports = dynImp.Imports.Where(x =>
            {
                return x.Points.Any(y => y.Version == version || y.Version == "any");
            }).ToList();
            Logger.TraceLn($"Imports for version \"{version}\": {imports.Count}");
            
            if (parser.Has("genlib"))
            {
                if (imports.Count == 0)
                {
                    Logger.WarnLn($"No imports found for version \"{version}\". Skipping library generation.");
                    return 0;
                }
                
                string defFileContents = "";
                defFileContents += $"LIBRARY \"{dynImp.Target}\"\n";
                defFileContents += $"EXPORTS\n";
                
                Logger.TraceLn($"Parsing imports...");
                foreach (var import in imports)
                {
                    Logger.TraceLn($"Parsing \"{import.Symbol}\" dynamic import...");
                    defFileContents += $"    {import.Symbol}\n";
                }
                
                Logger.TraceLn($"Generating export definition file (.def) for {dynImp.Target}...");
                string defFile = $"{outDir}/{machine}/{version}/{Path.GetFileNameWithoutExtension(dynImp.Target)}.def";
                Directory.CreateDirectory(Path.GetDirectoryName(defFile) ?? "");
                await File.WriteAllBytesAsync(defFile, Encoding.UTF8.GetBytes(defFileContents));
                
                Logger.TraceLn($"Generating import library (.lib) for {dynImp.Target}...");
                string libExePath = $"{VCTools.GetVCToolsPath(machine)}/lib.exe";
                string libFile = $"{outDir}/{machine}/{version}/{Path.GetFileNameWithoutExtension(dynImp.Target)}.lib";
                
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
                if (imports.Count == 0)
                {
                    Logger.WarnLn($"No imports found for version \"{version}\". Skipping portable executable modification.");
                    return 0;
                }
                
                Logger.TraceLn($"Modifying portable executable imports to support dynamic importing...");
                string modBin = parser.Get("modbin")!;
                if (File.Exists(modBin) is false)
                {
                    Logger.ErrorLn($"Portable executable does not exist. Please specify a valid portable executable.");
                    return -1;
                }
                
                Logger.TraceLn($"Modifying {modBin}...");
                PEFile moduleBinary = PEFile.FromFile(modBin);
                var importDataDirectory = moduleBinary.OptionalHeader.GetDataDirectory(DataDirectoryIndex.ImportDirectory);
                if (!importDataDirectory.IsPresentInPE)
                {
                    Logger.ErrorLn($"Failed to locate import directory in portable executable. Please ensure the file is a valid portable executable.");
                    return -1;
                }
                
                Logger.TraceLn($"Found import directory at RVA 0x{importDataDirectory.VirtualAddress:X8}.");
                Logger.TraceLn($"Parsing imports...");

                var importsList = ReadImportsAtRVA(moduleBinary, importDataDirectory.VirtualAddress);
                var importsWithoutDynamicModule = importsList.Where(x =>
                    moduleBinary.CreateReaderAtRva(x.Name).ReadUtf8String() != dynImp.Target).ToList();
                var dynamicModules = importsList.Where(x =>
                    moduleBinary.CreateReaderAtRva(x.Name).ReadUtf8String() == dynImp.Target).ToList();

                byte[] zeroBuffer = new byte[20];
                Array.Fill<byte>(zeroBuffer, 0);
                
                var idtSection = moduleBinary.Sections.FirstOrDefault(x => x.Name == IMPORT_SECTION_NAME);
                var oldImportsList = new List<ModuleImportDescriptor>();
                if (idtSection is not null)
                {
                    Logger.WarnLn($"Found existing {IMPORT_SECTION_NAME} (Import Directory Table) section. Merging and Overwriting...");
                    oldImportsList = ReadImportsAtRVA(moduleBinary, idtSection.Rva);
                    moduleBinary.Sections.Remove(idtSection);
                }

                importsWithoutDynamicModule = importsWithoutDynamicModule.Concat(oldImportsList).Distinct().ToList();
                
                var dynImpSection = moduleBinary.Sections.FirstOrDefault(x => x.Name == DYNIMP_SECTION_NAME);
                var oldDynImpsList = new List<ModuleImportDescriptor>();
                if (dynImpSection is not null)
                {
                    Logger.WarnLn($"Found existing {DYNIMP_SECTION_NAME} (Dynamic Import Table) section. Merging and overwriting...");
                    oldDynImpsList = ReadImportsAtRVA(moduleBinary, dynImpSection.Rva);
                    moduleBinary.Sections.Remove(dynImpSection);
                }

                oldDynImpsList = oldDynImpsList.Concat(dynamicModules).Distinct().ToList();

                var importsWithoutDynamicModuleBytes = importsWithoutDynamicModule.SelectMany(x => x.ToBytes()).ToArray();
                idtSection = new PESection(IMPORT_SECTION_NAME, SectionFlags.MemoryRead | SectionFlags.MemoryWrite | SectionFlags.ContentInitializedData)
                {
                    Contents = new DataSegment(importsWithoutDynamicModuleBytes)
                };
                
                moduleBinary.Sections.Add(idtSection);
                moduleBinary.AlignSections();
                moduleBinary.OptionalHeader.SetDataDirectory(DataDirectoryIndex.ImportDirectory, new DataDirectory(
                    idtSection.Rva,
                    (uint)importsWithoutDynamicModuleBytes.Length
                ));
                
                var oldDynImpsBytes = oldDynImpsList.SelectMany(x => x.ToBytes()).ToArray();
                dynImpSection = new PESection(DYNIMP_SECTION_NAME, SectionFlags.MemoryRead | SectionFlags.MemoryWrite | SectionFlags.ContentInitializedData)
                {
                    Contents = new DataSegment(oldDynImpsBytes)
                };
                
                moduleBinary.Sections.Add(dynImpSection);
                moduleBinary.AlignSections(); 
                moduleBinary.Write(Path.Combine(outDir, Path.GetFileName(modBin)));
                
                Logger.TraceLn($"Excluding {dynImp.Target} from import list...");
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