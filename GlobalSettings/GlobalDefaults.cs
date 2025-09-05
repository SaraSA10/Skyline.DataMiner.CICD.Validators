using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Skyline.DataMiner.CICD.Common;

/// <summary>
/// Global default values that can be used across the entire CI/CD pipeline.
/// </summary>
public static class GlobalDefaults
{
    /*
     * List of all released DataMiner versions with build number & CU version:
     * https://intranet.skyline.be/DataMiner/Lists/Released%20Versions/AllItems.aspx
     */

    /// <summary>
    /// Gets the minimum DataMiner version for which support is still given.
    /// </summary>
    public static string MinimumSupportedDataMinerVersion { get; } = "10.3.0";

    /// <summary>
    /// Gets the minimum DataMiner version with its build number for which support is still given.
    /// </summary>
    public static string MinimumSupportedDataMinerVersionWithBuildNumber { get; } = "10.3.0.0 - 12752";

    /// <summary>
    /// Gets the minimum DataMiner version with its build number for which support is still given.
    /// </summary>
    public static DataMinerVersion MinSupportedDmVersionWithBuildNumber = DataMinerVersion.Parse(MinimumSupportedDataMinerVersionWithBuildNumber);

    /// <summary>
    /// Gets the minimum DataMiner version which supports.
    /// </summary>
    public static string MinimumSupportDataMinerVersionForDMApp { get; } = "10.0.10.0-9414"; // DMApp install support is "10.0.9.0-9312" but DllImport support is 10.0.10.

    /// <summary>
    /// Gets the IP address of the Driver Passport Platform.
    /// </summary>
    public static string DriverPassportPlatformIpAddress { get; } = "10.11.2.57";

    public static char Seperator_DcpCatalogs_UniqueName { get; } = '|';

    public static string JenkinsRelationContentFile { get; } = "RelationContent.json";

    public static string UpdatedDcpDriverRecordIdsFilePath { get; } = "./tmp/__DcpDriverRecordIds__.txt";
}
