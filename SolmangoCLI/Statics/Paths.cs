// Copyright Siamango

using System.IO;
using System.Reflection;

namespace SolmangoCLI.Statics;

public class Paths
{
    public static readonly string REPORTS_FOLDER_PATH = Path.GetDirectoryName(Assembly.GetEntryAssembly().Location) + "/reports/";
}