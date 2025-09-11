namespace Skyline.DataMiner.CICD.Tools.Validator
{
    using System.CommandLine;
    using System.IO;
    using System.Reflection;
    using System.Text.Json;
    using System.Threading.Tasks;
    using System.Xml.Serialization;
    using Microsoft.Extensions.Logging;
    using Skyline.DataMiner.CICD.Tools.Validator.OutputWriters;

    public class Program
    {
        public static async Task<int> Main(string[] args)
        {
            var rootCommand = new RootCommand("Validates a DataMiner artifact or solution.");

            #region validate-protocol-solution

            var solutionPathOption = new Option<string>(
                name: "--solution-path",
                description: "Path to a solution file (.sln) of a DataMiner protocol or a directory that contains a .sln file. Note: In case the specified directory contains multiple .sln files, you must specify the file path of a specific solution.")
            {
                IsRequired = true
            };
            solutionPathOption.LegalFilePathsOnly();

            var validatorResultsOutputDirectoryOption = new Option<string>(
                name: "--output-directory",
                description: "Path to the directory where the validator results should be stored.")
            {
                IsRequired = true
            };

            var validatorResultsFileNameOption = new Option<string>(
                name: "--output-file-name",
                description:
                "Name of the results file. Note: Do not provide an extension, the extension is automatically added based on the results-output-formats option. Default: 'ValidatorResults_<protocolName>_<protocolVersion>'")
            {
                IsRequired = false
            };

            var outputFormatsOption = new Option<string[]>(
                name: "--output-format",
                description:
                "Specifies the output format. Possible values: JSON, XML, HTML. Specify a space separated list to output multiple formats.",
                getDefaultValue: () => new[] { "JSON", "HTML" })
            {
                Arity = ArgumentArity.ZeroOrMore,
                IsRequired = false,
                AllowMultipleArgumentsPerToken = true,
            };
            outputFormatsOption.FromAmong("JSON", "XML", "HTML");

            var includeSuppressedOption = new Option<bool>(
                name: "--include-suppressed",
                description: "Specifies whether the suppressed results should also be included in the results.",
                getDefaultValue: () => false)
            {
                IsRequired = false
            };

            var performRestoreOption = new Option<bool>(
                name: "--perform-restore",
                description: "Specifies whether to perform a dotnet restore operation.",
                getDefaultValue: () => true)
            {
                IsRequired = false
            };

            var restoreTimeoutOption = new Option<int>(
                name: "--restore-timeout",
                description: "Specifies the timeout for the restore operation (in ms).",
                getDefaultValue: () => 300000)
            {
                IsRequired = false
            };

            // output format.
            var validateProtocolSolutionCommand = new Command("validate-protocol-solution", "Validates a protocol solution.")
            {
                solutionPathOption,
                validatorResultsOutputDirectoryOption,
                validatorResultsFileNameOption,
                outputFormatsOption,
                includeSuppressedOption,
                performRestoreOption,
                restoreTimeoutOption
            };

            rootCommand.Add(validateProtocolSolutionCommand);
            

            #endregion

            #region major-change-checker

            var majorChangeCheckerSolutionPathOption = new Option<string>(
                    name: "--mcc-solution-path",
                    description: "Path to the new solution file (.sln) of a DataMiner protocol.")
            {
                IsRequired = true
            };
            majorChangeCheckerSolutionPathOption.LegalFilePathsOnly();

            var oldProtocolPathOption = new Option<string>(
                name: "--mcc-old-protocol-path",
                description: "Path to the old protocol.xml file for comparison.")
            {
                IsRequired = true
            };
            oldProtocolPathOption.LegalFilePathsOnly();

            var majorChangeCheckerOutputDirectoryOption = new Option<string>(
                name: "--mcc-output-directory",
                description: "Path to the directory where the MCC results should be stored.")
            {
                IsRequired = true
            };

            var majorChangeCheckerOutputFileNameOption = new Option<string>(
                name: "--output-file-name",
                description: "Name of the MCC results file.")
            {
                IsRequired = false
            };

            var majorChangeCheckerOutputFormatsOption = new Option<string[]>(
                name: "--output-format",
                description: "Specifies the output format for MCC results. Possible values: JSON, XML, HTML.",
                getDefaultValue: () => new[] { "JSON", "HTML"}) 
            {
                Arity = ArgumentArity.ZeroOrMore,
                IsRequired = false,
                AllowMultipleArgumentsPerToken = true,
            };
            majorChangeCheckerOutputFormatsOption.FromAmong("JSON", "XML", "HTML");

            var majorChangeCheckerIncludeSuppressedOption = new Option<bool>(
                name: "--include-suppressed",
                description: "Specifies whether the suppressed results should also be included in the MCC results.",
                getDefaultValue: () => false)
            {
                IsRequired = false
            };
/*
            var catalogIdOption = new Option<string>(
                name: "--catalog-id",
                description: "Catalog ID for fetching previous protocol version from Catalog API.")
            {
                IsRequired = false
            };

            var catalogApiKeyOption = new Option<string>(
                name: "--catalog-api-key",
                description: "Subscription key for Catalog API access.")
            {
                IsRequired = false
            };

            var tempDirectoryOption = new Option<string>(
                name: "--temp-directory",
                description: "Directory to store downloaded protocol versions.",
                getDefaultValue: () => Path.GetTempPath())
            {
                IsRequired = false
            };
*/
            var mccCommand = new Command("major-change-checker", "Performs major change checking between protocol versions.")
            {
                majorChangeCheckerSolutionPathOption,
                oldProtocolPathOption,
                majorChangeCheckerOutputDirectoryOption,
                majorChangeCheckerOutputFileNameOption,
                majorChangeCheckerOutputFormatsOption,
                majorChangeCheckerIncludeSuppressedOption,
                //catalogIdOption,                
                //catalogApiKeyOption,            
                //tempDirectoryOption
            };
            /*
                        mccCommand.SetHandler(async (context) =>
                        {
                            var solutionPath = context.ParseResult.GetValueForOption(majorChangeCheckerSolutionPathOption);
                            var oldProtocolPath = context.ParseResult.GetValueForOption(oldProtocolPathOption);
                            var outputDirectory = context.ParseResult.GetValueForOption(majorChangeCheckerOutputDirectoryOption);
                            var outputFileName = context.ParseResult.GetValueForOption(majorChangeCheckerOutputFileNameOption);
                            var outputFormats = context.ParseResult.GetValueForOption(majorChangeCheckerOutputFormatsOption);
                            var includeSuppressed = context.ParseResult.GetValueForOption(majorChangeCheckerIncludeSuppressedOption);
                            var catalogId = context.ParseResult.GetValueForOption(catalogIdOption);
                            var apiKey = context.ParseResult.GetValueForOption(catalogApiKeyOption);
                            var tempDirectory = context.ParseResult.GetValueForOption(tempDirectoryOption);
                            string oldProtocolCode = File.ReadAllText(oldProtocolPath);

                            var checker = new MajorChangeChecker();
                            var results = await checker.CheckMajorChanges(solutionPath, oldProtocolCode, includeSuppressed);

                            if (string.IsNullOrEmpty(outputFileName))
                            {
                                outputFileName = $"MCCResults_{results.Protocol}_{results.Version}";
                            }

                            Directory.CreateDirectory(outputDirectory);

                            foreach (var format in outputFormats)
                            {
                                string filePath = Path.Combine(outputDirectory, $"{outputFileName}.{format.ToLower()}");

                                switch (format.ToUpper())
                                {
                                    case "JSON":
                                        var jsonOptions = new JsonSerializerOptions { WriteIndented = true };
                                        string json = JsonSerializer.Serialize(results, jsonOptions);
                                        File.WriteAllText(filePath, json);
                                        break;

                                    case "XML":
                                        var xmlSerializer = new XmlSerializer(typeof(ValidatorResults));
                                        using (var writer = new StreamWriter(filePath))
                                        {
                                            xmlSerializer.Serialize(writer, results);
                                        }
                                        break;

                                    case "HTML":

                                        var htmlSerializer = new ResultWriterHtml(filePath, logger, includeSuppressed);
                                        using (var writer = new StreamWriter(filePath))
                                        {
                                            htmlSerializer.WriteResults(results);
                                        }
                                        break;
                                    }

                                        context.Console.WriteLine($"Saved {format} results to {filePath}");
                                }

                                context.Console.WriteLine($"Major change check completed. Results saved to {outputDirectory}");

                        });     */

            mccCommand.SetHandler(async (context) =>
            {
                var solutionPath = context.ParseResult.GetValueForOption(majorChangeCheckerSolutionPathOption);
                var oldProtocolPath = context.ParseResult.GetValueForOption(oldProtocolPathOption);
                var outputDirectory = context.ParseResult.GetValueForOption(majorChangeCheckerOutputDirectoryOption);
                var outputFileName = context.ParseResult.GetValueForOption(majorChangeCheckerOutputFileNameOption);
                var outputFormats = context.ParseResult.GetValueForOption(majorChangeCheckerOutputFormatsOption);
                var includeSuppressed = context.ParseResult.GetValueForOption(majorChangeCheckerIncludeSuppressedOption);
                //var catalogId = context.ParseResult.GetValueForOption(catalogIdOption);
                //var apiKey = context.ParseResult.GetValueForOption(catalogApiKeyOption);
                //var tempDirectory = context.ParseResult.GetValueForOption(tempDirectoryOption);

                var runner = new MajorChangeCheckerRunner();
                /*int result = await runner.RunMajorChangeChecker(
                    solutionPath, oldProtocolPath, outputDirectory, outputFileName,
                    outputFormats, includeSuppressed, catalogId, apiKey, tempDirectory);
                */
                int result = await runner.RunMajorChangeChecker(
                    solutionPath, oldProtocolPath, outputDirectory, outputFileName,
                    outputFormats, includeSuppressed);

                context.ExitCode = result;
            });

            rootCommand.Add(mccCommand);
            #endregion

            int value = await rootCommand.InvokeAsync(args);
            return value;
        }
    }     
}   
