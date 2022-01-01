// Copyright Siamango

using System.IO;
using System.Reflection;

namespace SolmangoCLI;

public class Paths
{
    public static readonly string REPORTS_FOLDER_PATH = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/reports/";
    public static readonly string RES_FOLDER_PATH = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/res/";
    public static readonly string CACHE_FOLDER_PATH = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/cache/";
}