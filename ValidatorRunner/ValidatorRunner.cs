using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Skyline.DataMiner.CICD.Validators.Common.Data;
using Skyline.DataMiner.CICD.Validators.Common.Interfaces;
using Skyline.DataMiner.CICD.Validators.Common.Model;
using Skyline.DataMiner.CICD.Validators.Protocol;
using Skyline.DataMiner.XmlSchemas.Protocol;
using Skyline.DataMiner.CICD.Common;
using Skyline.DataMiner.CICD.Validators.Common.Suppressions;
using Skyline.DataMiner.CICD.Parsers.Common.Xml;
using Skyline.DataMiner.CICD.Models.Protocol.Read;
using Skyline.DataMiner.CICD.Models.Common;
using Skyline.DataMiner.CICD.Models.Protocol;
using Microsoft.CodeAnalysis.MSBuild;
using ValidatorHelperMajorChangeChecker;
using LegacyValidator = Skyline.DataMiner.CICD.Validators.Protocol.Legacy.Validator;
using Skyline.DataMiner.CICD.Validators.Common.Tools;


namespace ValidatorRunnerMajorChangeChecker
{
    public class ValidatorRunner
    {
        private readonly string dataMinerBasePath;
        private FilteredResults validatorResults;
        private FilteredResults compareResults;

        public ValidatorRunner(string dataMinerBasePath)
        {
            this.dataMinerBasePath = dataMinerBasePath;

            var currentDomain = AppDomain.CurrentDomain;
            currentDomain.AssemblyResolve += MyResolveEventHandler;
        }

        /// <summary>
        /// Runs the validator against the protocol.xml file in the specified folder.
        /// </summary>
        /// <param name="protocolFolderPath">The folder that holds the protocol.xml file to be validated.</param>
        /// <param name="validatorResultsFilePath">The path of the file that contain the results.</param>
        /// <param name="validatorSuppressedResultsFilePath">The path of the file that contain the results including the suppressed ones.</param>
        /// <param name="customDllsPath">Path to a folder that holds additional DLLs that are used by the protocol.</param>
        /// <param name="uomFilePath">Path of the uom XSD file.</param>
        public void RunValidator(string protocolFolderPath, string validatorResultsFilePath, string validatorSuppressedResultsFilePath, string customDllsPath, string uomFilePath)
        {
            validatorResults = new FilteredResults();

            // Get protocol code
            string protocolCode = GetProtocolCode(protocolFolderPath);

            // Validate protocol
            ValidateXml(protocolCode, customDllsPath, uomFilePath);

            // Save results
            SaveAllValidatorResults(validatorResultsFilePath, validatorSuppressedResultsFilePath);
        }

        /// <summary>
        /// Runs the validator against the solution in the specified folder.
        /// </summary>
        /// <param name="solutionFilePath">The path to the solution (.sln) file.</param>
        /// <param name="protocolFolderPath">The folder that holds the protocol.xml file to be validated.</param>
        /// <param name="validatorResultsFilePath">The path of the file that contain the results.</param>
        /// <param name="validatorSuppressedResultsFilePath">The path of the file that contain the results including the suppressed ones.</param>
        /// <param name="uomFilePath">Path of the uom XSD file.</param>
        public void RunValidatorOnSolution(string solutionFilePath, string protocolFolderPath, string validatorResultsFilePath, string validatorSuppressedResultsFilePath, string uomFilePath)
        {
            validatorResults = new FilteredResults();

            // Get protocol code
            string protocolCode = GetProtocolCode(protocolFolderPath);

            // Validate protocol
            ValidateSolution(solutionFilePath, protocolCode, uomFilePath);

            // Save results
            SaveAllValidatorResults(validatorResultsFilePath, validatorSuppressedResultsFilePath);
        }

        /// <summary>
        /// Runs the major change checker against the current protocol and the previous minor version.
        /// </summary>
        /// <param name="protocolFolderPath">The folder that holds the protocol.xml file.</param>
        /// <param name="previousProtocolFolderPath">The folder that holds the previous protocol.xml file to compare against.</param>
        /// <param name="majorChangeCheckerResultsFilePath">Results file.</param>
        public void RunMajorChangeChecker(string protocolFolderPath, string previousProtocolFolderPath, string majorChangeCheckerResultsFilePath, string majorChangeCheckerSuppressedResultsFilePath, string uomFilePath)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                Console.WriteLine("Running major change checker: " + protocolFolderPath + ", " + previousProtocolFolderPath + ", " + majorChangeCheckerResultsFilePath);

                compareResults = new FilteredResults();

                string protocolCode = GetProtocolCode(protocolFolderPath);
                string previousProtocolCode = GetProtocolCode(previousProtocolFolderPath);

                CheckMajorChangesValidate(protocolCode, previousProtocolCode, uomFilePath);

                Console.WriteLine("Results: " + compareResults.Errors.Count + ", (suppressed: " + compareResults.SuppressedErrors.Count + ")");

