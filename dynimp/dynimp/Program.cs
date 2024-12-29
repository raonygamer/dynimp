using System.Reflection;
using dynimp.Utils;

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
            Logger.TraceLn(@$"Usage: dynimp [options]");
            Logger.TraceLn(@$"Options:");
            Logger.TraceLn(@$" -help | --help / Show this help message and exit.");
            return 0;
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
        
        return 0;
    }
}