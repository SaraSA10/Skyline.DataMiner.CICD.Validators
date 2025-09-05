using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Skyline.DataMiner.CICD.Validators.Common.Interfaces;
using Skyline.DataMiner.Scripting;

namespace ValidatorRunnerMajorChangeChecker
{
    public class ProtocolQActionHelperProvider : IProtocolQActionHelperProvider
    {
        private readonly string _protocolCode;

        public ProtocolQActionHelperProvider(string protocolCode)
        {
            _protocolCode = protocolCode;
        }

        public string GetProtocolQActionHelper()
        {
            var tempPath = Path.GetTempPath();
            var tempName = Guid.NewGuid().ToString("N");
            var tempFile = Path.Combine(tempPath, tempName + ".txt");

            var errors = QActionHelper.CreateProtocolQActionHelperFromString(_protocolCode, tempPath, tempName);
            if (errors != null && errors.Count > 0)
            {
                StringBuilder helpererrors = new StringBuilder();

                foreach (var err in errors)
                {
                    helpererrors.Append("\n    Line " + err.Line + ": " + err.Description);
                }

                throw new InvalidOperationException("Could not generate QAction helper: " + helpererrors);
            }

            var csContent = new StringBuilder();

            csContent.AppendLine("// --- auto-generated code --- do not modify ---");

            if (File.Exists(tempFile))
            {
                csContent.Append(File.ReadAllText(tempFile));
                File.Delete(tempFile);
            }

            return csContent.ToString();
        }

        public string GetProtocolQActionHelper(string protocolCode, bool ignoreErrors = false)
        {
            return GetProtocolQActionHelper();
        }
    }
}