                // Save results
                SaveAllCompareResults(majorChangeCheckerResultsFilePath, majorChangeCheckerSuppressedResultsFilePath);
            }
        }

        public void CheckMajorChangesValidate(string protocolCode, string previousProtocolCode, string uomFilePath)
        {
            using (CancellationTokenSource cts = new CancellationTokenSource())
            {
                IValidator validator = new Validator();

                var previousInputData = new ProtocolInputData(previousProtocolCode, null);
                var newInputData = new ProtocolInputData(protocolCode, null);
                var minVersion = GlobalDefaults.MinSupportedDmVersionWithBuildNumber;

                ValidatorSettings settings = new ValidatorSettings(minVersion, new UnitList(uomFilePath));
                var allResults = validator.RunCompare(newInputData, previousInputData, settings, cts.Token);

                // Filter out suppressed and information messages
                VersionHistorySuppressionManager suppressionManager = new VersionHistorySuppressionManager(newInputData.Model.Protocol);
                FilterMajorChangeCheckResults(allResults, suppressionManager);
            }
        }

        public void ValidateXml(string protocolCode, string customDllsPath, string uomFilePath)
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            string[] dllPaths =
            {
                customDllsPath,
                System.IO.Path.Combine(dataMinerBasePath, "ProtocolScripts"),
                System.IO.Path.Combine(dataMinerBasePath, "Files"),

				// DllImport will be 'SRM\SLSRMLibrary' so no need to specify the subfolders here.
				@"D:\DataMiner dlls\DllImport",
            };

            var parser = new Parser(protocolCode);
            var document = parser.Document;
            var model = new ProtocolModel(document);

            IAssemblyResolver dllImportResolver = new InternalFilesAssemblyResolver(dllPaths);
            IProtocolQActionHelperProvider qactionHelperProvider = new ProtocolQActionHelperProvider(protocolCode);
            string qactionHelperCode = qactionHelperProvider.GetProtocolQActionHelper(protocolCode);

            QActionCompilationModel compilationModel = new QActionCompilationModel(qactionHelperCode, model, dllImportResolver);

            var inputData = new ProtocolInputData(model, document, compilationModel);
            ValidatorSettings settings = new ValidatorSettings(
                GlobalDefaults.MinSupportedDmVersionWithBuildNumber, new UnitList(uomFilePath));
            Task<IList<IValidationResult>>[] tasks =
            {
                Task.Factory.StartNew(() =>
                {
                    // Old validator task
                    IValidator validator = new LegacyValidator();

                    return validator.RunValidate(inputData, settings, cts.Token);
                }),
                Task.Factory.StartNew(() =>
                {
                    // New validator task
					IValidator validator = new Validator();

                    return validator.RunValidate(inputData, settings, cts.Token);
                })
            };

            // Run validator tasks and combine results
            IList<IValidationResult> allResults = Task.WhenAll(tasks).Result.SelectMany(x => x).ToList();

            // Filter out suppressed and information messages
            CommentSuppressionManager suppressionManager = new CommentSuppressionManager(inputData.Document, new SimpleLineInfoProvider(protocolCode));
            FilterResults(allResults, suppressionManager);
        }

        public void ValidateSolution(string solutionFilePath, string protocolCode, string uomFilePath)
        {
            CancellationTokenSource cts = new CancellationTokenSource();

            var workspace = MSBuildWorkspace.Create();
            var solution = workspace.OpenSolutionAsync(solutionFilePath, cancellationToken: cts.Token).Result;

            var parser = new Parser(protocolCode);
            var document = parser.Document;
            var model = new ProtocolModel(document);
            var inputData = new ProtocolInputData(model, document, new QActionCompilationModel(model, solution));

            ValidatorSettings settings = new ValidatorSettings(
                GlobalDefaults.MinSupportedDmVersionWithBuildNumber, new UnitList(uomFilePath));
            Task<IList<IValidationResult>>[] tasks =
            {
                Task.Factory.StartNew(() =>
                {
                    // Old validator task
                    IValidator validator = new LegacyValidator();

                    return validator.RunValidate(inputData, settings, cts.Token);
                }),
                Task.Factory.StartNew(() =>
                {
                    // New validator task
					IValidator validator = new Validator();

                    return validator.RunValidate(inputData, settings, cts.Token);
                })
            };

            // Run validator tasks and combine results
            IList<IValidationResult> allResults = Task.WhenAll(tasks).Result.SelectMany(x => x).ToList();

            // Filter out suppressed and information messages
            CommentSuppressionManager suppressionManager = new CommentSuppressionManager(inputData.Document, new SimpleLineInfoProvider(protocolCode));
            FilterResults(allResults, suppressionManager);
        }

        public void FilterResults(IList<IValidationResult> allResults, CommentSuppressionManager suppressionManager, bool isParentSuppressed = false)
        {
            foreach (var result in allResults)
            {
                if (result.SubResults != null && result.SubResults.Count > 0)
                {
                    var suppress = isParentSuppressed || suppressionManager.IsSuppressed(result);
                    FilterResults(result.SubResults, suppressionManager, suppress);
                }
                else
                {
                    if (result.Severity == Severity.Information)
                    {
                        //validatorResults.InfoMessages.Add(result);
                    }
                    else if (isParentSuppressed || suppressionManager.IsSuppressed(result))
                    //else if (suppressionManager.TryFindSuppression(result, out Suppression suppression))
                    {
                        //string reason = suppression.Reason;
                        validatorResults.SuppressedErrors.Add(result);
                    }
                    else if (suppressionManager.AreAllChildrenSuppressed(result))
                    //else if (result.SubResults.All(x => suppressionManager.TryFindSuppression(x, out Suppression subSuppression)))
                    {
                        //string reason = "All subResults are suppressed.";
                        validatorResults.SuppressedErrors.Add(result);
                    }
                    else
                    {
                        validatorResults.Errors.Add(result);
                    }
                }
            }

            //Console.WriteLine("FilterResults2|validatorResults.Errors.Count '" + validatorResults.Errors.Count + "' - validatorResults.SuppressedErrors.Count '" + validatorResults.SuppressedErrors.Count + "' - validatorResults.InfoMessages.Count '" + validatorResults.InfoMessages.Count + "'.");
            //Console.WriteLine("FilterResults2|validatorResults.Errors.Count '" + validatorResults.Errors.Count + "' - validatorResults.SuppressedErrors.Count '" + validatorResults.SuppressedErrors.Count + "'.");
        }

        public void FilterMajorChangeCheckResults(IList<IValidationResult> allResults, VersionHistorySuppressionManager suppressionManager, bool isParentSuppressed = false)
        {
            foreach (var result in allResults)
            {
                if (result.SubResults != null && result.SubResults.Count > 0)
                {
                    FilterMajorChangeCheckResults(result.SubResults, suppressionManager, suppressionManager.IsSuppressed(result));
                }
                else
                {
                    if (result.Severity == Severity.Information)
                    {
                        //compareResults.InfoMessages.Add(result);
                    }
                    else if (isParentSuppressed || suppressionManager.IsSuppressed(result))
                    {
                        compareResults.SuppressedErrors.Add(result);
                    }
                    else if (suppressionManager.AreAllChildrenSuppressed(result))
                    {
                        compareResults.SuppressedErrors.Add(result);
                    }
                    else
                    {
                        compareResults.Errors.Add(result);
                    }
                }
            }

            //Console.WriteLine("FilterResults2|compareResults.Errors.Count '" + compareResults.Errors.Count + "' - compareResults.SuppressedErrors.Count '" + compareResults.SuppressedErrors.Count + "' - compareResults.InfoMessages.Count '" + compareResults.InfoMessages.Count + "'.");
            //Console.WriteLine("FilterResults2|compareResults.Errors.Count '" + compareResults.Errors.Count + "' - compareResults.SuppressedErrors.Count '" + compareResults.SuppressedErrors.Count + "'.");
        }

        private static string GetProtocolCode(string protocolFolderPath)
        {
            string protocolFilePath = Path.Combine(protocolFolderPath, "protocol.xml");

            if (!File.Exists(protocolFilePath))
            {
                throw new InvalidOperationException("protocol not found. Location '" + protocolFilePath + "'.");
            }

            string protocolCode = File.ReadAllText(protocolFilePath);
            return protocolCode;
        }

        private void SaveAllValidatorResults(string validatorResultsFilePath, string validatorSuppressedResultsFilePath)
        {
            // Errors
            SaveSpecificResults(validatorResults.Errors, validatorResultsFilePath);

            // Suppressed Errors
            SaveSpecificResults(validatorResults.SuppressedErrors, validatorSuppressedResultsFilePath);

            // Info Messages
            //SaveSpecificResults(validatorResults.InfoMessages, validatorFilteredResultsFilePath);
        }

        private void SaveAllCompareResults(string compareResultsFilePath, string compareSuppressedResultsFilePath)
        {
            // Errors
            SaveSpecificResults(compareResults.Errors, compareResultsFilePath);

            // Suppressed Errors
            SaveSpecificResults(compareResults.SuppressedErrors, compareSuppressedResultsFilePath);

            // Info Messages
            //SaveSpecificResults(compareResults.InfoMessages, compareFilteredResultsFilePath);
        }

        private static void SaveSpecificResults(IList<IValidationResult> results, string validatorResultsFilePath)
        {
            int lastBackslashIndex = validatorResultsFilePath.LastIndexOf('\\');
            if (lastBackslashIndex > -1)
            {
                string folderPath = validatorResultsFilePath.Substring(0, lastBackslashIndex);
                Directory.CreateDirectory(folderPath);
            }

            string sResults = ValidatorResultsSerializer.Serialize(results);
            File.WriteAllText(validatorResultsFilePath, sResults);
        }

        private Assembly MyResolveEventHandler(object sender, ResolveEventArgs args)
        {
            var result = ValidatorAssemblyLoader.GetAssembly(args, Path.Combine(dataMinerBasePath, "Files"));

            return result;
        }
    }
}
